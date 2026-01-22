"""Session management tools for the DWSIM MCP server."""

from __future__ import annotations

from typing import Any, Dict, Iterable

from mcp import types
from pydantic import ValidationError

from models.errors.resource_limit_error import ResourceLimitError
from models.errors.session_error import SessionError as SessionErrorModel
from models.requests import (
    CloseSessionRequest,
    CreateSessionRequest,
    LoadCaseRequest,
    SaveCaseRequest,
)
from models.responses import CloseSessionResponse, LoadCaseResponse, SaveCaseResponse

from dwsim_mcp_server.ipc.exceptions import InteropError, SessionError
from dwsim_mcp_server.limits.resource_limit_guard import ResourceLimitViolation
from dwsim_mcp_server.observability import get_logger
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


def build_session_tools() -> list[types.Tool]:
    """Return MCP tool definitions for session management."""
    return [
        types.Tool(
            name="create_session",
            title="Create Session",
            description="Create a new DWSIM session for flowsheet work. This MUST be called first before any other DWSIM tools. Returns a session_id used in all subsequent calls.",
            inputSchema=CreateSessionRequest.model_json_schema(),
            outputSchema=CREATE_SESSION_OUTPUT_SCHEMA,
        ),
        types.Tool(
            name="close_session",
            title="Close Session",
            description="Close an existing DWSIM session and release resources. Always call this when done to free memory.",
            inputSchema=CloseSessionRequest.model_json_schema(),
            outputSchema=CloseSessionResponse.model_json_schema(),
        ),
        types.Tool(
            name="save_case",
            title="Save Case",
            description="Save the current flowsheet case to a DWSIM file (.dwxmz).",
            inputSchema=SaveCaseRequest.model_json_schema(),
            outputSchema=SaveCaseResponse.model_json_schema(),
        ),
        types.Tool(
            name="load_case",
            title="Load Case",
            description="Load a flowsheet case from a DWSIM file (.dwxmz) into a session.",
            inputSchema=LoadCaseRequest.model_json_schema(),
            outputSchema=LoadCaseResponse.model_json_schema(),
        ),
    ]


async def handle_session_tool(tool_name: str, arguments: Dict[str, Any], dependencies: Any):
    """Dispatch session tools by name."""
    logger = get_logger(__name__)
    session_client = dependencies.session_client
    settings = dependencies.settings

    try:
        if tool_name == "create_session":
            payload = CreateSessionRequest.model_validate(arguments)
            session_id = await session_client.create_session(
                flowsheet_name=payload.name,
                timeout_seconds=payload.timeout,
            )
            logger.info("session_created", session_id=session_id, name=payload.name)
            return {"session_id": session_id}

        if tool_name == "close_session":
            payload = CloseSessionRequest.model_validate(arguments)
            result = await session_client.close_session(payload.session_id)
            logger.info("session_closed", session_id=payload.session_id, success=result)
            return CloseSessionResponse(success=result).model_dump()

        if tool_name == "save_case":
            payload = SaveCaseRequest.model_validate(arguments)
            resolved_path = resolve_case_path(payload.file_path, settings.case_storage_roots)
            result = await session_client.save_case(payload.session_id, str(resolved_path))
            logger.info("case_saved", session_id=payload.session_id, path=str(resolved_path), success=result)
            return SaveCaseResponse(success=result).model_dump()

        if tool_name == "load_case":
            payload = LoadCaseRequest.model_validate(arguments)
            resolved_path = resolve_case_path(payload.file_path, settings.case_storage_roots)
            result = await session_client.load_case(payload.session_id, str(resolved_path))
            logger.info("case_loaded", session_id=payload.session_id, path=str(resolved_path), success=result)
            if not result:
                raise SessionError("Case load did not report success.")
            return LoadCaseResponse(session_id=payload.session_id).model_dump()

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
    except ResourceLimitViolation as exc:
        return _resource_limit_error(exc.error)
    except SessionError as exc:
        return _error_result(code="SESSION_ERROR", message=str(exc))
    except InteropError as exc:
        return _error_result(code="INTEROP_ERROR", message=str(exc))
    except ValueError as exc:
        return _error_result(code="INVALID_PATH", message=str(exc))
    except Exception as exc:
        logger.exception("session_tool_failed", extra={"tool": tool_name})
        return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


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
