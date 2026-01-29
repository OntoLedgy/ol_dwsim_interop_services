"""Export MCP tools."""

from __future__ import annotations

from typing import Any, Dict

from mcp import types
from pydantic import ValidationError

from dwsim_mcp_server.models.errors.session_error import SessionError as SessionErrorModel
from dwsim_mcp_server.models.mcp_inputs.export_inputs import (
    ExportCsvInput,
    ExportCsvOutput,
    ExportJsonInput,
    ExportJsonOutput,
    GenerateReportInput,
    GenerateReportOutput,
    SaveCaseInput,
    SaveCaseOutput,
)

from dwsim_mcp_server.observability import get_logger


def build_export_tools() -> list[types.Tool]:
    """Return MCP tool definitions for export operations."""
    return [
        types.Tool(
            name="export_csv",
            title="Export CSV",
            description=(
                "Export flowsheet streams/units to a CSV for spreadsheets or debugging. "
                "Provide file_path ending in .csv and optionally object_ids to filter "
                "(e.g., ['Feed', 'Separator']). Returns the saved path and row count."
            ),
            inputSchema=ExportCsvInput.model_json_schema(),
            outputSchema=ExportCsvOutput.model_json_schema(),
        ),
        types.Tool(
            name="export_json",
            title="Export JSON",
            description=(
                "Export flowsheet state to JSON for programmatic inspection or caching. "
                "Use format='summary' for a top-level overview or format='full' for complete data."
            ),
            inputSchema=ExportJsonInput.model_json_schema(),
            outputSchema=ExportJsonOutput.model_json_schema(),
        ),
        types.Tool(
            name="generate_report",
            title="Generate Report",
            description=(
                "Generate a Markdown report file for sharing or QA notes. "
                "Provide file_path ending in .md and choose template='summary' or 'detailed'."
            ),
            inputSchema=GenerateReportInput.model_json_schema(),
            outputSchema=GenerateReportOutput.model_json_schema(),
        ),
        types.Tool(
            name="save_case",
            title="Save Case",
            description=(
                "Save the flowsheet to a DWSIM case file for later reload. "
                "Provide file_path ending in .dwxmz (compressed) or .dwxml (uncompressed)."
            ),
            inputSchema=SaveCaseInput.model_json_schema(),
            outputSchema=SaveCaseOutput.model_json_schema(),
        ),
    ]


async def handle_export_tool(tool_name: str, arguments: Dict[str, Any], dependencies: Any):
    """Dispatch export tools by name."""
    logger = get_logger(__name__)
    service = getattr(dependencies, "flowsheet_service", None)

    if service is None:
        return _error_result(
            code="SERVICE_UNAVAILABLE",
            message="Flowsheet service is not configured.",
        )

    try:
        if tool_name == "export_csv":
            payload = ExportCsvInput.model_validate(arguments)
            result = await service.export_csv(payload)
            return result.model_dump()

        if tool_name == "export_json":
            payload = ExportJsonInput.model_validate(arguments)
            result = await service.export_json(payload)
            return result.model_dump()

        if tool_name == "generate_report":
            payload = GenerateReportInput.model_validate(arguments)
            result = await service.generate_report(payload)
            return result.model_dump()

        if tool_name == "save_case":
            payload = SaveCaseInput.model_validate(arguments)
            result = await service.save_case(payload)
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
        logger.exception("export_tool_failed", extra={"tool": tool_name})
        return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


def _error_result(code: str, message: str) -> types.CallToolResult:
    payload = SessionErrorModel(code=code, message=message).model_dump()
    return types.CallToolResult(
        content=[types.TextContent(type="text", text=message)],
        structuredContent=payload,
        isError=True,
    )
