from pathlib import Path
import sys

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
