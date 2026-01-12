"""Simulation execution MCP tools."""

from __future__ import annotations

from typing import Any, Dict

from mcp import types
from pydantic import ValidationError

from models.errors.resource_limit_error import ResourceLimitError
from models.errors.simulation_error import SimulationError as SimulationErrorModel
from models.requests import GetResultsRequest, GetStatusRequest, RunSimulationRequest
from models.responses import SimulationResultResponse, SimulationStatusResponse

from dwsim_mcp_server.ipc.exceptions import InteropError, SessionError
from dwsim_mcp_server.limits.resource_limit_guard import ResourceLimitViolation
from dwsim_mcp_server.observability import get_logger


def build_simulation_tools() -> list[types.Tool]:
    """Return MCP tool definitions for simulation execution."""
    return [
        types.Tool(
            name="run",
            title="Run Simulation",
            description=(
                "Execute the DWSIM solver for a session and return convergence details, "
                "timing, stream properties, and mass balance diagnostics."
            ),
            inputSchema=RunSimulationRequest.model_json_schema(),
            outputSchema=SimulationResultResponse.model_json_schema(),
        ),
        types.Tool(
            name="get_status",
            title="Get Simulation Status",
            description="Retrieve the latest simulation status for a session (idle, running, converged, failed).",
            inputSchema=GetStatusRequest.model_json_schema(),
            outputSchema=SimulationStatusResponse.model_json_schema(),
        ),
        types.Tool(
            name="get_results",
            title="Get Simulation Results",
            description=(
                "Retrieve cached simulation results for a session, optionally filtered by object ID."
            ),
            inputSchema=GetResultsRequest.model_json_schema(),
            outputSchema=SimulationResultResponse.model_json_schema(),
        ),
    ]


async def handle_simulation_tool(tool_name: str, arguments: Dict[str, Any], dependencies: Any):
    """Dispatch simulation tools by name."""
    logger = get_logger(__name__)
    session_client = dependencies.session_client

    try:
        if tool_name == "run":
            return await _handle_run(session_client, arguments, logger)
        if tool_name == "get_status":
            return await _handle_get_status(session_client, arguments, logger)
        if tool_name == "get_results":
            return await _handle_get_results(session_client, arguments, logger)

        return types.CallToolResult(
            content=[types.TextContent(type="text", text=f"Unknown tool: {tool_name}")],
            structuredContent={"code": "UNKNOWN_TOOL", "message": f"Unknown tool: {tool_name}"},
            isError=True,
        )
    except ValidationError as exc:
        return _error_result(code="VALIDATION_ERROR", message=str(exc))
    except ResourceLimitViolation as exc:
        return _resource_limit_error(exc.error)
    except SessionError as exc:
        return _error_result(code="SESSION_ERROR", message=str(exc))
    except InteropError as exc:
        return _error_result(code="INTEROP_ERROR", message=str(exc))
    except Exception as exc:
        logger.exception("simulation_tool_failed", extra={"tool": tool_name})
        return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


async def _handle_run(session_client: Any, arguments: Dict[str, Any], logger):
    payload = RunSimulationRequest.model_validate(arguments)
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


async def _handle_get_status(session_client: Any, arguments: Dict[str, Any], logger):
    payload = GetStatusRequest.model_validate(arguments)
    result = await session_client.get_calculation_status(payload.session_id)
    response = SimulationStatusResponse.model_validate(result)
    logger.info(
        "simulation_status_retrieved",
        session_id=payload.session_id,
        status=response.status,
        is_running=response.is_running,
    )
    return response.model_dump()


async def _handle_get_results(session_client: Any, arguments: Dict[str, Any], logger):
    payload = GetResultsRequest.model_validate(arguments)
    result = await session_client.get_calculation_results(
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
