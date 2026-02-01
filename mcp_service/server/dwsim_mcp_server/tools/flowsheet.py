"""Flowsheet-building MCP tools."""

from __future__ import annotations

from typing import Any, Awaitable, Callable, Dict

from mcp import types
from mcp.server.fastmcp.server import Context
from pydantic import ValidationError

from dwsim_mcp_server.models.errors.session_error import SessionError as SessionErrorModel
from dwsim_mcp_server.models.mcp_inputs import (
    AddCompoundInput,
    AddCompoundOutput,
    AddStreamInput,
    AddStreamOutput,
    AddUnitInput,
    AddUnitOutput,
    ConnectInput,
    ConnectOutput,
    DeleteObjectInput,
    DeleteObjectOutput,
    FlashStreamInput,
    FlashStreamOutput,
    ListObjectsInput,
    ListObjectsOutput,
    SetBinaryInteractionParameterInput,
    SetBinaryInteractionParameterOutput,
    SetObjectParameterInput,
    SetObjectParameterOutput,
    SetPropertyPackageInput,
    SetPropertyPackageOutput,
)

from dwsim_mcp_server.observability import get_logger


class _ServiceUnavailable(Exception):
    pass


def register_flowsheet_tools(mcp) -> None:
    """Register flowsheet tools with FastMCP."""

    @mcp.tool(
        description=(
            "Add a compound from the DWSIM databank to the session (idempotent). "
            "Supports aliases like CO2, H2O, and isobutane; common inputs include "
            "Methane, n-Butane, Water, and Carbon Dioxide."
        )
    )
    async def add_compound(session_id: str, compound_name: str, ctx: Context | None = None):
        return await _execute_tool(
            "add_compound",
            lambda: _add_compound(ctx, session_id=session_id, compound_name=compound_name),
        )

    @mcp.tool(
        description="Set a thermodynamic property package for the session. Common packages: Peng-Robinson (general hydrocarbons), SRK (gases), NRTL (polar/non-ideal), UNIQUAC (activity coefficients)."
    )
    async def set_property_package(
        session_id: str, property_package: str, ctx: Context | None = None
    ):
        return await _execute_tool(
            "set_property_package",
            lambda: _set_property_package(
                ctx, session_id=session_id, property_package=property_package
            ),
        )

    @mcp.tool(
        description=(
            "Create a material stream. For FEED streams: set is_source=true with T(K), "
            "P(Pa), molar_flow(mol/s), composition; for OUTLET streams: set is_source=false "
            "and omit composition to auto-fill equal fractions (DWSIM computes real values). "
            "Always flash feed streams after creation."
        )
    )
    async def add_stream(session_id: str, stream_name: str, stream_type: str, is_source: bool, temperature: float | None = None, pressure: float | None = None, molar_flow: float | None = None, composition: Dict[str, float] | None = None, ctx: Context | None = None):
        return await _execute_tool(
            "add_stream",
            lambda: _add_stream(
                ctx,
                session_id=session_id,
                stream_name=stream_name,
                stream_type=stream_type,
                is_source=is_source,
                temperature=temperature,
                pressure=pressure,
                molar_flow=molar_flow,
                composition=composition,
            ),
        )

    @mcp.tool(
        description="Create a unit operation. Currently supported: unit_type='separator' (three-phase separator). Parameters: CalculationMode='Legacy', PressureCalculation='Average', DimensionRatio=3.0, ResidenceTime=5.0."
    )
    async def add_unit(session_id: str, unit_name: str, unit_type: str, parameters: Dict[str, Any] | None = None, ctx: Context | None = None):
        return await _execute_tool(
            "add_unit",
            lambda: _add_unit(
                ctx,
                session_id=session_id,
                unit_name=unit_name,
                unit_type=unit_type,
                parameters=parameters,
            ),
        )

    @mcp.tool(
        description="Connect a stream to a unit operation port. For separator: port_name can be 'Inlet', 'VaporOutlet', 'LiquidOutlet1', or 'LiquidOutlet2'. source_id is the stream ID, target_id is the unit ID."
    )
    async def connect(session_id: str, source_id: str, target_id: str, port_name: str, ctx: Context | None = None):
        return await _execute_tool(
            "connect",
            lambda: _connect(
                ctx,
                session_id=session_id,
                source_id=source_id,
                target_id=target_id,
                port_name=port_name,
            ),
        )

    @mcp.tool(
        description="List all streams, units, and connections in the current session. Use to verify flowsheet topology before running simulation."
    )
    async def list_objects(session_id: str, ctx: Context | None = None):
        return await _execute_tool(
            "list_objects",
            lambda: _list_objects(ctx, session_id=session_id),
        )

    @mcp.tool(description="Update a parameter on a flowsheet object.")
    async def set_object_parameter(session_id: str, object_id: str, parameter_name: str, parameter_value: Any, ctx: Context | None = None):
        return await _execute_tool(
            "set_object_parameter",
            lambda: _set_object_parameter(
                ctx,
                session_id=session_id,
                object_id=object_id,
                parameter_name=parameter_name,
                parameter_value=parameter_value,
            ),
        )

    @mcp.tool(description="Delete a flowsheet object and orphaned connections safely.")
    async def delete_object(session_id: str, object_id: str, ctx: Context | None = None):
        return await _execute_tool(
            "delete_object",
            lambda: _delete_object(ctx, session_id=session_id, object_id=object_id),
        )

    @mcp.tool(
        description="Perform flash calculation on a feed stream to compute phase equilibrium. MUST be called on feed streams (is_source=true) after creation and before running simulation."
    )
    async def flash_stream(session_id: str, stream_id: str, ctx: Context | None = None):
        return await _execute_tool(
            "flash_stream",
            lambda: _flash_stream(ctx, session_id=session_id, stream_id=stream_id),
        )

    @mcp.tool(
        description="Set a binary interaction parameter (BIP) for a pair of compounds. CRITICAL for accurate phase equilibrium. Typical values: hydrocarbon pairs ~0.01-0.05, water-hydrocarbon ~0.5. Set AFTER property package, BEFORE adding streams."
    )
    async def set_binary_interaction_parameter(session_id: str, compound_a: str, compound_b: str, interaction_value: float, ctx: Context | None = None):
        return await _execute_tool(
            "set_binary_interaction_parameter",
            lambda: _set_binary_interaction_parameter(
                ctx,
                session_id=session_id,
                compound_a=compound_a,
                compound_b=compound_b,
                interaction_value=interaction_value,
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
    logger.exception("flowsheet_tool_failed", extra={"tool": tool_name})
    return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


def _get_service(ctx: Context | None):
    service = ctx.request_context.lifespan_context.flowsheet_service
    if service is None:
        raise _ServiceUnavailable("Flowsheet service is not configured.")
    return service


async def _add_compound(ctx: Context | None, *, session_id: str, compound_name: str) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = AddCompoundInput.model_validate(
        {"session_id": session_id, "compound_name": compound_name}
    )
    return (await service.add_compound(payload)).model_dump()


async def _set_property_package(
    ctx: Context | None, *, session_id: str, property_package: str
) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = SetPropertyPackageInput.model_validate(
        {"session_id": session_id, "property_package": property_package}
    )
    return (await service.set_property_package(payload)).model_dump()


async def _add_stream(
    ctx: Context | None,
    *,
    session_id: str,
    stream_name: str,
    stream_type: str,
    is_source: bool,
    temperature: float | None,
    pressure: float | None,
    molar_flow: float | None,
    composition: Dict[str, float] | None,
) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = AddStreamInput.model_validate(
        {
            "session_id": session_id,
            "stream_name": stream_name,
            "stream_type": stream_type,
            "is_source": is_source,
            "temperature": temperature,
            "pressure": pressure,
            "molar_flow": molar_flow,
            "composition": composition,
        }
    )
    return (await service.add_stream(payload)).model_dump()


async def _add_unit(
    ctx: Context | None,
    *,
    session_id: str,
    unit_name: str,
    unit_type: str,
    parameters: Dict[str, Any] | None,
) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = AddUnitInput.model_validate(
        {
            "session_id": session_id,
            "unit_name": unit_name,
            "unit_type": unit_type,
            "parameters": parameters,
        }
    )
    return (await service.add_unit(payload)).model_dump()


async def _connect(
    ctx: Context | None,
    *,
    session_id: str,
    source_id: str,
    target_id: str,
    port_name: str,
) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = ConnectInput.model_validate(
        {
            "session_id": session_id,
            "source_id": source_id,
            "target_id": target_id,
            "port_name": port_name,
        }
    )
    return (await service.connect(payload)).model_dump()


async def _list_objects(ctx: Context | None, *, session_id: str) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = ListObjectsInput.model_validate({"session_id": session_id})
    return (await service.list_objects(payload)).model_dump()


async def _set_object_parameter(
    ctx: Context | None,
    *,
    session_id: str,
    object_id: str,
    parameter_name: str,
    parameter_value: Any,
) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = SetObjectParameterInput.model_validate(
        {
            "session_id": session_id,
            "object_id": object_id,
            "parameter_name": parameter_name,
            "parameter_value": parameter_value,
        }
    )
    return (await service.set_object_parameter(payload)).model_dump()


async def _delete_object(ctx: Context | None, *, session_id: str, object_id: str) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = DeleteObjectInput.model_validate(
        {"session_id": session_id, "object_id": object_id}
    )
    return (await service.delete_object(payload)).model_dump()


async def _flash_stream(ctx: Context | None, *, session_id: str, stream_id: str) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = FlashStreamInput.model_validate({"session_id": session_id, "stream_id": stream_id})
    return (await service.flash_stream(payload)).model_dump()


async def _set_binary_interaction_parameter(
    ctx: Context | None,
    *,
    session_id: str,
    compound_a: str,
    compound_b: str,
    interaction_value: float,
) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = SetBinaryInteractionParameterInput.model_validate(
        {
            "session_id": session_id,
            "compound_a": compound_a,
            "compound_b": compound_b,
            "interaction_value": interaction_value,
        }
    )
    return (await service.set_binary_interaction_parameter(payload)).model_dump()


def _error_result(code: str, message: str) -> types.CallToolResult:
    payload = SessionErrorModel(code=code, message=message).model_dump()
    return types.CallToolResult(
        content=[types.TextContent(type="text", text=message)],
        structuredContent=payload,
        isError=True,
    )
