"""Export MCP tools."""

from typing import Any, Awaitable, Callable, Dict

from mcp import types
from fastmcp import Context
from pydantic import ValidationError

from dwsim_mcp_server.models.errors.session_error import SessionError as SessionErrorModel
from dwsim_mcp_server.models.mcp_inputs.export_inputs import (
    ExportCsvInput,
    ExportJsonInput,
    GenerateReportInput,
    SaveCaseInput,
)

from dwsim_mcp_server.observability import get_logger
from dwsim_mcp_server.tools.legacy import LegacyContext


class _ServiceUnavailable(Exception):
    pass


EXPORT_CSV_DESCRIPTION = (
    "Export flowsheet streams/units to a CSV for spreadsheets or debugging. "
    "Provide file_path ending in .csv and optionally object_ids to filter "
    "(e.g., ['Feed', 'Separator']). Returns the saved path and row count."
)
EXPORT_JSON_DESCRIPTION = (
    "Export flowsheet state to JSON for programmatic inspection or caching. "
    "Use format='summary' for a top-level overview or format='full' for complete data."
)
GENERATE_REPORT_DESCRIPTION = (
    "Generate a Markdown report file for sharing or QA notes. "
    "Provide file_path ending in .md and choose template='summary' or 'detailed'."
)
SAVE_CASE_DESCRIPTION = (
    "Save the flowsheet to a DWSIM case file for later reload. "
    "Provide file_path ending in .dwxmz (compressed) or .dwxml (uncompressed)."
)


def register_export_tools(mcp) -> None:
    """Register export tools with FastMCP."""

    @mcp.tool(description=EXPORT_CSV_DESCRIPTION)
    async def export_csv(
        session_id: str, file_path: str, object_ids: list[str] | None = None, ctx: Context | None = None
    ):
        return await _execute_tool(
            "export_csv",
            lambda: _export_csv(
                ctx,
                session_id=session_id,
                file_path=file_path,
                object_ids=object_ids,
            ),
        )

    @mcp.tool(description=EXPORT_JSON_DESCRIPTION)
    async def export_json(session_id: str, format: str = "summary", ctx: Context | None = None):
        return await _execute_tool(
            "export_json",
            lambda: _export_json(ctx, session_id=session_id, format=format),
        )

    @mcp.tool(description=GENERATE_REPORT_DESCRIPTION)
    async def generate_report(session_id: str, file_path: str, template: str = "summary", ctx: Context | None = None):
        return await _execute_tool(
            "generate_report",
            lambda: _generate_report(
                ctx,
                session_id=session_id,
                file_path=file_path,
                template=template,
            ),
        )

    @mcp.tool(description=SAVE_CASE_DESCRIPTION)
    async def save_case(session_id: str, file_path: str, ctx: Context | None = None):
        return await _execute_tool(
            "save_case",
            lambda: _save_case(ctx, session_id=session_id, file_path=file_path),
        )


def build_export_tools() -> list[types.Tool]:
    return [
        _tool("export_csv", EXPORT_CSV_DESCRIPTION, ExportCsvInput),
        _tool("export_json", EXPORT_JSON_DESCRIPTION, ExportJsonInput),
        _tool("generate_report", GENERATE_REPORT_DESCRIPTION, GenerateReportInput),
        _tool("save_case", SAVE_CASE_DESCRIPTION, SaveCaseInput),
    ]


async def handle_export_tool(
    tool_name: str, arguments: Dict[str, Any], dependencies
) -> Dict[str, Any] | types.CallToolResult:
    ctx = LegacyContext(dependencies)
    handlers = {
        "export_csv": lambda: _export_csv(
            ctx,
            session_id=arguments.get("session_id"),
            file_path=arguments.get("file_path"),
            object_ids=arguments.get("object_ids"),
        ),
        "export_json": lambda: _export_json(
            ctx,
            session_id=arguments.get("session_id"),
            format=arguments.get("format", "summary"),
        ),
        "generate_report": lambda: _generate_report(
            ctx,
            session_id=arguments.get("session_id"),
            file_path=arguments.get("file_path"),
            template=arguments.get("template", "summary"),
        ),
        "save_case": lambda: _save_case(
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
    if isinstance(exc, _ServiceUnavailable):
        return _error_result(code="SERVICE_UNAVAILABLE", message=str(exc))
    if isinstance(exc, ValidationError):
        return _error_result(code="VALIDATION_ERROR", message=str(exc))
    logger.exception("export_tool_failed", extra={"tool": tool_name})
    return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


def _get_service(ctx: Context | None):
    service = ctx.lifespan_context.flowsheet_service
    if service is None:
        raise _ServiceUnavailable("Flowsheet service is not configured.")
    return service


async def _export_csv(
    ctx: Context | None, *, session_id: str, file_path: str, object_ids: list[str] | None
) -> Dict[str, Any]:
    payload = ExportCsvInput.model_validate(
        {"session_id": session_id, "file_path": file_path, "object_ids": object_ids}
    )
    return (await _get_service(ctx).export_csv(payload)).model_dump()


async def _export_json(ctx: Context | None, *, session_id: str, format: str) -> Dict[str, Any]:
    payload = ExportJsonInput.model_validate({"session_id": session_id, "format": format})
    return (await _get_service(ctx).export_json(payload)).model_dump()


async def _generate_report(
    ctx: Context | None, *, session_id: str, file_path: str, template: str
) -> Dict[str, Any]:
    payload = GenerateReportInput.model_validate(
        {"session_id": session_id, "file_path": file_path, "template": template}
    )
    return (await _get_service(ctx).generate_report(payload)).model_dump()


async def _save_case(ctx: Context | None, *, session_id: str, file_path: str) -> Dict[str, Any]:
    payload = SaveCaseInput.model_validate({"session_id": session_id, "file_path": file_path})
    return (await _get_service(ctx).save_case(payload)).model_dump()


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
