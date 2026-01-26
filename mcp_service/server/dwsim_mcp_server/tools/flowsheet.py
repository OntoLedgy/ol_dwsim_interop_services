"""Flowsheet-building MCP tools."""

from __future__ import annotations

from typing import Any, Dict

from mcp import types
from pydantic import ValidationError

from models.errors.session_error import SessionError as SessionErrorModel
from models.mcp_inputs import (
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


def build_flowsheet_tools() -> list[types.Tool]:
    """Return MCP tool definitions for flowsheet building."""
    return [
        types.Tool(
            name="add_compound",
            title="Add Compound",
            description=(
                "Add a compound from the DWSIM databank to the session (idempotent). "
                "Supports aliases like CO2, H2O, and isobutane; common inputs include "
                "Methane, n-Butane, Water, and Carbon Dioxide."
            ),
            inputSchema=AddCompoundInput.model_json_schema(),
            outputSchema=AddCompoundOutput.model_json_schema(),
        ),
        types.Tool(
            name="set_property_package",
            title="Set Property Package",
            description="Set a thermodynamic property package for the session. Common packages: Peng-Robinson (general hydrocarbons), SRK (gases), NRTL (polar/non-ideal), UNIQUAC (activity coefficients).",
            inputSchema=SetPropertyPackageInput.model_json_schema(),
            outputSchema=SetPropertyPackageOutput.model_json_schema(),
        ),
        types.Tool(
            name="add_stream",
            title="Add Stream",
            description=(
                "Create a material stream. For FEED streams: set is_source=true with T(K), "
                "P(Pa), molar_flow(mol/s), composition; for OUTLET streams: set is_source=false "
                "and omit composition to auto-fill equal fractions (DWSIM computes real values). "
                "Always flash feed streams after creation."
            ),
            inputSchema=AddStreamInput.model_json_schema(),
            outputSchema=AddStreamOutput.model_json_schema(),
        ),
        types.Tool(
            name="add_unit",
            title="Add Unit Operation",
            description="Create a unit operation. Currently supported: unit_type='separator' (three-phase separator). Parameters: CalculationMode='Legacy', PressureCalculation='Average', DimensionRatio=3.0, ResidenceTime=5.0.",
            inputSchema=AddUnitInput.model_json_schema(),
            outputSchema=AddUnitOutput.model_json_schema(),
        ),
        types.Tool(
            name="connect",
            title="Connect Objects",
            description="Connect a stream to a unit operation port. For separator: port_name can be 'Inlet', 'VaporOutlet', 'LiquidOutlet1', or 'LiquidOutlet2'. source_id is the stream ID, target_id is the unit ID.",
            inputSchema=ConnectInput.model_json_schema(),
            outputSchema=ConnectOutput.model_json_schema(),
        ),
        types.Tool(
            name="list_objects",
            title="List Flowsheet Objects",
            description="List all streams, units, and connections in the current session. Use to verify flowsheet topology before running simulation.",
            inputSchema=ListObjectsInput.model_json_schema(),
            outputSchema=ListObjectsOutput.model_json_schema(),
        ),
        types.Tool(
            name="set_object_parameter",
            title="Set Object Parameter",
            description="Update a parameter on a flowsheet object.",
            inputSchema=SetObjectParameterInput.model_json_schema(),
            outputSchema=SetObjectParameterOutput.model_json_schema(),
        ),
        types.Tool(
            name="delete_object",
            title="Delete Object",
            description="Delete a flowsheet object and orphaned connections safely.",
            inputSchema=DeleteObjectInput.model_json_schema(),
            outputSchema=DeleteObjectOutput.model_json_schema(),
        ),
        types.Tool(
            name="flash_stream",
            title="Flash Stream",
            description="Perform flash calculation on a feed stream to compute phase equilibrium. MUST be called on feed streams (is_source=true) after creation and before running simulation.",
            inputSchema=FlashStreamInput.model_json_schema(),
            outputSchema=FlashStreamOutput.model_json_schema(),
        ),
        types.Tool(
            name="set_binary_interaction_parameter",
            title="Set Binary Interaction Parameter",
            description="Set a binary interaction parameter (BIP) for a pair of compounds. CRITICAL for accurate phase equilibrium. Typical values: hydrocarbon pairs ~0.01-0.05, water-hydrocarbon ~0.5. Set AFTER property package, BEFORE adding streams.",
            inputSchema=SetBinaryInteractionParameterInput.model_json_schema(),
            outputSchema=SetBinaryInteractionParameterOutput.model_json_schema(),
        ),
    ]


async def handle_flowsheet_tool(tool_name: str, arguments: Dict[str, Any], dependencies: Any):
    """Dispatch flowsheet tools by name."""
    logger = get_logger(__name__)
    service = getattr(dependencies, "flowsheet_service", None)

    if service is None:
        return _error_result(
            code="SERVICE_UNAVAILABLE",
            message="Flowsheet service is not configured.",
        )

    try:
        if tool_name == "add_compound":
            payload = AddCompoundInput.model_validate(arguments)
            result = await service.add_compound(payload)
            return result.model_dump()

        if tool_name == "set_property_package":
            payload = SetPropertyPackageInput.model_validate(arguments)
            result = await service.set_property_package(payload)
            return result.model_dump()

        if tool_name == "add_stream":
            payload = AddStreamInput.model_validate(arguments)
            result = await service.add_stream(payload)
            return result.model_dump()

        if tool_name == "add_unit":
            payload = AddUnitInput.model_validate(arguments)
            result = await service.add_unit(payload)
            return result.model_dump()

        if tool_name == "connect":
            payload = ConnectInput.model_validate(arguments)
            result = await service.connect(payload)
            return result.model_dump()

        if tool_name == "list_objects":
            payload = ListObjectsInput.model_validate(arguments)
            result = await service.list_objects(payload)
            return result.model_dump()

        if tool_name == "set_object_parameter":
            payload = SetObjectParameterInput.model_validate(arguments)
            result = await service.set_object_parameter(payload)
            return result.model_dump()

        if tool_name == "delete_object":
            payload = DeleteObjectInput.model_validate(arguments)
            result = await service.delete_object(payload)
            return result.model_dump()

        if tool_name == "flash_stream":
            payload = FlashStreamInput.model_validate(arguments)
            result = await service.flash_stream(payload)
            return result.model_dump()

        if tool_name == "set_binary_interaction_parameter":
            payload = SetBinaryInteractionParameterInput.model_validate(arguments)
            result = await service.set_binary_interaction_parameter(payload)
            return result.model_dump()

        return types.CallToolResult(
            content=[types.TextContent(type="text", text=f"Unknown tool: {tool_name}")],
            structuredContent={"code": "UNKNOWN_TOOL", "message": f"Unknown tool: {tool_name}"},
            isError=True,
        )
    except ValidationError as exc:
        return _error_result(
            code="VALIDATION_ERROR",
            message=str(exc),
        )
    except Exception as exc:
        logger.exception("flowsheet_tool_failed", extra={"tool": tool_name})
        return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


def _error_result(code: str, message: str) -> types.CallToolResult:
    payload = SessionErrorModel(code=code, message=message).model_dump()
    return types.CallToolResult(
        content=[types.TextContent(type="text", text=message)],
        structuredContent=payload,
        isError=True,
    )
