# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

from pathlib import Path

import pytest
from typer.testing import CliRunner

from dwsim_mcp_server.cli import main
from dwsim_mcp_server.cli import setup as setup_module


@pytest.fixture()
def runner():
    return CliRunner()


class _FakeSetupManager:
    def __init__(self, console=None) -> None:
        self.console = console
        self.download_called = False
        self.detect_called = False

    def download_dwsim_binaries(self) -> Path:
        self.download_called = True
        return Path("C:/DWSIM")

    def detect_dwsim_path(self) -> Path | None:
        self.detect_called = True
        return Path("C:/DWSIM")

    def get_missing_requirements(self, path: Path):
        return []

    def create_config_file(self, path: Path) -> Path:
        return Path("C:/tmp/dwsim.config.json")


class _FailingDownloadManager(_FakeSetupManager):
    def download_dwsim_binaries(self) -> Path:
        raise RuntimeError("download failed")


class _MissingRequirementsManager(_FakeSetupManager):
    def get_missing_requirements(self, path: Path):
        return ["DWSIM.exe"]


class _NoDetectionManager(_FakeSetupManager):
    def detect_dwsim_path(self):
        return None


def test_setup_download_success(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "SetupManager", _FakeSetupManager)

    result = runner.invoke(main.app, ["setup", "--download"])

    assert result.exit_code == 0
    assert "DWSIM binaries downloaded" in result.output
    assert "DWSIM configuration created" in result.output


def test_setup_download_failure(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "SetupManager", _FailingDownloadManager)

    result = runner.invoke(main.app, ["setup", "--download"])

    assert result.exit_code == 1
    assert "download failed" in result.output


def test_setup_missing_requirements(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "SetupManager", _MissingRequirementsManager)

    result = runner.invoke(main.app, ["setup"])

    assert result.exit_code == 1
    assert "missing required files" in result.output


def test_setup_no_detection(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "SetupManager", _NoDetectionManager)

    result = runner.invoke(main.app, ["setup"])

    assert result.exit_code == 1
    assert "DWSIM installation not found" in result.output


def test_setup_non_windows_exits(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Linux")

    result = runner.invoke(main.app, ["setup"])

    assert result.exit_code == 1
    assert "only supported on Windows" in result.output


def test_download_uses_httpx_and_reports_checksum_failure(tmp_path, monkeypatch):
    manager = setup_module.SetupManager()

    def _download_target_dir():
        return tmp_path

    def _create_temp_download_path(_):
        return tmp_path / "temp.zip"

    monkeypatch.setattr(manager, "_download_target_dir", _download_target_dir)
    monkeypatch.setattr(manager, "_create_temp_download_path", _create_temp_download_path)

    class _Response:
        headers = {"Content-Length": "0"}

        def raise_for_status(self):
            return None

        def iter_bytes(self, chunk_size=0):
            yield b""

    class _Stream:
        def __enter__(self):
            return _Response()

        def __exit__(self, exc_type, exc, tb):
            return False

    class _Client:
        def __init__(self, *args, **kwargs):
            pass

        def __enter__(self):
            return self

        def __exit__(self, exc_type, exc, tb):
            return False

        def stream(self, *args, **kwargs):
            return _Stream()

    monkeypatch.setattr(setup_module.httpx, "Client", _Client)

    with pytest.raises(RuntimeError, match="Checksum verification failed"):
        manager.download_dwsim_binaries()
