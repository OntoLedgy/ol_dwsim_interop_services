from __future__ import annotations

import hashlib
import json
import os
import tempfile
import zipfile
from pathlib import Path
from typing import Iterable

import httpx
from rich.console import Console
from rich.progress import (
    BarColumn,
    DownloadColumn,
    Progress,
    TextColumn,
    TimeRemainingColumn,
    TransferSpeedColumn,
)


class SetupManager:
    """Manage DWSIM setup and configuration for the CLI."""

    DOWNLOAD_URL = (
        "https://github.com/OntoLedgy/dwsim/releases/download/v9.0.5-mcp/"
        "dwsim_binaries.zip"
    )
    DOWNLOAD_SHA256 = "bf34a1a68d1ca7f32372ef4ee4bd81a6fd8a19b2174d3a621817b45f191040f1"
    DOWNLOAD_CHUNK_SIZE = 1024 * 1024
    REQUIRED_FILES = (
        "DWSIM.exe",
        "DWSIM.Thermodynamics.dll",
        "CapeOpen.dll",
    )

    def __init__(self, console: Console | None = None) -> None:
        self._console = console or Console()

    def detect_dwsim_path(self) -> Path | None:
        """Detect the DWSIM installation directory."""
        for candidate in self._candidate_paths():
            resolved = self._normalize_candidate(candidate)
            if resolved is None:
                continue
            if self.validate_dwsim_installation(resolved):
                return resolved
        return None

    def validate_dwsim_installation(self, path: Path) -> bool:
        """Validate the DWSIM installation path contains required files."""
        if not path.exists():
            return False

        base_dir = path
        if path.is_file() and path.name.lower() == "dwsim.exe":
            base_dir = path.parent

        missing = [
            filename
            for filename in self.REQUIRED_FILES
            if not (base_dir / filename).exists()
        ]
        return not missing

    def get_missing_requirements(self, path: Path) -> list[str]:
        """Return missing required files for a candidate path."""
        if not path.exists():
            return list(self.REQUIRED_FILES)

        base_dir = path
        if path.is_file() and path.name.lower() == "dwsim.exe":
            base_dir = path.parent

        return [
            filename
            for filename in self.REQUIRED_FILES
            if not (base_dir / filename).exists()
        ]

    def create_config_file(self, dwsim_path: Path) -> Path:
        """Create the DWSIM configuration JSON file."""
        config_path = self._config_path()
        config_path.parent.mkdir(parents=True, exist_ok=True)
        payload = {"dwsim_path": str(dwsim_path)}
        config_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
        return config_path

    def download_dwsim_binaries(self) -> Path:
        """Download, verify, and extract DWSIM binaries into the repo worker."""
        target_dir = self._download_target_dir()
        target_dir.mkdir(parents=True, exist_ok=True)

        temp_path = self._create_temp_download_path(target_dir)
        checksum = hashlib.sha256()

        try:
            with httpx.Client(follow_redirects=True, timeout=60.0) as client:
                with client.stream("GET", self.DOWNLOAD_URL) as response:
                    response.raise_for_status()
                    total = response.headers.get("Content-Length")
                    total_size = int(total) if total else None

                    progress = Progress(
                        TextColumn("[progress.description]{task.description}"),
                        BarColumn(),
                        DownloadColumn(),
                        TransferSpeedColumn(),
                        TimeRemainingColumn(),
                        console=self._console,
                    )
                    with progress:
                        task_id = progress.add_task(
                            "Downloading DWSIM binaries", total=total_size
                        )
                        with temp_path.open("wb") as handle:
                            for chunk in response.iter_bytes(
                                chunk_size=self.DOWNLOAD_CHUNK_SIZE
                            ):
                                if not chunk:
                                    continue
                                handle.write(chunk)
                                checksum.update(chunk)
                                progress.update(task_id, advance=len(chunk))
        except httpx.HTTPError as exc:
            self._cleanup_temp_file(temp_path)
            raise RuntimeError(
                "Failed to download DWSIM binaries. Check your network connection "
                "and retry with `dwsim-mcp setup --download`."
            ) from exc
        except OSError as exc:
            self._cleanup_temp_file(temp_path)
            raise RuntimeError(
                "Failed to write the downloaded DWSIM binaries. Ensure you have "
                "write access and retry with `dwsim-mcp setup --download`."
            ) from exc

        actual_hash = checksum.hexdigest().lower()
        if actual_hash != self.DOWNLOAD_SHA256:
            self._cleanup_temp_file(temp_path)
            raise RuntimeError(
                "Checksum verification failed for the downloaded DWSIM binaries. "
                "Delete any partial files and retry with `dwsim-mcp setup --download`."
            )

        try:
            with zipfile.ZipFile(temp_path) as archive:
                archive.extractall(target_dir)
        except zipfile.BadZipFile as exc:
            self._cleanup_temp_file(temp_path)
            raise RuntimeError(
                "Downloaded DWSIM archive is invalid. Delete any partial files and "
                "retry with `dwsim-mcp setup --download`."
            ) from exc
        finally:
            self._cleanup_temp_file(temp_path)

        return target_dir

    def _config_path(self) -> Path:
        root = Path(__file__).resolve().parents[3]
        return root / "dwsim_worker" / "dwsim.config.json"

    def _download_target_dir(self) -> Path:
        repo_root = Path(__file__).resolve().parents[3]
        return repo_root / "dwsim_worker" / "dwsim_binaries" / "x64" / "Debug"

    def _create_temp_download_path(self, target_dir: Path) -> Path:
        temp_file = tempfile.NamedTemporaryFile(
            suffix=".zip", delete=False, dir=str(target_dir)
        )
        temp_path = Path(temp_file.name)
        temp_file.close()
        return temp_path

    def _cleanup_temp_file(self, path: Path) -> None:
        try:
            path.unlink(missing_ok=True)
        except OSError:
            pass

    def _candidate_paths(self) -> Iterable[Path]:
        repo_root = Path(__file__).resolve().parents[3]
        repo_candidates = [
            repo_root / "dwsim_worker" / "dwsim_binaries" / "x64" / "Debug",
            repo_root / "dwsim_worker" / "dwsim_binaries" / "x64" / "Release",
        ]

        program_files = os.environ.get("ProgramFiles")
        program_files_x86 = os.environ.get("ProgramFiles(x86)")
        local_appdata = os.environ.get("LOCALAPPDATA")

        candidates = [
            Path("C:/DWSIM"),
            Path("C:/DWSIM7"),
            Path("C:/DWSIM8"),
            Path("C:/DWSIM9"),
            Path("C:/Program Files/DWSIM"),
            Path("C:/Program Files (x86)/DWSIM"),
        ]

        if program_files:
            candidates.extend(
                [
                    Path(program_files) / "DWSIM",
                    Path(program_files) / "DWSIM9",
                    Path(program_files) / "DWSIM8",
                ]
            )

        if program_files_x86:
            candidates.extend(
                [
                    Path(program_files_x86) / "DWSIM",
                    Path(program_files_x86) / "DWSIM9",
                    Path(program_files_x86) / "DWSIM8",
                ]
            )

        if local_appdata:
            candidates.append(Path(local_appdata) / "DWSIM")

        return [*repo_candidates, *candidates]

    def _normalize_candidate(self, candidate: Path) -> Path | None:
        if not candidate.exists():
            return None
        if candidate.is_file() and candidate.name.lower() == "dwsim.exe":
            return candidate.parent
        if candidate.is_dir():
            return candidate
        return None
