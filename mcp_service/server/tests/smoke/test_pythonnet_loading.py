# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

import os
import xml.etree.ElementTree as ElementTree
from pathlib import Path

import pytest

from dwsim_mcp_server.ipc.session_client import SessionClient


class TestPythonnetLoading:
    def test_session_manager_lifecycle(self, dwsim_worker_dll_path, pythonnet_clr, monkeypatch):
        monkeypatch.setenv("DWSIM_WORKER_DLL", str(dwsim_worker_dll_path))
        if not _dwsim_install_present():
            pytest.skip("DWSIM assemblies not available. Set DWSIM_PATH to run this smoke test.")
        client = SessionClient(default_flowsheet_name="pythonnet-test")
        try:
            session_id = client.create_session()
            assert session_id
            assert client.close_session(session_id) is True
        finally:
            client.dispose()


def _dwsim_install_present() -> bool:
    env_path = os.getenv("DWSIM_PATH")
    if env_path and Path(env_path).exists():
        return True

    config_path = _dwsim_worker_config_path()
    if config_path and config_path.exists():
        try:
            config_dwsim_path = _read_app_setting(config_path, "DwsimPath")
        except Exception:
            config_dwsim_path = None
        if config_dwsim_path and Path(config_dwsim_path).exists():
            return True

    default_paths = (
        Path("C:/Program Files/DWSIM"),
        Path("C:/Program Files (x86)/DWSIM"),
        Path("C:/DWSIM"),
    )
    return any(path.exists() for path in default_paths)


def _dwsim_worker_config_path() -> Path | None:
    dll_path = (
        Path(__file__).resolve().parents[4]
        / "mcp_service"
        / "dwsim_worker"
        / "DwsimWorker"
        / "bin"
        / "Debug"
        / "DwsimWorker.dll.config"
    )
    return dll_path if dll_path.exists() else None


def _read_app_setting(config_path: Path, key: str) -> str | None:
    tree = ElementTree.parse(config_path)
    root = tree.getroot()
    app_settings = root.find("appSettings")
    if app_settings is None:
        return None

    for add in app_settings.findall("add"):
        if add.get("key") == key:
            return add.get("value")
    return None
