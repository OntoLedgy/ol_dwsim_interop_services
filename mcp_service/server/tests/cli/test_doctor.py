from __future__ import annotations

import builtins
import json
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest
from typer.testing import CliRunner

from dwsim_mcp_server.cli import doctor as doctor_module
from dwsim_mcp_server.cli import main
from dwsim_mcp_server.ipc.exceptions import AssemblyLoadError


@pytest.fixture()
def runner():
    return CliRunner()


def test_doctor_command_exit_codes(mocker, runner):
    class _RunnerOk:
        def run(self, *args, **kwargs):
            return 0

    class _RunnerFail:
        def run(self, *args, **kwargs):
            return 2

    mocker.patch.object(main, "DoctorRunner", _RunnerOk)
    result_ok = runner.invoke(main.app, ["doctor"])

    mocker.patch.object(main, "DoctorRunner", _RunnerFail)
    result_fail = runner.invoke(main.app, ["doctor"])

    assert result_ok.exit_code == 0
    assert result_fail.exit_code == 2


def test_check_python_version_pass(monkeypatch):
    runner = doctor_module.DoctorRunner()
    monkeypatch.setattr(doctor_module.sys, "version_info", SimpleNamespace(major=3, minor=11, micro=1))

    result = runner.check_python_version()

    assert result.status == "pass"


def test_check_python_version_fail(monkeypatch):
    runner = doctor_module.DoctorRunner()
    monkeypatch.setattr(doctor_module.sys, "version_info", SimpleNamespace(major=3, minor=10, micro=9))

    result = runner.check_python_version()

    assert result.status == "fail"
    assert "below 3.11" in result.message


def _install_fake_winreg(monkeypatch, release: int | None, *, raise_exc: Exception | None = None):
    class _Key:
        def __enter__(self):
            if raise_exc:
                raise raise_exc
            return self

        def __exit__(self, exc_type, exc, tb):
            return False

    class _WinregModule:
        HKEY_LOCAL_MACHINE = object()

        def OpenKey(self, *args, **kwargs):
            if raise_exc:
                raise raise_exc
            return _Key()

        def QueryValueEx(self, key, name):
            if raise_exc:
                raise raise_exc
            return release, None

    monkeypatch.setitem(sys.modules, "winreg", _WinregModule())


def test_check_dotnet_framework_warns_on_non_windows(monkeypatch):
    runner = doctor_module.DoctorRunner()
    monkeypatch.setattr(doctor_module.platform, "system", lambda: "Linux")

    result = runner.check_dotnet_framework()

    assert result.status == "warn"


def test_check_dotnet_framework_pass(monkeypatch):
    runner = doctor_module.DoctorRunner()
    monkeypatch.setattr(doctor_module.platform, "system", lambda: "Windows")
    _install_fake_winreg(monkeypatch, doctor_module.DoctorRunner._DOTNET_RELEASE_MIN)

    result = runner.check_dotnet_framework()

    assert result.status == "pass"


def test_check_dotnet_framework_fail_low_release(monkeypatch):
    runner = doctor_module.DoctorRunner()
    monkeypatch.setattr(doctor_module.platform, "system", lambda: "Windows")
    _install_fake_winreg(monkeypatch, 460000)

    result = runner.check_dotnet_framework()

    assert result.status == "fail"
    assert "below 4.8" in result.message


def test_check_dotnet_framework_fail_missing_key(monkeypatch):
    runner = doctor_module.DoctorRunner()
    monkeypatch.setattr(doctor_module.platform, "system", lambda: "Windows")
    _install_fake_winreg(monkeypatch, None, raise_exc=FileNotFoundError())

    result = runner.check_dotnet_framework()

    assert result.status == "fail"
    assert "registry key" in result.message


def test_check_pythonnet_missing(monkeypatch):
    runner = doctor_module.DoctorRunner()

    real_import = builtins.__import__

    def _import(name, *args, **kwargs):
        if name == "pythonnet":
            raise ImportError("no pythonnet")
        return real_import(name, *args, **kwargs)

    monkeypatch.setattr(builtins, "__import__", _import)

    result = runner.check_pythonnet()

    assert result.status == "fail"
    assert "pythonnet is not installed" in result.message


def test_check_pythonnet_clr_failure(monkeypatch):
    runner = doctor_module.DoctorRunner()

    class _PyNet:
        __version__ = "3.0"

    monkeypatch.setitem(sys.modules, "pythonnet", _PyNet())

    real_import = builtins.__import__

    def _import(name, *args, **kwargs):
        if name == "clr":
            raise ImportError("no clr")
        return real_import(name, *args, **kwargs)

    monkeypatch.setattr(builtins, "__import__", _import)

    result = runner.check_pythonnet()

    assert result.status == "fail"
    assert "clr import failed" in result.message


def test_check_pythonnet_pass(monkeypatch):
    runner = doctor_module.DoctorRunner()

    class _PyNet:
        __version__ = "3.0"

    monkeypatch.setitem(sys.modules, "pythonnet", _PyNet())
    monkeypatch.setitem(sys.modules, "clr", object())

    result = runner.check_pythonnet()

    assert result.status == "pass"


