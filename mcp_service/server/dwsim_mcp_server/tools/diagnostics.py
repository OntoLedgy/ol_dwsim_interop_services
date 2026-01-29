"""Diagnostics MCP tools."""

from __future__ import annotations

from typing import Any, Dict, Optional

from mcp import types
from pydantic import BaseModel, ConfigDict, Field, ValidationError

from dwsim_mcp_server.models.errors.session_error import SessionError as SessionErrorModel

from dwsim_mcp_server.models.diagnostics import ServerDiagnostics, SessionDiagnostics
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


DIAGNOSTICS_OUTPUT_SCHEMA: Dict[str, Any] = {
    "oneOf": [
        ServerDiagnostics.model_json_schema(),
        SessionDiagnostics.model_json_schema(),
    ]
}


def build_diagnostics_tools() -> list[types.Tool]:
    """Return MCP tool definitions for diagnostics operations."""
    return [
        types.Tool(
            name="get_diagnostics",
            title="Get Diagnostics",
            description=(
                "Retrieve diagnostics for the server or a specific session. "
                "Provide session_id to get session diagnostics; omit to get server diagnostics."
            ),
            inputSchema=GetDiagnosticsInput.model_json_schema(),
            outputSchema=DIAGNOSTICS_OUTPUT_SCHEMA,
        )
    ]


async def handle_diagnostics_tool(
    tool_name: str, arguments: Dict[str, Any], dependencies: Any
):
    """Dispatch diagnostics tools by name."""
    logger = get_logger(__name__)
    service = getattr(dependencies, "diagnostics_service", None)

    if service is None:
        return _error_result(
            code="SERVICE_UNAVAILABLE",
            message="Diagnostics service is not configured.",
        )

    try:
        if tool_name == "get_diagnostics":
            payload = GetDiagnosticsInput.model_validate(arguments)
            if payload.session_id:
                result = service.get_session_diagnostics(payload.session_id)
            else:
                result = service.get_server_diagnostics()
            return result.model_dump()

        return types.CallToolResult(
            content=[types.TextContent(type="text", text=f"Unknown tool: {tool_name}")],
            structuredContent={"code": "UNKNOWN_TOOL", "message": f"Unknown tool: {tool_name}"},
            isError=True,
        )
    except ValidationError as exc:
        return _error_result(code="VALIDATION_ERROR", message=str(exc))
    except SessionNotFoundError as exc:
        return _error_result(
            code="SESSION_NOT_FOUND",
            message=str(exc),
            session_id=arguments.get("session_id") if isinstance(arguments, dict) else None,
        )
    except Exception as exc:
        logger.exception("diagnostics_tool_failed", extra={"tool": tool_name})
        return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


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