import os
from pathlib import Path
import sys
import json

_server_root = Path(__file__).resolve().parent.parent
_repo_root = _server_root.parent.parent
if str(_repo_root) not in sys.path:
    sys.path.insert(0, str(_repo_root))

import pytest


@pytest.fixture(scope="session")
def server_root_path() -> Path:
    return Path(__file__).resolve().parent.parent


@pytest.fixture(scope="session")
def dwsim_worker_dll_path(server_root_path: Path) -> Path:
    dll_path = (
        server_root_path
        / ".."
        / "dwsim_worker"
        / "DwsimWorker"
        / "bin"
        / "Debug"
        / "DwsimWorker.dll"
    )
    if not dll_path.exists():
        pytest.skip(f"DwsimWorker.dll not found at: {dll_path}")
    return dll_path


@pytest.fixture(scope="session", autouse=True)
def set_dwsim_worker_env(dwsim_worker_dll_path: Path):
    """Ensure DWSIM worker DLL path is available for pythonnet loader."""
    os.environ.setdefault("DWSIM_WORKER_DLL", str(dwsim_worker_dll_path))
    yield


@pytest.fixture(scope="session", autouse=True)
def set_dwsim_path_from_shared_config():
    """Align Python tests with the same DWSIM path used by the worker tests.

    Reads mcp_service/dwsim_worker/dwsim.config.json if present and sets
    DWSIM_PATH for assembly discovery. Skips if the env var is already set.
    """
    if os.getenv("DWSIM_PATH"):
        yield
        return

    # __file__ -> mcp_service/server/tests/conftest.py
    # parents[2] -> mcp_service, so we can reach sibling dwsim_worker/
    config_path = Path(__file__).resolve().parents[2] / "dwsim_worker" / "dwsim.config.json"
    if not config_path.exists():
        yield
        return

    try:
        data = json.loads(config_path.read_text())
        dwsim_path = data.get("dwsim_path")
        if dwsim_path:
            os.environ["DWSIM_PATH"] = dwsim_path
    except Exception:
        pass

    yield


@pytest.fixture(scope="session")
def pythonnet_clr():
    try:
        import clr  # type: ignore
    except ImportError:
        pytest.skip("pythonnet is not installed. Run: pip install pythonnet")
    return clr


@pytest.fixture(scope="session")
def dwsim_worker_types(pythonnet_clr, dwsim_worker_dll_path: Path):
    pythonnet_clr.AddReference(str(dwsim_worker_dll_path))
    from DwsimWorker.Engine import SessionManager  # type: ignore
    from DwsimWorker.Engine import FlowsheetContextConfigBuilder  # type: ignore
    from Serilog import LoggerConfiguration  # type: ignore

    return SessionManager, FlowsheetContextConfigBuilder, LoggerConfiguration
