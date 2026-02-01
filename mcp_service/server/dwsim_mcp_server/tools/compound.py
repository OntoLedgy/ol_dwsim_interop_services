"""Compound validation MCP tools."""

from __future__ import annotations

from typing import Any, Awaitable, Callable, Dict

from mcp import types
from pydantic import ValidationError

from dwsim_mcp_server.models.errors.session_error import SessionError as SessionErrorModel
from dwsim_mcp_server.models.mcp_inputs.compound_validation import (
    ListCompoundsInput,
    ListCompoundsOutput,
    ValidateCompoundsInput,
    ValidateCompoundsOutput,
)

from dwsim_mcp_server.observability import get_logger


class _ServiceUnavailable(Exception):
    pass


def register_compound_tools(mcp) -> None:
    """Register compound tools with FastMCP."""

    @mcp.tool(
        description=(
            "Validate compound names against the DWSIM databank before adding them. "
            "Returns canonical names, alias resolution (CO2, H2O, isobutane), and "
            "fuzzy-matched suggestions for typos (e.g., 'methne' -> Methane)."
        )
    )
    async def validate_compounds(session_id: str, compounds: list[str], ctx: Any = None):
        return await _execute_tool(
            "validate_compounds",
            lambda: _validate_compounds(ctx, session_id=session_id, compounds=compounds),
        )

    @mcp.tool(
        description=(
            "List compounds available in the DWSIM databank to discover valid names. "
            "Use pattern for case-insensitive search (e.g., 'butan') and category for "
            "type filtering (e.g., Hydrocarbon); supports limit/offset pagination."
        )
    )
    async def list_available_compounds(session_id: str, pattern: str | None = None, category: str | None = None, limit: int | None = None, offset: int | None = None, ctx: Any = None):
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


def _get_service(ctx: Any):
    service = ctx.request_context.lifespan_context.flowsheet_service
    if service is None:
        raise _ServiceUnavailable("Flowsheet service is not configured.")
    return service


async def _validate_compounds(
    ctx: Any, *, session_id: str, compounds: list[str]
) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = ValidateCompoundsInput.model_validate(
        {"session_id": session_id, "compounds": compounds}
    )
    return (await service.validate_compounds(payload)).model_dump()


async def _list_compounds(
    ctx: Any,
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
