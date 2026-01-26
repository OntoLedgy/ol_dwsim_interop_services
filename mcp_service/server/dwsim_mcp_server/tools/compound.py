"""Compound validation MCP tools."""

from __future__ import annotations

from typing import Any, Dict

from mcp import types
from pydantic import ValidationError

from models.errors.session_error import SessionError as SessionErrorModel
from models.mcp_inputs.compound_validation import (
    ListCompoundsInput,
    ListCompoundsOutput,
    ValidateCompoundsInput,
    ValidateCompoundsOutput,
)

from dwsim_mcp_server.observability import get_logger


def build_compound_tools() -> list[types.Tool]:
    """Return MCP tool definitions for compound operations."""
    return [
        types.Tool(
            name="validate_compounds",
            title="Validate Compounds",
            description=(
                "Validate compound names against the DWSIM databank before adding them. "
                "Returns canonical names, alias resolution (CO2, H2O, isobutane), and "
                "fuzzy-matched suggestions for typos (e.g., 'methne' → Methane)."
            ),
            inputSchema=ValidateCompoundsInput.model_json_schema(),
            outputSchema=ValidateCompoundsOutput.model_json_schema(),
        ),
        types.Tool(
            name="list_available_compounds",
            title="List Available Compounds",
            description=(
                "List compounds available in the DWSIM databank to discover valid names. "
                "Use pattern for case-insensitive search (e.g., 'butan') and category for "
                "type filtering (e.g., Hydrocarbon); supports limit/offset pagination."
            ),
            inputSchema=ListCompoundsInput.model_json_schema(),
            outputSchema=ListCompoundsOutput.model_json_schema(),
        ),
    ]


async def handle_compound_tool(
    tool_name: str, arguments: Dict[str, Any], dependencies: Any
):
    """Dispatch compound tools by name."""
    logger = get_logger(__name__)
    service = getattr(dependencies, "flowsheet_service", None)

    if service is None:
        return _error_result(
            code="SERVICE_UNAVAILABLE",
            message="Flowsheet service is not configured.",
        )

    try:
        if tool_name == "validate_compounds":
            payload = ValidateCompoundsInput.model_validate(arguments)
            result = await service.validate_compounds(payload)
            return result.model_dump()

        if tool_name == "list_available_compounds":
            payload = ListCompoundsInput.model_validate(arguments)
            result = await service.list_compounds(payload)
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
        logger.exception("compound_tool_failed", extra={"tool": tool_name})
        return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


def _error_result(code: str, message: str) -> types.CallToolResult:
    payload = SessionErrorModel(code=code, message=message).model_dump()
    return types.CallToolResult(
        content=[types.TextContent(type="text", text=message)],
        structuredContent=payload,
        isError=True,
    )
