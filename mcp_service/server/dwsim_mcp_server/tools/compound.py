"""Compound validation MCP tools."""

from __future__ import annotations

from typing import Any, Awaitable, Callable, Dict

from mcp import types
from mcp.server.fastmcp.server import Context
from pydantic import ValidationError

from dwsim_mcp_server.models.errors.session_error import SessionError as SessionErrorModel
from dwsim_mcp_server.models.mcp_inputs.compound_validation import (
    ListCompoundsInput,
    ListCompoundsOutput,
    ValidateCompoundsInput,
    ValidateCompoundsOutput,
)

from dwsim_mcp_server.observability import get_logger
from dwsim_mcp_server.tools.legacy import LegacyContext


class _ServiceUnavailable(Exception):
    pass


VALIDATE_COMPOUNDS_DESCRIPTION = (
    "Validate compound names against the DWSIM databank before adding them. "
    "Returns canonical names, alias resolution (CO2, H2O, isobutane), and "
    "fuzzy-matched suggestions for typos (e.g., 'methne' → Methane)."
)
LIST_AVAILABLE_DESCRIPTION = (
    "List compounds available in the DWSIM databank to discover valid names. "
    "Use pattern for case-insensitive search (e.g., 'butan') and category for "
    "type filtering (e.g., Hydrocarbon); supports limit/offset pagination."
)


def register_compound_tools(mcp) -> None:
    """Register compound tools with FastMCP."""

    @mcp.tool(
        description=(
            "Validate compound names against the DWSIM databank before adding them. "
            "Returns canonical names, alias resolution (CO2, H2O, isobutane), and "
            "fuzzy-matched suggestions for typos (e.g., 'methne' → Methane)."
        )
    )
    async def validate_compounds(
        session_id: str, compound_names: list[str], ctx: Context | None = None
    ):
        return await _execute_tool(
            "validate_compounds",
            lambda: _validate_compounds(
                ctx, session_id=session_id, compound_names=compound_names
            ),
        )

    @mcp.tool(
        description=(
            "List compounds available in the DWSIM databank to discover valid names. "
            "Use pattern for case-insensitive search (e.g., 'butan') and category for "
            "type filtering (e.g., Hydrocarbon); supports limit/offset pagination."
        )
    )
    async def list_available_compounds(session_id: str, pattern: str | None = None, category: str | None = None, limit: int | None = None, offset: int | None = None, ctx: Context | None = None):
        return await _execute_tool(
            "list_available_compounds",
            lambda: _list_compounds(
                ctx,
                session_id=session_id,
                pattern=pattern,
                category=category,
                limit=limit,
                offset=offset,
            ),
        )


def build_compound_tools() -> list[types.Tool]:
    return [
        _tool(
            "validate_compounds",
            VALIDATE_COMPOUNDS_DESCRIPTION,
            ValidateCompoundsInput,
        ),
        _tool(
            "list_available_compounds",
            LIST_AVAILABLE_DESCRIPTION,
            ListCompoundsInput,
        ),
    ]


async def handle_compound_tool(
    tool_name: str, arguments: Dict[str, Any], dependencies
) -> Dict[str, Any] | types.CallToolResult:
    ctx = LegacyContext(dependencies)
    handlers = {
        "validate_compounds": lambda: _validate_compounds(
            ctx,
            session_id=arguments.get("session_id"),
            compound_names=arguments.get("compound_names"),
        ),
        "list_available_compounds": lambda: _list_compounds(
            ctx,
            session_id=arguments.get("session_id"),
            pattern=arguments.get("pattern"),
            category=arguments.get("category"),
            limit=arguments.get("limit"),
            offset=arguments.get("offset"),
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
    logger.exception("compound_tool_failed", extra={"tool": tool_name})
    return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


def _get_service(ctx: Context | None):
    service = ctx.request_context.lifespan_context.flowsheet_service
    if service is None:
        raise _ServiceUnavailable("Flowsheet service is not configured.")
    return service


async def _validate_compounds(
    ctx: Context | None, *, session_id: str, compound_names: list[str]
) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = ValidateCompoundsInput.model_validate(
        {"session_id": session_id, "compound_names": compound_names}
    )
    return (await service.validate_compounds(payload)).model_dump()


async def _list_compounds(
    ctx: Context | None,
    *,
    session_id: str,
    pattern: str | None,
    category: str | None,
    limit: int | None,
    offset: int | None,
) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = ListCompoundsInput.model_validate(
        {
            "session_id": session_id,
            "pattern": pattern,
            "category": category,
            "limit": limit,
            "offset": offset,
        }
    )
    return (await service.list_compounds(payload)).model_dump()


def _error_result(code: str, message: str) -> types.CallToolResult:
    payload = SessionErrorModel(code=code, message=message).model_dump()
    return types.CallToolResult(
        content=[types.TextContent(type="text", text=message)],
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


