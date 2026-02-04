"""Simulation execution MCP tools."""

from __future__ import annotations

from typing import Any, Awaitable, Callable, Dict

from mcp import types
from fastmcp import Context
from pydantic import ValidationError

from dwsim_mcp_server.models.errors.resource_limit_error import ResourceLimitError
from dwsim_mcp_server.models.errors.simulation_error import SimulationError as SimulationErrorModel
from dwsim_mcp_server.models.requests import GetResultsRequest, GetStatusRequest, RunSimulationRequest
from dwsim_mcp_server.models.responses import SimulationResultResponse, SimulationStatusResponse

from dwsim_mcp_server.ipc.exceptions import InteropError, SessionError
from dwsim_mcp_server.limits.resource_limit_guard import ResourceLimitViolation
from dwsim_mcp_server.observability import get_logger
from dwsim_mcp_server.tools.legacy import LegacyContext


def register_simulation_tools(mcp) -> None:
    """Register simulation tools with FastMCP."""

    @mcp.tool(
        description=(
            "Execute the DWSIM solver for a session. PREREQUISITES: 1) All compounds added, "
            "2) Property package set, 3) BIPs configured, 4) Feed stream created and flashed, "
            "5) Outlet streams created, 6) Unit operation added, 7) All streams connected. "
            "Returns convergence status, stream properties, and mass balance diagnostics."
        )
    )
    async def run(session_id: str, timeout_seconds: int | None = None, ctx: Context | None = None):
        return await _execute_tool(
            "run",
            lambda: _run_simulation(ctx, session_id=session_id, timeout_seconds=timeout_seconds),
        )

    @mcp.tool(
        description="Retrieve the latest simulation status for a session: idle, running, converged, or failed."
    )
    async def get_status(session_id: str, ctx: Context | None = None):
        return await _execute_tool(
            "get_status",
            lambda: _get_status(ctx, session_id=session_id),
        )

    @mcp.tool(
        description=(
            "Retrieve detailed simulation results including all stream properties, phase compositions, "
            "thermodynamic properties (enthalpy, entropy, density, viscosity), and mass balance error."
        )
    )
    async def get_results(session_id: str, object_id: str | None = None, ctx: Context | None = None):
        return await _execute_tool(
            "get_results",
            lambda: _get_results(ctx, session_id=session_id, object_id=object_id),
        )


def build_simulation_tools() -> list[types.Tool]:
    return [
        _tool("run", _run_description(), RunSimulationRequest),
        _tool("get_status", _status_description(), GetStatusRequest),
        _tool("get_results", _results_description(), GetResultsRequest),
    ]


async def handle_simulation_tool(
    tool_name: str, arguments: Dict[str, Any], dependencies
) -> Dict[str, Any] | types.CallToolResult:
    ctx = LegacyContext(dependencies)
    handlers = {
        "run": lambda: _run_simulation(
            ctx,
            session_id=arguments.get("session_id"),
            timeout_seconds=arguments.get("timeout_seconds"),
        ),
        "get_status": lambda: _get_status(
            ctx, session_id=arguments.get("session_id")
        ),
        "get_results": lambda: _get_results(
            ctx,
            session_id=arguments.get("session_id"),
            object_id=arguments.get("object_id"),
        ),
    }
    handler = handlers.get(tool_name)
    if handler is None:
        return _error_result(code="UNKNOWN_TOOL", message=f"Unknown tool: {tool_name}")
    return await _execute_tool(tool_name, handler)


async def _execute_tool(
    tool_name: str, handler: Callable[[], Awaitable[Dict[str, Any]]]
) -> Dict[str, Any] | types.CallToolResult:
    logger = get_logger(__name__)
    try:
        return await handler()
    except Exception as exc:  # noqa: BLE001 - map all tool failures
        return _handle_tool_error(logger, tool_name, exc)


