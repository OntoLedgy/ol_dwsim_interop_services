"""Thermodynamic analysis MCP tools."""

from __future__ import annotations

from typing import Any, Awaitable, Callable, Dict

from mcp import types
from mcp.server.fastmcp.server import Context
from pydantic import ValidationError

from dwsim_mcp_server.models.errors.resource_limit_error import ResourceLimitError
from dwsim_mcp_server.models.errors.simulation_error import SimulationError as SimulationErrorModel
from dwsim_mcp_server.models.mcp_inputs import FlashPHInputs, FlashPSInputs, FlashTPInputs

from dwsim_mcp_server.ipc.exceptions import InteropError, SessionError
from dwsim_mcp_server.limits.resource_limit_guard import ResourceLimitViolation
from dwsim_mcp_server.observability import get_logger


class _ServiceUnavailable(Exception):
    pass


def register_analysis_tools(mcp) -> None:
    """Register thermodynamic analysis tools with FastMCP."""

    @mcp.tool(
        description=(
            "Perform a temperature-pressure flash calculation for a mixture. "
            "Requires a session with a property package configured. Provide compound names and mole "
            "fractions (must sum to 1.0), plus temperature and pressure measurements with units."
        )
    )
    async def flash_tp(session_id: str, temperature: float, pressure: float, compounds: Dict[str, float], units: Dict[str, str], ctx: Context | None = None):
        return await _execute_tool(
            "flash_tp",
            lambda: _flash_tp(
                ctx,
                session_id=session_id,
                temperature=temperature,
                pressure=pressure,
                compounds=compounds,
                units=units,
            ),
        )

    @mcp.tool(
        description=(
            "Perform a pressure-enthalpy flash calculation for a mixture. "
            "Requires a session with a property package configured. Provide compound names and mole "
            "fractions (must sum to 1.0), plus pressure and molar enthalpy measurements with units."
        )
    )
    async def flash_ph(session_id: str, pressure: float, enthalpy: float, compounds: Dict[str, float], units: Dict[str, str], ctx: Context | None = None):
        return await _execute_tool(
            "flash_ph",
            lambda: _flash_ph(
                ctx,
                session_id=session_id,
                pressure=pressure,
                enthalpy=enthalpy,
                compounds=compounds,
                units=units,
            ),
        )

    @mcp.tool(
        description=(
            "Perform a pressure-entropy flash calculation for a mixture. "
            "Requires a session with a property package configured. Provide compound names and mole "
            "fractions (must sum to 1.0), plus pressure and molar entropy measurements with units."
        )
    )
    async def flash_ps(session_id: str, pressure: float, entropy: float, compounds: Dict[str, float], units: Dict[str, str], ctx: Context | None = None):
        return await _execute_tool(
            "flash_ps",
            lambda: _flash_ps(
                ctx,
                session_id=session_id,
                pressure=pressure,
                entropy=entropy,
                compounds=compounds,
                units=units,
            ),
        )


async def _execute_tool(
    tool_name: str, handler: Callable[[], Awaitable[Dict[str, Any]]]
) -> Dict[str, Any] | types.CallToolResult:
    logger = get_logger(__name__)
    try:
        return await handler()
    except Exception as exc:  # noqa: BLE001 - map all tool failures
        return _handle_tool_error(logger, tool_name, exc)


def _handle_tool_error(logger, tool_name: str, exc: Exception) -> types.CallToolResult:
    if isinstance(exc, _ServiceUnavailable):
        return _error_result(code="SERVICE_UNAVAILABLE", message=str(exc))
    if isinstance(exc, ValidationError):
        return _error_result(code="VALIDATION_ERROR", message=str(exc))
    if isinstance(exc, ResourceLimitViolation):
        return _resource_limit_error(exc.error)
    if isinstance(exc, SessionError):
        return _error_result(code="SESSION_ERROR", message=str(exc))
    if isinstance(exc, InteropError):
        return _error_result(code="INTEROP_ERROR", message=str(exc))
    logger.exception("analysis_tool_failed", extra={"tool": tool_name})
    return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


def _get_service(ctx: Context | None):
    service = ctx.request_context.lifespan_context.thermodynamics_service
    if service is None:
        raise _ServiceUnavailable("Thermodynamics service is not configured.")
    return service


async def _flash_tp(
    ctx: Context | None,
    *,
    session_id: str,
    temperature: float,
    pressure: float,
    compounds: Dict[str, float],
    units: Dict[str, str],
) -> Dict[str, Any]:
    payload = FlashTPInputs.model_validate(
        {
            "session_id": session_id,
            "temperature": temperature,
            "pressure": pressure,
            "compounds": compounds,
            "units": units,
        }
    )
    return (await _get_service(ctx).flash_tp(payload)).model_dump()


async def _flash_ph(
    ctx: Context | None,
    *,
    session_id: str,
    pressure: float,
    enthalpy: float,
    compounds: Dict[str, float],
    units: Dict[str, str],
) -> Dict[str, Any]:
    payload = FlashPHInputs.model_validate(
        {
            "session_id": session_id,
            "pressure": pressure,
            "enthalpy": enthalpy,
            "compounds": compounds,
            "units": units,
        }
    )
    return (await _get_service(ctx).flash_ph(payload)).model_dump()


async def _flash_ps(
    ctx: Context | None,
    *,
    session_id: str,
    pressure: float,
    entropy: float,
    compounds: Dict[str, float],
    units: Dict[str, str],
) -> Dict[str, Any]:
    payload = FlashPSInputs.model_validate(
        {
            "session_id": session_id,
            "pressure": pressure,
            "entropy": entropy,
            "compounds": compounds,
            "units": units,
        }
    )
    return (await _get_service(ctx).flash_ps(payload)).model_dump()


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
