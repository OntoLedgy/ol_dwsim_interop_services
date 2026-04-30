# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Thermodynamic analysis MCP tools."""

from typing import Any, Awaitable, Callable, Dict, List, Tuple

from mcp import types
from fastmcp import Context
from pydantic import ValidationError

from dwsim_mcp_server.models.errors.resource_limit_error import ResourceLimitError
from dwsim_mcp_server.models.errors.simulation_error import SimulationError as SimulationErrorModel
from dwsim_mcp_server.models.mcp_inputs import FlashPHInputs, FlashPSInputs, FlashTPInputs
from dwsim_mcp_server.models.enums.physical_quantity_types import PhysicalQuantityTypes

from dwsim_mcp_server.ipc.exceptions import InteropError, SessionError
from dwsim_mcp_server.limits.resource_limit_guard import ResourceLimitViolation
from dwsim_mcp_server.observability import get_logger
from dwsim_mcp_server.tools.legacy import LegacyContext


class _ServiceUnavailable(Exception):
    pass


# --- Input transformation helpers ---

def _split_compounds(compounds: Dict[str, float]) -> Tuple[List[str], List[float]]:
    """Convert {compound: fraction} dict to parallel lists."""
    names = list(compounds.keys())
    fractions = list(compounds.values())
    return names, fractions


def _make_measurement(value: float, unit_str: str, quantity_type: PhysicalQuantityTypes) -> Dict[str, Any]:
    """Create a Measurements-compatible dict from simple value and unit string."""
    return {
        "quantity_type": quantity_type.value,
        "value": value,
        "unit": {
            "unit_name": unit_str,
            "quantity_type": quantity_type.value,
        },
    }


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


def build_analysis_tools() -> list[types.Tool]:
    return [
        _tool("flash_tp", _flash_tp_description(), FlashTPInputs),
        _tool("flash_ph", _flash_ph_description(), FlashPHInputs),
        _tool("flash_ps", _flash_ps_description(), FlashPSInputs),
    ]


async def handle_analysis_tool(
    tool_name: str, arguments: Dict[str, Any], dependencies
) -> Dict[str, Any] | types.CallToolResult:
    ctx = LegacyContext(dependencies)
    handlers = {
        "flash_tp": lambda: _flash_tp(
            ctx,
            session_id=arguments.get("session_id"),
            temperature=arguments.get("temperature"),
            pressure=arguments.get("pressure"),
            compounds=arguments.get("compounds"),
            units=arguments.get("units"),
        ),
        "flash_ph": lambda: _flash_ph(
            ctx,
            session_id=arguments.get("session_id"),
            pressure=arguments.get("pressure"),
            enthalpy=arguments.get("enthalpy"),
            compounds=arguments.get("compounds"),
            units=arguments.get("units"),
        ),
        "flash_ps": lambda: _flash_ps(
            ctx,
            session_id=arguments.get("session_id"),
            pressure=arguments.get("pressure"),
            entropy=arguments.get("entropy"),
            compounds=arguments.get("compounds"),
            units=arguments.get("units"),
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
    # Transform MCP-style inputs to internal model format
    compound_names, composition = _split_compounds(compounds)
    temp_unit = units.get("temperature", "K")
    press_unit = units.get("pressure", "Pa")

    payload = FlashTPInputs.model_validate(
        {
            "session_id": session_id,
            "compounds": compound_names,
            "composition": composition,
            "temperature": _make_measurement(temperature, temp_unit, PhysicalQuantityTypes.TEMPERATURE),
            "pressure": _make_measurement(pressure, press_unit, PhysicalQuantityTypes.PRESSURE),
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
    # Transform MCP-style inputs to internal model format
    compound_names, composition = _split_compounds(compounds)
    press_unit = units.get("pressure", "Pa")
    enthalpy_unit = units.get("enthalpy", "J/mol")

    payload = FlashPHInputs.model_validate(
        {
            "session_id": session_id,
            "compounds": compound_names,
            "composition": composition,
            "pressure": _make_measurement(pressure, press_unit, PhysicalQuantityTypes.PRESSURE),
            "enthalpy": _make_measurement(enthalpy, enthalpy_unit, PhysicalQuantityTypes.MOLAR_ENTHALPY),
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
    # Transform MCP-style inputs to internal model format
    compound_names, composition = _split_compounds(compounds)
    press_unit = units.get("pressure", "Pa")
    entropy_unit = units.get("entropy", "J/mol/K")

    payload = FlashPSInputs.model_validate(
        {
            "session_id": session_id,
            "compounds": compound_names,
            "composition": composition,
            "pressure": _make_measurement(pressure, press_unit, PhysicalQuantityTypes.PRESSURE),
            "entropy": _make_measurement(entropy, entropy_unit, PhysicalQuantityTypes.MOLAR_ENTROPY),
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


def _tool(name: str, description: str, model) -> types.Tool:
    return types.Tool(
        name=name,
        description=description,
        inputSchema=_schema(model),
    )


def _schema(model) -> Dict[str, Any]:
    return model.model_json_schema()


def _flash_tp_description() -> str:
    return (
        "Perform a temperature-pressure flash calculation for a mixture. "
        "Requires a session with a property package configured. Provide compound names and mole "
        "fractions (must sum to 1.0), plus temperature and pressure measurements with units."
    )


def _flash_ph_description() -> str:
    return (
        "Perform a pressure-enthalpy flash calculation for a mixture. "
        "Requires a session with a property package configured. Provide compound names and mole "
        "fractions (must sum to 1.0), plus pressure and molar enthalpy measurements with units."
    )


def _flash_ps_description() -> str:
    return (
        "Perform a pressure-entropy flash calculation for a mixture. "
        "Requires a session with a property package configured. Provide compound names and mole "
        "fractions (must sum to 1.0), plus pressure and molar entropy measurements with units."
    )
