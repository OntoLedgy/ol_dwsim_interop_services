# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# This file is part of the OntoLedgy Thermodynamics Architecture and is
# dual-licensed:
#
#   1. Open source under the GNU Affero General Public License v3.0 or
#      later (AGPL-3.0-or-later). See the LICENSE file in the repository
#      root for the full licence text and NOTICE for attribution.
#   2. Commercial under a separate proprietary licence offered by
#      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Utilities for loading the DwsimWorker .NET assembly via pythonnet."""

from __future__ import annotations

import logging
import os
from pathlib import Path
from typing import Optional

from dwsim_mcp_server.ipc.exceptions import AssemblyLoadError, map_dotnet_exception

_logger = logging.getLogger(__name__)


def resolve_dll_path(explicit_path: Optional[str] = None) -> Path:
    if explicit_path:
        dll_path = Path(explicit_path)
    else:
        dll_path = _resolve_from_environment() or _resolve_default_path()

    dll_path = dll_path.resolve()
    if not dll_path.exists():
        raise AssemblyLoadError(
            "DwsimWorker.dll not found at: "
            f"{dll_path}. Build the project or set DWSIM_WORKER_DLL."
        )
    return dll_path


def load_dwsim_worker(explicit_path: Optional[str] = None):
    """Load the DwsimWorker assembly and return the root module."""
    dll_path = resolve_dll_path(explicit_path)
    _inject_dwsim_path_env_var()
    try:
        import clr  # type: ignore
    except ImportError as exc:
        raise AssemblyLoadError(
            "pythonnet is not installed. Run: pip install pythonnet"
        ) from exc

    try:
        clr.AddReference(str(dll_path))
        import DwsimWorker  # type: ignore
    except Exception as exc:
        raise map_dotnet_exception(exc, kind="assembly_load") from exc

    return DwsimWorker


def _inject_dwsim_path_env_var() -> None:
    # PathResolver.cs in the .NET worker resolves DWSIM via several strategies,
    # the first of which is the DWSIM_PATH env var. The relative-path strategies
    # navigate up from Assembly.GetExecutingAssembly().Location, which is
    # unreliable when the assembly is loaded by pythonnet (Location can be empty
    # or a shadow-copy path), so the Python parent — which already knows where
    # `dwsim-mcp setup` placed the binaries — is the single source of truth.
    existing = os.environ.get("DWSIM_PATH")
    if existing:
        _logger.debug(
            "DWSIM_PATH already set to %s; not overriding from config.", existing
        )
        return

    from dwsim_mcp_server.cli.setup import DwsimSetupService

    dwsim_path = DwsimSetupService().read_config_dwsim_path()
    if dwsim_path is None:
        _logger.debug(
            "No DWSIM path in config; falling back to PathResolver strategies."
        )
        return

    os.environ["DWSIM_PATH"] = str(dwsim_path)
    _logger.info("Injected DWSIM_PATH=%s from dwsim.config.json.", dwsim_path)


def _resolve_from_environment() -> Optional[Path]:
    dll_path = os.getenv("DWSIM_WORKER_DLL")
    if dll_path:
        return Path(dll_path)

    worker_dir = os.getenv("DWSIM_WORKER_DIR")
    if worker_dir:
        configuration = os.getenv("DWSIM_WORKER_CONFIGURATION", "Debug")
        return (
            Path(worker_dir)
            / "DwsimWorker"
            / "bin"
            / configuration
            / "DwsimWorker.dll"
        )
    return None


def _resolve_default_path() -> Path:
    bundled = Path(__file__).resolve().parent.parent / "_prebuilt" / "DwsimWorker.dll"
    if bundled.exists():
        return bundled

    server_root = Path(__file__).resolve().parents[2]
    configuration = os.getenv("DWSIM_WORKER_CONFIGURATION", "Debug")
    return (
        server_root
        / ".."
        / "dwsim_worker"
        / "DwsimWorker"
        / "bin"
        / configuration
        / "DwsimWorker.dll"
    )
