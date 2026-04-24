from __future__ import annotations

from pathlib import Path

import pytest
from typer.testing import CliRunner

from dwsim_mcp_server.cli import main
from dwsim_mcp_server.cli import service as service_module


@pytest.fixture()
def runner():
    return CliRunner()


class _FakeServiceManager:
    SERVICE_NAME = "ol-dwsim-mcp-server"

    def __init__(self, console=None) -> None:
        self.console = console
        self.installed = False
        self.uninstalled = False
        self.started = False
        self.stopped = False

    def check_admin(self):
        return True

    def check_nssm(self):
        return "nssm"

    def install(self, executable_path: str | Path):
        self.installed = True

    def uninstall(self):
        self.uninstalled = True

    def start(self):
        self.started = True

    def stop(self):
        self.stopped = True

    def status(self):
        return "RUNNING"


class _NonAdminManager(_FakeServiceManager):
    def check_admin(self):
        return False


class _NoNssmManager(_FakeServiceManager):
    def check_nssm(self):
        return None


class _FailingActionManager(_FakeServiceManager):
    def start(self):
        raise RuntimeError("nssm failed")


def test_service_install_success(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "WindowsServiceManager", _FakeServiceManager)
    mocker.patch.object(main.shutil, "which", return_value="C:/bin/dwsim-mcp")

    result = runner.invoke(main.app, ["service", "install"])

    assert result.exit_code == 0
    assert "Service installed" in result.output


def test_service_install_requires_executable(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "WindowsServiceManager", _FakeServiceManager)
    mocker.patch.object(main.shutil, "which", return_value=None)

    result = runner.invoke(main.app, ["service", "install"])

    assert result.exit_code == 1
    assert "Executable not provided" in result.output


def test_service_start_failure_path(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "WindowsServiceManager", _FailingActionManager)

    result = runner.invoke(main.app, ["service", "start"])

    assert result.exit_code == 1
    assert "nssm failed" in result.output


def test_service_status_outputs_result(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "WindowsServiceManager", _FakeServiceManager)

    result = runner.invoke(main.app, ["service", "status"])

    assert result.exit_code == 0
    assert "RUNNING" in result.output


def test_service_status_empty_output(mocker, runner):
    class _NoStatusManager(_FakeServiceManager):
        def status(self):
            return ""

    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "WindowsServiceManager", _NoStatusManager)

    result = runner.invoke(main.app, ["service", "status"])

    assert result.exit_code == 0
    assert "No status output" in result.output


def test_service_uninstall_success(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "WindowsServiceManager", _FakeServiceManager)

    result = runner.invoke(main.app, ["service", "uninstall"])

    assert result.exit_code == 0
    assert "Service removed" in result.output


def test_service_stop_success(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "WindowsServiceManager", _FakeServiceManager)

    result = runner.invoke(main.app, ["service", "stop"])

    assert result.exit_code == 0
    assert "Service stopped" in result.output


def test_service_start_success(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "WindowsServiceManager", _FakeServiceManager)

    result = runner.invoke(main.app, ["service", "start"])

    assert result.exit_code == 0
    assert "Service started" in result.output


def test_service_environment_requires_admin(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "WindowsServiceManager", _NonAdminManager)

    result = runner.invoke(main.app, ["service", "status"])

    assert result.exit_code == 1
    assert "Administrative privileges" in result.output


def test_service_environment_requires_nssm(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Windows")
    mocker.patch.object(main, "WindowsServiceManager", _NoNssmManager)

    result = runner.invoke(main.app, ["service", "status"])

    assert result.exit_code == 1
    assert "NSSM was not found" in result.output


def test_service_environment_non_windows(mocker, runner):
    mocker.patch.object(main.platform, "system", return_value="Linux")
    mocker.patch.object(main, "WindowsServiceManager", _FakeServiceManager)

    result = runner.invoke(main.app, ["service", "status"])

    assert result.exit_code == 1
    assert "only supported on Windows" in result.output


def test_run_nssm_invokes_subprocess(monkeypatch):
    manager = service_module.WindowsServiceManager()

    monkeypatch.setattr(service_module.platform, "system", lambda: "Windows")
    monkeypatch.setattr(manager, "check_nssm", lambda: "C:/nssm.exe")

    called = {}

    def _run(args, capture_output, text, check):
        called["args"] = args
        called["capture_output"] = capture_output
        called["text"] = text
        called["check"] = check
        return service_module.subprocess.CompletedProcess(args, 0, stdout="OK", stderr="")

    monkeypatch.setattr(service_module.subprocess, "run", _run)

    result = manager._run_nssm(["status", manager.SERVICE_NAME])

    assert result.stdout == "OK"
    assert called["args"][0] == "C:/nssm.exe"


def test_run_nssm_raises_on_missing_nssm(monkeypatch):
    manager = service_module.WindowsServiceManager()

    monkeypatch.setattr(service_module.platform, "system", lambda: "Windows")
    monkeypatch.setattr(manager, "check_nssm", lambda: None)

    with pytest.raises(RuntimeError, match="NSSM is not available"):
        manager._run_nssm(["status", manager.SERVICE_NAME])


def test_raise_on_failure(monkeypatch):
    manager = service_module.WindowsServiceManager()
    result = service_module.subprocess.CompletedProcess(["nssm"], 1, stdout="", stderr="bad")

    with pytest.raises(RuntimeError, match="NSSM start failed"):
        manager._raise_on_failure(result, "start")
