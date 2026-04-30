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

"""Doctor command checks for DWSIM MCP CLI."""

from __future__ import annotations

import json
import platform
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable

from rich.console import Console
from rich.table import Table

from dwsim_mcp_server.cli.setup import SetupManager
from dwsim_mcp_server.ipc.clr_loader import resolve_dll_path
from dwsim_mcp_server.ipc.exceptions import AssemblyLoadError


@dataclass(frozen=True)
class DoctorCheck:
    status: str
    message: str
    remediation: str


class DoctorRunner:
    """Run dependency checks for the DWSIM MCP CLI."""

    _STATUS_STYLES = {"pass": "green", "fail": "red", "warn": "yellow"}
    _MIN_PYTHON = (3, 11)
    _DOTNET_RELEASE_MIN = 528040

    def __init__(self, console: Console | None = None) -> None:
        self._console = console or Console()

    def run(self, *, verbose: bool = False, quiet: bool = False) -> int:
        checks = [
            ("Python version", self.check_python_version),
            (".NET Framework 4.8", self.check_dotnet_framework),
            ("pythonnet / clr", self.check_pythonnet),
            ("DwsimWorker.dll", self.check_dwsim_worker_dll),
            ("DWSIM config", self.check_config_file),
            ("DWSIM assemblies", self.check_dwsim_assemblies),
        ]

        results: list[tuple[str, DoctorCheck]] = []
        for label, check in checks:
            results.append((label, self._safe_check(check)))

        self._render_results(results, verbose=verbose, quiet=quiet)
        return 0 if all(item.status == "pass" for _, item in results) else 1

    def check_python_version(self) -> DoctorCheck:
        version = sys.version_info
        if (version.major, version.minor) >= self._MIN_PYTHON:
            return DoctorCheck(
                status="pass",
                message=f"Python {version.major}.{version.minor}.{version.micro} detected.",
                remediation="",
            )
        return DoctorCheck(
            status="fail",
            message=f"Python {version.major}.{version.minor}.{version.micro} is below 3.11.",
            remediation="Install Python 3.11+ and ensure dwsim-mcp uses it.",
        )

    def check_dotnet_framework(self) -> DoctorCheck:
        if platform.system().lower() != "windows":
            return DoctorCheck(
                status="warn",
                message="Windows registry check skipped on non-Windows.",
                remediation="Run this command on Windows to validate .NET Framework 4.8.",
            )

        try:
            import winreg

            with winreg.OpenKey(
                winreg.HKEY_LOCAL_MACHINE,
                r"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full",
            ) as key:
                release, _ = winreg.QueryValueEx(key, "Release")
        except FileNotFoundError:
            return DoctorCheck(
                status="fail",
                message="Unable to find .NET Framework v4 Full registry key.",
                remediation="Install .NET Framework 4.8 Runtime or Developer Pack.",
            )
        except OSError as exc:
            return DoctorCheck(
                status="fail",
                message=f"Failed to read .NET Framework registry: {exc}",
                remediation="Install .NET Framework 4.8 Runtime or Developer Pack.",
            )

        if int(release) >= self._DOTNET_RELEASE_MIN:
            return DoctorCheck(
                status="pass",
                message=f".NET Framework release {release} detected.",
                remediation="",
            )
        return DoctorCheck(
            status="fail",
            message=f".NET Framework release {release} is below 4.8.",
            remediation="Install .NET Framework 4.8 Runtime or Developer Pack.",
        )

    def check_pythonnet(self) -> DoctorCheck:
        try:
            import pythonnet  # type: ignore
        except Exception:
            return DoctorCheck(
                status="fail",
                message="pythonnet is not installed.",
                remediation="Install pythonnet (pip install pythonnet).",
            )

        version = getattr(pythonnet, "__version__", "unknown")
        try:
            import clr  # type: ignore  # noqa: F401
        except Exception as exc:
            return DoctorCheck(
                status="fail",
                message=f"pythonnet {version} installed but clr import failed: {exc}",
                remediation="Reinstall pythonnet and ensure .NET Framework 4.8 is installed.",
            )

        return DoctorCheck(
            status="pass",
            message=f"pythonnet {version} and clr are available.",
            remediation="",
        )

    def check_dwsim_worker_dll(self) -> DoctorCheck:
        try:
            dll_path = resolve_dll_path()
        except AssemblyLoadError as exc:
            return DoctorCheck(
                status="fail",
                message=str(exc),
                remediation=(
                    "Build DwsimWorker or set DWSIM_WORKER_DLL / DWSIM_WORKER_DIR."
                ),
            )
        except Exception as exc:
            return DoctorCheck(
                status="fail",
                message=f"Failed to resolve DwsimWorker.dll: {exc}",
                remediation=(
                    "Build DwsimWorker or set DWSIM_WORKER_DLL / DWSIM_WORKER_DIR."
                ),
            )

        return DoctorCheck(
            status="pass",
            message=f"Found DwsimWorker.dll at {dll_path}.",
            remediation="",
        )

    def check_config_file(self) -> DoctorCheck:
        config_path = self._config_path()
        if not config_path.exists():
            return DoctorCheck(
                status="fail",
                message=f"Config file not found at {config_path}.",
                remediation="Run dwsim-mcp setup --dwsim-path <path> or --download.",
            )

        try:
            payload = json.loads(config_path.read_text(encoding="utf-8-sig"))
        except Exception as exc:
            return DoctorCheck(
                status="fail",
                message=f"Config file is not valid JSON: {exc}",
                remediation="Recreate the config with dwsim-mcp setup.",
            )

        dwsim_path = payload.get("dwsim_path")
        if not dwsim_path:
            return DoctorCheck(
                status="fail",
                message="Config missing required key: dwsim_path.",
                remediation="Update dwsim.config.json or rerun dwsim-mcp setup.",
            )

        # Resolve relative paths relative to config file directory
        path = Path(dwsim_path)
        if not path.is_absolute():
            path = (config_path.parent / path).resolve()

        if not path.exists():
            return DoctorCheck(
                status="fail",
                message=f"dwsim_path does not exist: {path} (from config: {dwsim_path})",
                remediation="Update dwsim.config.json to point to a valid DWSIM install, or run dwsim-mcp setup --download.",
            )

        return DoctorCheck(
            status="pass",
            message=f"Config OK. dwsim_path={path}",
            remediation="",
        )

    def check_dwsim_assemblies(self) -> DoctorCheck:
        config_path = self._config_path()
        if not config_path.exists():
            return DoctorCheck(
                status="fail",
                message="Cannot validate DWSIM assemblies without config file.",
                remediation="Run dwsim-mcp setup to create dwsim.config.json.",
            )

        try:
            payload = json.loads(config_path.read_text(encoding="utf-8-sig"))
        except Exception as exc:
            return DoctorCheck(
                status="fail",
                message=f"Config file is not valid JSON: {exc}",
                remediation="Recreate the config with dwsim-mcp setup.",
            )

        dwsim_path = payload.get("dwsim_path")
        if not dwsim_path:
            return DoctorCheck(
                status="fail",
                message="dwsim_path is missing from config.",
                remediation="Update dwsim.config.json or rerun dwsim-mcp setup.",
            )

        # Resolve relative paths relative to config file directory
        base_dir = Path(dwsim_path)
        if not base_dir.is_absolute():
            base_dir = (config_path.parent / base_dir).resolve()

        if base_dir.is_file() and base_dir.name.lower() == "dwsim.exe":
            base_dir = base_dir.parent

        if not base_dir.exists():
            return DoctorCheck(
                status="fail",
                message=f"DWSIM path does not exist: {base_dir}",
                remediation="Update dwsim.config.json or reinstall DWSIM.",
            )

        required_files = SetupManager.REQUIRED_FILES
        missing = [name for name in required_files if not (base_dir / name).exists()]
        if missing:
            return DoctorCheck(
                status="fail",
                message="Missing required DWSIM files: " + ", ".join(missing),
                remediation="Install DWSIM or update dwsim_path to a valid install.",
            )

        return DoctorCheck(
            status="pass",
            message=f"DWSIM assemblies present at {base_dir}.",
            remediation="",
        )

    def _render_results(
        self,
        results: Iterable[tuple[str, DoctorCheck]],
        *,
        verbose: bool,
        quiet: bool,
    ) -> None:
        table = Table(title="dwsim-mcp doctor", show_header=True, header_style="bold")
        table.add_column("Status", style="bold")
        table.add_column("Check")
        table.add_column("Details", overflow="fold")

        counts = {"pass": 0, "fail": 0, "warn": 0}

        for label, result in results:
            counts[result.status] = counts.get(result.status, 0) + 1
            if quiet and result.status == "pass":
                continue

            style = self._STATUS_STYLES.get(result.status, "white")
            status_text = f"[{style}]{result.status.upper()}[/{style}]"
            details = result.message
            if result.remediation and (verbose or result.status != "pass"):
                details = f"{details}\n[dim]Remediation:[/] {result.remediation}"

            table.add_row(status_text, label, details)

        self._console.print(table)
        summary = (
            f"[green]{counts.get('pass', 0)} passed[/green], "
            f"[yellow]{counts.get('warn', 0)} warnings[/yellow], "
            f"[red]{counts.get('fail', 0)} failed[/red]"
        )
        self._console.print(summary)

    def _safe_check(self, check: Callable[[], DoctorCheck]) -> DoctorCheck:
        try:
            return check()
        except Exception as exc:
            return DoctorCheck(
                status="fail",
                message=f"Unexpected error: {exc}",
                remediation="Re-run with --verbose and report the error.",
            )

    def _config_path(self) -> Path:
        root = Path(__file__).resolve().parents[3]
        return root / "dwsim_worker" / "dwsim.config.json"
