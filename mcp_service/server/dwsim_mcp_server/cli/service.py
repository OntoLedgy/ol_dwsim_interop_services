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

"""Windows service manager for dwsim-mcp using NSSM."""

from __future__ import annotations

import ctypes
import platform
import shutil
import subprocess
from pathlib import Path
from typing import Sequence

from rich.console import Console


class WindowsServiceManager:
    """Manage the dwsim-mcp server as a Windows service via NSSM."""

    SERVICE_NAME = "ol-dwsim-mcp-server"

    def __init__(self, console: Console | None = None) -> None:
        self._console = console or Console()

    def check_admin(self) -> bool:
        """Return True if running with administrative privileges."""
        try:
            return bool(ctypes.windll.shell32.IsUserAnAdmin())
        except Exception:
            return False

    def check_nssm(self) -> str | None:
        """Return NSSM path if available."""
        return shutil.which("nssm")

    def install(self, executable_path: str | Path) -> None:
        """Register the service with NSSM."""
        executable = self._resolve_executable(executable_path)
        result = self._run_nssm(["install", self.SERVICE_NAME, str(executable)])
        self._raise_on_failure(result, "install")

    def uninstall(self) -> None:
        """Remove the service with NSSM."""
        result = self._run_nssm(["remove", self.SERVICE_NAME, "confirm"])
        self._raise_on_failure(result, "remove")

    def start(self) -> None:
        """Start the service via NSSM."""
        result = self._run_nssm(["start", self.SERVICE_NAME])
        self._raise_on_failure(result, "start")

    def stop(self) -> None:
        """Stop the service via NSSM."""
        result = self._run_nssm(["stop", self.SERVICE_NAME])
        self._raise_on_failure(result, "stop")

    def status(self) -> str:
        """Return the service status from NSSM."""
        result = self._run_nssm(["status", self.SERVICE_NAME])
        self._raise_on_failure(result, "status")
        return (result.stdout or result.stderr or "").strip()

    def _ensure_windows(self) -> None:
        if platform.system().lower() != "windows":
            raise RuntimeError("Service management is only supported on Windows.")

    def _run_nssm(self, args: Sequence[str]) -> subprocess.CompletedProcess[str]:
        self._ensure_windows()
        nssm_path = self.check_nssm()
        if not nssm_path:
            raise RuntimeError(
                "NSSM is not available in PATH. Install NSSM and add it to PATH."
            )

        try:
            return subprocess.run(
                [nssm_path, *args],
                capture_output=True,
                text=True,
                check=False,
            )
        except OSError as exc:
            raise RuntimeError(f"Failed to run NSSM: {exc}") from exc

    def _raise_on_failure(self, result: subprocess.CompletedProcess[str], action: str) -> None:
        if result.returncode == 0:
            return
        details = (result.stderr or result.stdout or "Unknown error").strip()
        raise RuntimeError(f"NSSM {action} failed: {details}")

    def _resolve_executable(self, executable_path: str | Path) -> Path:
        candidate = Path(executable_path).expanduser()
        if candidate.exists():
            return candidate.resolve()

        fallback = shutil.which(str(executable_path))
        if fallback:
            return Path(fallback).resolve()

        raise RuntimeError(f"Executable not found: {executable_path}")
