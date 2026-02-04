"""Diagnostics MCP tools."""

from typing import Any, Awaitable, Callable, Dict, Optional

from mcp import types
from fastmcp import Context
from pydantic import BaseModel, ConfigDict, Field, ValidationError

from dwsim_mcp_server.models.errors.session_error import SessionError as SessionErrorModel

from dwsim_mcp_server.observability import get_logger
from dwsim_mcp_server.service.diagnostics_service import SessionNotFoundError


class GetDiagnosticsInput(BaseModel):
    """Input payload for diagnostics queries."""

    model_config = ConfigDict(extra="forbid")

    session_id: Optional[str] = Field(
        default=None,
        min_length=1,
        description=(
            "Session identifier to fetch diagnostics for. If omitted, server diagnostics are returned."
        ),
    )


class _ServiceUnavailable(Exception):
    pass


def register_diagnostics_tools(mcp) -> None:
    """Register diagnostics tools with FastMCP."""

    @mcp.tool(
        description=(
            "Retrieve diagnostics for the server or a specific session. "
            "Provide session_id to get session diagnostics; omit to get server diagnostics."
        )
    )
    async def get_diagnostics(session_id: Optional[str] = None, ctx: Optional[Context] = None):
        return await _execute_tool(
            "get_diagnostics",
            lambda: _get_diagnostics(ctx, session_id=session_id),
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
    if isinstance(exc, SessionNotFoundError):
        return _error_result(code="SESSION_NOT_FOUND", message=str(exc))
    logger.exception("diagnostics_tool_failed", extra={"tool": tool_name})
    return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


def _get_service(ctx: Context | None):
    service = ctx.request_context.lifespan_context.diagnostics_service
    if service is None:
        raise _ServiceUnavailable("Diagnostics service is not configured.")
    return service


async def _get_diagnostics(ctx: Context | None, *, session_id: Optional[str]) -> Dict[str, Any]:
    payload = GetDiagnosticsInput.model_validate({"session_id": session_id})
    service = _get_service(ctx)
    if payload.session_id:
        return service.get_session_diagnostics(payload.session_id).model_dump()
    return service.get_server_diagnostics().model_dump()


def _error_result(
    code: str,
    message: str,
    session_id: Optional[str] = None,
    details: Optional[Dict[str, Any]] = None,
) -> types.CallToolResult:
    payload = SessionErrorModel(
        code=code,
        message=message,
        session_id=session_id,
        details=details,
    ).model_dump()
    return types.CallToolResult(
        content=[types.TextContent(type="text", text=message)],
        structuredContent=payload,
        isError=True,
    )
