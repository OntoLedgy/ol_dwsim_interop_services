"""Thermodynamic analysis MCP tools."""

from __future__ import annotations

from typing import Any, Dict

from mcp import types
from pydantic import ValidationError

from dwsim_mcp_server.models.errors.resource_limit_error import ResourceLimitError
from dwsim_mcp_server.models.errors.simulation_error import SimulationError as SimulationErrorModel
from dwsim_mcp_server.models.mcp_inputs import FlashPHInputs, FlashPSInputs, FlashTPInputs
from dwsim_mcp_server.models.responses import FlashResults

from dwsim_mcp_server.ipc.exceptions import InteropError, SessionError
from dwsim_mcp_server.limits.resource_limit_guard import ResourceLimitViolation
from dwsim_mcp_server.observability import get_logger


def build_analysis_tools() -> list[types.Tool]:
    """Return MCP tool definitions for thermodynamic analysis."""
    return [
        types.Tool(
            name="flash_tp",
            title="Flash TP",
            description=(
                "Perform a temperature-pressure flash calculation for a mixture. "
                "Requires a session with a property package configured. Provide compound names and mole "
                "fractions (must sum to 1.0), plus temperature and pressure measurements with units."
            ),
            inputSchema=FlashTPInputs.model_json_schema(),
            outputSchema=FlashResults.model_json_schema(),
        ),
        types.Tool(
            name="flash_ph",
            title="Flash PH",
            description=(
                "Perform a pressure-enthalpy flash calculation for a mixture. "
                "Requires a session with a property package configured. Provide compound names and mole "
                "fractions (must sum to 1.0), plus pressure and molar enthalpy measurements with units."
            ),
            inputSchema=FlashPHInputs.model_json_schema(),
            outputSchema=FlashResults.model_json_schema(),
        ),
        types.Tool(
            name="flash_ps",
            title="Flash PS",
            description=(
                "Perform a pressure-entropy flash calculation for a mixture. "
                "Requires a session with a property package configured. Provide compound names and mole "
                "fractions (must sum to 1.0), plus pressure and molar entropy measurements with units."
            ),
            inputSchema=FlashPSInputs.model_json_schema(),
            outputSchema=FlashResults.model_json_schema(),
        ),
    ]


async def handle_analysis_tool(tool_name: str, arguments: Dict[str, Any], dependencies: Any):
    """Dispatch thermodynamic analysis tools by name."""
    logger = get_logger(__name__)
    service = getattr(dependencies, "thermodynamics_service", None)

    if service is None:
        return _error_result(
            code="SERVICE_UNAVAILABLE",
            message="Thermodynamics service is not configured.",
        )

    try:
        if tool_name == "flash_tp":
            payload = FlashTPInputs.model_validate(arguments)
            result = await service.flash_tp(payload)
            return result.model_dump()

        if tool_name == "flash_ph":
            payload = FlashPHInputs.model_validate(arguments)
            result = await service.flash_ph(payload)
            return result.model_dump()

        if tool_name == "flash_ps":
            payload = FlashPSInputs.model_validate(arguments)
            result = await service.flash_ps(payload)
            return result.model_dump()

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
        logger.exception("analysis_tool_failed", extra={"tool": tool_name})
        return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


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