def test_check_dwsim_worker_dll_fail(monkeypatch):
    runner = doctor_module.DoctorRunner()

    def _raise():
        raise AssemblyLoadError("no dll")

    monkeypatch.setattr(doctor_module, "resolve_dll_path", _raise)

    result = runner.check_dwsim_worker_dll()

    assert result.status == "fail"
    assert "no dll" in result.message


def test_check_dwsim_worker_dll_pass(monkeypatch, tmp_path):
    runner = doctor_module.DoctorRunner()
    dll_path = tmp_path / "DwsimWorker.dll"

    monkeypatch.setattr(doctor_module, "resolve_dll_path", lambda: dll_path)

    result = runner.check_dwsim_worker_dll()

    assert result.status == "pass"
    assert str(dll_path) in result.message


def test_check_config_file_missing(monkeypatch, tmp_path):
    runner = doctor_module.DoctorRunner()
    config_path = tmp_path / "dwsim.config.json"
    monkeypatch.setattr(runner, "_config_path", lambda: config_path)

    result = runner.check_config_file()

    assert result.status == "fail"
    assert "not found" in result.message


def test_check_config_file_invalid_json(monkeypatch, tmp_path):
    runner = doctor_module.DoctorRunner()
    config_path = tmp_path / "dwsim.config.json"
    config_path.write_text("invalid", encoding="utf-8")
    monkeypatch.setattr(runner, "_config_path", lambda: config_path)

    result = runner.check_config_file()

    assert result.status == "fail"
    assert "not valid JSON" in result.message


def test_check_config_file_missing_key(monkeypatch, tmp_path):
    runner = doctor_module.DoctorRunner()
    config_path = tmp_path / "dwsim.config.json"
    config_path.write_text(json.dumps({}), encoding="utf-8")
    monkeypatch.setattr(runner, "_config_path", lambda: config_path)

    result = runner.check_config_file()

    assert result.status == "fail"
    assert "dwsim_path" in result.message


def test_check_config_file_path_missing(monkeypatch, tmp_path):
    runner = doctor_module.DoctorRunner()
    config_path = tmp_path / "dwsim.config.json"
    config_path.write_text(json.dumps({"dwsim_path": str(tmp_path / "missing")}), encoding="utf-8")
    monkeypatch.setattr(runner, "_config_path", lambda: config_path)

    result = runner.check_config_file()

    assert result.status == "fail"
    assert "does not exist" in result.message


def test_check_config_file_pass(monkeypatch, tmp_path):
    runner = doctor_module.DoctorRunner()
    config_path = tmp_path / "dwsim.config.json"
    dwsim_path = tmp_path / "DWSIM"
    dwsim_path.mkdir()
    config_path.write_text(json.dumps({"dwsim_path": str(dwsim_path)}), encoding="utf-8")
    monkeypatch.setattr(runner, "_config_path", lambda: config_path)

    result = runner.check_config_file()

    assert result.status == "pass"


def test_check_dwsim_assemblies_missing_config(monkeypatch, tmp_path):
    runner = doctor_module.DoctorRunner()
    config_path = tmp_path / "dwsim.config.json"
    monkeypatch.setattr(runner, "_config_path", lambda: config_path)

    result = runner.check_dwsim_assemblies()

    assert result.status == "fail"
    assert "without config" in result.message


def test_check_dwsim_assemblies_invalid_json(monkeypatch, tmp_path):
    runner = doctor_module.DoctorRunner()
    config_path = tmp_path / "dwsim.config.json"
    config_path.write_text("invalid", encoding="utf-8")
    monkeypatch.setattr(runner, "_config_path", lambda: config_path)

    result = runner.check_dwsim_assemblies()

    assert result.status == "fail"
    assert "not valid JSON" in result.message


def test_check_dwsim_assemblies_missing_path(monkeypatch, tmp_path):
    runner = doctor_module.DoctorRunner()
    config_path = tmp_path / "dwsim.config.json"
    config_path.write_text(json.dumps({}), encoding="utf-8")
    monkeypatch.setattr(runner, "_config_path", lambda: config_path)

    result = runner.check_dwsim_assemblies()

    assert result.status == "fail"
    assert "missing" in result.message


def test_check_dwsim_assemblies_missing_files(monkeypatch, tmp_path):
    runner = doctor_module.DoctorRunner()
    config_path = tmp_path / "dwsim.config.json"
    base_dir = tmp_path / "DWSIM"
    base_dir.mkdir()
    config_path.write_text(json.dumps({"dwsim_path": str(base_dir)}), encoding="utf-8")
    monkeypatch.setattr(runner, "_config_path", lambda: config_path)

    result = runner.check_dwsim_assemblies()

    assert result.status == "fail"
    assert "Missing required DWSIM files" in result.message


def test_check_dwsim_assemblies_pass(monkeypatch, tmp_path):
    runner = doctor_module.DoctorRunner()
    config_path = tmp_path / "dwsim.config.json"
    base_dir = tmp_path / "DWSIM"
    base_dir.mkdir()

    for name in doctor_module.SetupManager.REQUIRED_FILES:
        (base_dir / name).write_text("", encoding="utf-8")

    config_path.write_text(json.dumps({"dwsim_path": str(base_dir)}), encoding="utf-8")
    monkeypatch.setattr(runner, "_config_path", lambda: config_path)

    result = runner.check_dwsim_assemblies()

    assert result.status == "pass"
