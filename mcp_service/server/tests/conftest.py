import os
from datetime import datetime
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
def pythonnet_clr(dwsim_worker_dll_path: Path):
    try:
        import clr  # type: ignore
    except ImportError:
        pytest.skip("pythonnet is not installed. Run: pip install pythonnet")

    # Set up assembly resolver to handle version mismatches
    # pythonnet doesn't respect binding redirects, so we need to manually redirect
    _setup_assembly_resolver(clr, dwsim_worker_dll_path)

    return clr


def _setup_assembly_resolver(clr, dwsim_worker_dll_path: Path):
    """Set up assembly resolver to handle version conflicts in pythonnet.

    Binding redirects in App.config don't work with pythonnet because Python
    is the host process. This resolver intercepts assembly load requests and
    redirects to the correct versions available in the build output.
    """
    from System import AppDomain, ResolveEventHandler  # type: ignore
    from System.Reflection import Assembly  # type: ignore

    bin_dir = dwsim_worker_dll_path.parent

    # Map of assembly names to version redirects
    # Key: assembly name, Value: path to the correct assembly
    assembly_redirects = {}

    # Find System.Runtime.CompilerServices.Unsafe in the build output
    unsafe_dll = bin_dir / "System.Runtime.CompilerServices.Unsafe.dll"
    if unsafe_dll.exists():
        assembly_redirects["System.Runtime.CompilerServices.Unsafe"] = str(unsafe_dll)

    # Find System.Diagnostics.DiagnosticSource in the build output
    diag_dll = bin_dir / "System.Diagnostics.DiagnosticSource.dll"
    if diag_dll.exists():
        assembly_redirects["System.Diagnostics.DiagnosticSource"] = str(diag_dll)

    def _resolve_assembly(sender, args):
        """Resolve assembly requests by redirecting to available versions."""
        try:
            # Parse assembly name (format: "Name, Version=x.x.x.x, Culture=..., PublicKeyToken=...")
            full_name = args.Name
            simple_name = full_name.split(",")[0].strip()

            if simple_name in assembly_redirects:
                dll_path = assembly_redirects[simple_name]
                return Assembly.LoadFrom(dll_path)
        except Exception:
            pass
        return None

    # Register the resolver
    AppDomain.CurrentDomain.AssemblyResolve += ResolveEventHandler(_resolve_assembly)


@pytest.fixture(scope="session")
def dwsim_worker_types(pythonnet_clr, dwsim_worker_dll_path: Path):
    pythonnet_clr.AddReference(str(dwsim_worker_dll_path))
    from DwsimWorker.Engine import SessionManager  # type: ignore
    from DwsimWorker.Engine import FlowsheetContextConfigBuilder  # type: ignore
    from Serilog import LoggerConfiguration  # type: ignore

    return SessionManager, FlowsheetContextConfigBuilder, LoggerConfiguration


# =============================================================================
# Test Output Fixtures
# =============================================================================

@pytest.fixture(scope="session")
def test_outputs_root() -> Path:
    """Base directory for all test outputs.

    Returns:
        Path to mcp_service/server/tests/output/
    """
    outputs_dir = Path(__file__).resolve().parent / "output"
    outputs_dir.mkdir(parents=True, exist_ok=True)
    return outputs_dir


@pytest.fixture(scope="function")
def test_run_output_dir(request, test_outputs_root: Path) -> Path:
    """Create a timestamped output directory for the current test.

    Directory structure:
        tests/output/<test_name>/<timestamp>/

    Example:
        tests/output/test_sensitivity_analysis_end_to_end/20260121_163045/

    Returns:
        Path to the test-specific timestamped output directory.
    """
    test_name = request.node.name
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_dir = test_outputs_root / test_name / timestamp
    output_dir.mkdir(parents=True, exist_ok=True)
    return output_dir


@pytest.fixture(scope="function")
def test_output_writer(test_run_output_dir: Path):
    """Helper to write test outputs to files.

    Usage:
        def test_something(test_output_writer):
            result = run_analysis()
            test_output_writer("result.json", result, format="json")
            test_output_writer("data.csv", rows, format="csv")
    """
    def _write(filename: str, data, format: str = "auto"):
        filepath = test_run_output_dir / filename

        if format == "auto":
            if filename.endswith(".json"):
                format = "json"
            elif filename.endswith(".csv"):
                format = "csv"
            else:
                format = "text"

        if format == "json":
            import json as json_mod
            with open(filepath, "w", encoding="utf-8") as f:
                if hasattr(data, "model_dump"):
                    json_mod.dump(data.model_dump(), f, indent=2, default=str)
                elif hasattr(data, "__dict__"):
                    json_mod.dump(data.__dict__, f, indent=2, default=str)
                else:
                    json_mod.dump(data, f, indent=2, default=str)
        elif format == "csv":
            import csv as csv_mod
            with open(filepath, "w", encoding="utf-8", newline="") as f:
                if isinstance(data, list) and len(data) > 0:
                    if hasattr(data[0], "model_dump"):
                        rows = [row.model_dump() for row in data]
                    elif isinstance(data[0], dict):
                        rows = data
                    else:
                        rows = [{"value": row} for row in data]
                    if rows:
                        writer = csv_mod.DictWriter(f, fieldnames=rows[0].keys())
                        writer.writeheader()
                        writer.writerows(rows)
                else:
                    f.write(str(data))
        else:
            with open(filepath, "w", encoding="utf-8") as f:
                f.write(str(data))

        return filepath

    return _write
