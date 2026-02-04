"""Session management tools for the DWSIM MCP server."""

from typing import Any, Awaitable, Callable, Dict, Optional

from mcp import types
from fastmcp import Context
from pydantic import ValidationError

from dwsim_mcp_server.models.errors.resource_limit_error import ResourceLimitError
from dwsim_mcp_server.models.errors.session_error import SessionError as SessionErrorModel
from dwsim_mcp_server.models.requests import (
    CloseSessionRequest,
    CreateSessionRequest,
    LoadCaseRequest,
    SaveCaseRequest,
)
from dwsim_mcp_server.models.responses import CloseSessionResponse, LoadCaseResponse, SaveCaseResponse

from dwsim_mcp_server.ipc.exceptions import InteropError, SessionError
from dwsim_mcp_server.limits.resource_limit_guard import ResourceLimitViolation
from dwsim_mcp_server.observability import get_logger
from dwsim_mcp_server.tools.legacy import LegacyContext
from dwsim_mcp_server.utils import resolve_case_path

CREATE_SESSION_OUTPUT_SCHEMA: Dict[str, Any] = {
    "type": "object",
    "properties": {
        "session_id": {
            "type": "string",
            "description": "Unique session identifier",
        }
    },
    "required": ["session_id"],
}

CREATE_SESSION_DESCRIPTION = (
    "Create a new DWSIM session for flowsheet work. This MUST be called first before any other DWSIM tools. Returns a session_id used in all subsequent calls."
)
CLOSE_SESSION_DESCRIPTION = (
    "Close an existing DWSIM session and release resources. Always call this when done to free memory."
)
SAVE_CASE_DESCRIPTION = "Save the current flowsheet case to a DWSIM file (.dwxmz)."
LOAD_CASE_DESCRIPTION = "Load a flowsheet case from a DWSIM file (.dwxmz) into a session."


def register_session_tools(mcp) -> None:
    """Register session tools with FastMCP."""

    @mcp.tool(
        description=CREATE_SESSION_DESCRIPTION
    )
    async def create_session(
        name: Optional[str] = None,
        timeout: Optional[int] = None,
        ctx: Optional[Context] = None,
    ):
        return await _execute_tool(
            "create_session",
            lambda: _create_session(ctx, name=name, timeout=timeout),
        )

    @mcp.tool(
        description=CLOSE_SESSION_DESCRIPTION
    )
    async def close_session(session_id: str, ctx: Context | None = None):
        return await _execute_tool(
            "close_session",
            lambda: _close_session(ctx, session_id=session_id),
        )

    @mcp.tool(description=SAVE_CASE_DESCRIPTION)
    async def save_case(session_id: str, file_path: str, ctx: Context | None = None):
        return await _execute_tool(
            "save_case",
            lambda: _save_case(ctx, session_id=session_id, file_path=file_path),
        )

    @mcp.tool(description=LOAD_CASE_DESCRIPTION)
    async def load_case(session_id: str, file_path: str, ctx: Context | None = None):
        return await _execute_tool(
            "load_case",
            lambda: _load_case(ctx, session_id=session_id, file_path=file_path),
        )


def build_session_tools() -> list[types.Tool]:
    return [
        _tool("create_session", CREATE_SESSION_DESCRIPTION, CreateSessionRequest),
        _tool("close_session", CLOSE_SESSION_DESCRIPTION, CloseSessionRequest),
        _tool("save_case", SAVE_CASE_DESCRIPTION, SaveCaseRequest),
        _tool("load_case", LOAD_CASE_DESCRIPTION, LoadCaseRequest),
    ]


async def handle_session_tool(
    tool_name: str, arguments: Dict[str, Any], dependencies
) -> Dict[str, Any] | types.CallToolResult:
    ctx = LegacyContext(dependencies)
    handlers = {
        "create_session": lambda: _create_session(
            ctx, name=arguments.get("name"), timeout=arguments.get("timeout")
        ),
        "close_session": lambda: _close_session(
            ctx, session_id=arguments.get("session_id")
        ),
        "save_case": lambda: _save_case(
            ctx,
            session_id=arguments.get("session_id"),
            file_path=arguments.get("file_path"),
        ),
        "load_case": lambda: _load_case(
            ctx,
            session_id=arguments.get("session_id"),
            file_path=arguments.get("file_path"),
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
    if isinstance(exc, ValidationError):
        return _error_result(code="VALIDATION_ERROR", message=str(exc))
    if isinstance(exc, ResourceLimitViolation):
        return _resource_limit_error(exc.error)
    if isinstance(exc, SessionError):
        return _error_result(code="SESSION_ERROR", message=str(exc))
    if isinstance(exc, InteropError):
        return _error_result(code="INTEROP_ERROR", message=str(exc))
    if isinstance(exc, ValueError):
        return _error_result(code="INVALID_PATH", message=str(exc))
    logger.exception("session_tool_failed", extra={"tool": tool_name})
    return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


def _get_app_context(ctx: Context | None):
    return ctx.request_context.lifespan_context


async def _create_session(
    ctx: Context | None, *, name: Optional[str], timeout: Optional[int]
) -> Dict[str, Any]:
    app = _get_app_context(ctx)
    payload = CreateSessionRequest.model_validate({"name": name, "timeout": timeout})
    session_id = await app.session_client.create_session(
        flowsheet_name=payload.name,
        timeout_seconds=payload.timeout,
    )
    get_logger(__name__).info("session_created", session_id=session_id, name=payload.name)
    return {"session_id": session_id}


async def _close_session(ctx: Context | None, *, session_id: str) -> Dict[str, Any]:
    app = _get_app_context(ctx)
    payload = CloseSessionRequest.model_validate({"session_id": session_id})
    result = await app.session_client.close_session(payload.session_id)
    get_logger(__name__).info("session_closed", session_id=payload.session_id, success=result)
    return CloseSessionResponse(success=result).model_dump()


async def _save_case(ctx: Context | None, *, session_id: str, file_path: str) -> Dict[str, Any]:
    app = _get_app_context(ctx)
    payload = SaveCaseRequest.model_validate(
        {"session_id": session_id, "file_path": file_path}
    )
    resolved_path = resolve_case_path(payload.file_path, app.settings.case_storage_roots)
    result = await app.session_client.save_case(payload.session_id, str(resolved_path))
    get_logger(__name__).info(
        "case_saved",
        session_id=payload.session_id,
        path=str(resolved_path),
        success=result,
    )
    return SaveCaseResponse(success=result).model_dump()


async def _load_case(ctx: Context | None, *, session_id: str, file_path: str) -> Dict[str, Any]:
    app = _get_app_context(ctx)
    payload = LoadCaseRequest.model_validate(
        {"session_id": session_id, "file_path": file_path}
    )
    resolved_path = resolve_case_path(payload.file_path, app.settings.case_storage_roots)
    result = await app.session_client.load_case(payload.session_id, str(resolved_path))
    get_logger(__name__).info(
        "case_loaded",
        session_id=payload.session_id,
        path=str(resolved_path),
        success=result,
    )
    if not result:
        raise SessionError("Case load did not report success.")
    return LoadCaseResponse(session_id=payload.session_id).model_dump()


def _error_result(code: str, message: str) -> types.CallToolResult:
    payload = SessionErrorModel(code=code, message=message).model_dump()
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
