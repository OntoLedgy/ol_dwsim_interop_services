import asyncio
from types import SimpleNamespace

import pytest
from mcp import types

from dwsim_mcp_server.ipc.exceptions import InteropError, SessionError
from dwsim_mcp_server.limits.resource_limit_guard import ResourceLimitViolation
from dwsim_mcp_server.tools.simulation import build_simulation_tools, handle_simulation_tool
from dwsim_mcp_server.models.errors.resource_limit_error import ResourceLimitError


class FakeSimulationClient:
    def __init__(self):
        self.run_calls = []
        self.status_calls = []
        self.results_calls = []

    async def run_calculation(self, session_id: str, *, timeout_seconds=None):
        self.run_calls.append((session_id, timeout_seconds))
        return {
            "status": "converged",
            "convergence_state": "Converged",
            "elapsed_ms": 10.0,
            "stream_results": [],
            "messages": [],
            "mass_balance_valid": True,
            "mass_balance_error_percent": 0.0,
        }

    async def get_calculation_status(self, session_id: str):
        self.status_calls.append(session_id)
        return {
            "status": "idle",
            "is_running": False,
            "last_run_timestamp": None,
            "elapsed_ms": None,
        }

    async def get_calculation_results(self, session_id: str, *, object_id=None):
        self.results_calls.append((session_id, object_id))
        return {
            "status": "converged",
            "convergence_state": "Converged",
            "elapsed_ms": 10.0,
            "stream_results": [],
            "messages": ["ok"],
            "mass_balance_valid": True,
            "mass_balance_error_percent": 0.0,
        }


def _dependencies(session_client=None):
    return SimpleNamespace(session_client=session_client or FakeSimulationClient())


def test_build_simulation_tools_registers_names():
    tools = build_simulation_tools()
    names = {tool.name for tool in tools}
    assert {"run", "get_status", "get_results"} <= names


def test_run_simulation_tool_happy_path():
    deps = _dependencies()
    result = asyncio.run(
        handle_simulation_tool("run", {"session_id": "session-1234", "timeout_seconds": 120}, deps)
    )
    assert result["status"] == "converged"
    assert deps.session_client.run_calls == [("session-1234", 120)]


def test_get_status_tool_happy_path():
    deps = _dependencies()
    result = asyncio.run(
        handle_simulation_tool("get_status", {"session_id": "session-1234"}, deps)
    )
    assert result["status"] == "idle"
    assert deps.session_client.status_calls == ["session-1234"]


def test_get_results_tool_happy_path():
    deps = _dependencies()
    result = asyncio.run(
        handle_simulation_tool(
            "get_results",
            {"session_id": "session-1234", "object_id": "stream-1"},
            deps,
        )
    )
    assert result["status"] == "converged"
    assert deps.session_client.results_calls == [("session-1234", "stream-1")]


def test_run_simulation_validation_error():
    deps = _dependencies()
    result = asyncio.run(handle_simulation_tool("run", {}, deps))
    assert isinstance(result, types.CallToolResult)
    assert result.isError is True
    assert result.structuredContent["code"] == "VALIDATION_ERROR"


def test_simulation_session_error_mapping():
    class ErrorClient(FakeSimulationClient):
        async def get_calculation_results(self, session_id: str, *, object_id=None):
            raise SessionError("No results available")

    deps = _dependencies(session_client=ErrorClient())
    result = asyncio.run(
        handle_simulation_tool("get_results", {"session_id": "missing"}, deps)
    )
    assert isinstance(result, types.CallToolResult)
    assert result.isError is True
    assert result.structuredContent["code"] == "SESSION_ERROR"


def test_simulation_interop_error_mapping():
    class ErrorClient(FakeSimulationClient):
        async def get_calculation_status(self, session_id: str):
            raise InteropError("Interop failed")

    deps = _dependencies(session_client=ErrorClient())
    result = asyncio.run(
        handle_simulation_tool("get_status", {"session_id": "session-1234"}, deps)
    )
    assert isinstance(result, types.CallToolResult)
    assert result.isError is True
    assert result.structuredContent["code"] == "INTEROP_ERROR"


def test_simulation_resource_limit_mapping():
    class ErrorClient(FakeSimulationClient):
        async def run_calculation(self, session_id: str, *, timeout_seconds=None):
            error = ResourceLimitError(
                code="TIMEOUT",
                message="Operation timed out.",
                session_id=session_id,
                details={"timeout_seconds": 1},
            )
            raise ResourceLimitViolation(error)

    deps = _dependencies(session_client=ErrorClient())
    result = asyncio.run(
        handle_simulation_tool("run", {"session_id": "session-1234"}, deps)
    )
    assert isinstance(result, types.CallToolResult)
    assert result.isError is True
    assert result.structuredContent["code"] == "TIMEOUT"