def _handle_tool_error(logger, tool_name: str, exc: Exception) -> types.CallToolResult:
    if isinstance(exc, ValidationError):
        return _error_result(code="VALIDATION_ERROR", message=str(exc))
    if isinstance(exc, ResourceLimitViolation):
        return _resource_limit_error(exc.error)
    if isinstance(exc, SessionError):
        return _error_result(code="SESSION_ERROR", message=str(exc))
    if isinstance(exc, InteropError):
        return _error_result(code="INTEROP_ERROR", message=str(exc))
    logger.exception("simulation_tool_failed", extra={"tool": tool_name})
    return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


def _get_session_client(ctx: Context | None):
    return ctx.lifespan_context.session_client


async def _run_simulation(
    ctx: Context | None, *, session_id: str, timeout_seconds: int | None
) -> Dict[str, Any]:
    logger = get_logger(__name__)
    payload_data = {"session_id": session_id}
    if timeout_seconds is not None:
        payload_data["timeout_seconds"] = timeout_seconds
    payload = RunSimulationRequest.model_validate(payload_data)
    session_client = _get_session_client(ctx)
    logger.info("simulation_run_requested", session_id=payload.session_id, timeout_seconds=payload.timeout_seconds)
    result = await session_client.run_calculation(payload.session_id, timeout_seconds=payload.timeout_seconds)
    response = SimulationResultResponse.model_validate(result)
    logger.info(
        "simulation_run_completed",
        session_id=payload.session_id,
        status=response.status,
        convergence_state=response.convergence_state,
    )
    return response.model_dump()


async def _get_status(ctx: Context | None, *, session_id: str) -> Dict[str, Any]:
    logger = get_logger(__name__)
    payload = GetStatusRequest.model_validate({"session_id": session_id})
    result = await _get_session_client(ctx).get_calculation_status(payload.session_id)
    response = SimulationStatusResponse.model_validate(result)
    logger.info(
        "simulation_status_retrieved",
        session_id=payload.session_id,
        status=response.status,
        is_running=response.is_running,
    )
    return response.model_dump()


async def _get_results(ctx: Context | None, *, session_id: str, object_id: str | None) -> Dict[str, Any]:
    logger = get_logger(__name__)
    payload = GetResultsRequest.model_validate({"session_id": session_id, "object_id": object_id})
    result = await _get_session_client(ctx).get_calculation_results(
        payload.session_id,
        object_id=payload.object_id,
    )
    response = SimulationResultResponse.model_validate(result)
    logger.info(
        "simulation_results_retrieved",
        session_id=payload.session_id,
        status=response.status,
        stream_count=len(response.stream_results),
    )
    return response.model_dump()


def _error_result(code: str, message: str) -> types.CallToolResult:
    payload = SimulationErrorModel(code=code, message=message).model_dump()
    return types.CallToolResult(
        content=[types.TextContent(type="text", text=message)],
        structuredContent=payload,
        isError=True,
    )


def _resource_limit_error(error: ResourceLimitError) -> types.CallToolResult:
    payload = error.model_dump()
    return types.CallToolResult(
        content=[types.TextContent(type="text", text=error.message)],
        structuredContent=payload,
        isError=True,
    )


def _tool(name: str, description: str, model) -> types.Tool:
    return types.Tool(
        name=name,
        description=description,
        inputSchema=_schema(model),
    )


def _schema(model) -> Dict[str, Any]:
    return model.model_json_schema()


def _run_description() -> str:
    return (
        "Execute the DWSIM solver for a session. PREREQUISITES: 1) All compounds added, "
        "2) Property package set, 3) BIPs configured, 4) Feed stream created and flashed, "
        "5) Outlet streams created, 6) Unit operation added, 7) All streams connected. "
        "Returns convergence status, stream properties, and mass balance diagnostics."
    )


def _status_description() -> str:
    return (
        "Retrieve the latest simulation status for a session: idle, running, converged, or failed."
    )


def _results_description() -> str:
    return (
        "Retrieve detailed simulation results including all stream properties, phase compositions, "
        "thermodynamic properties (enthalpy, entropy, density, viscosity), and mass balance error."
    )
