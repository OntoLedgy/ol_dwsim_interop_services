"""Sensitivity analysis MCP tools."""

from __future__ import annotations

from typing import Any, Dict

from mcp import types
from pydantic import ValidationError

from dwsim_mcp_server.models.errors.simulation_error import SimulationError as SimulationErrorModel
from dwsim_mcp_server.models.mcp_inputs import (
    OptimizationRequest,
    ParameterSweepRequest,
    SensitivityAnalysisRequest,
)
from dwsim_mcp_server.models.responses import OptimizationResult, SensitivityStudyResult, StudyStatus

from dwsim_mcp_server.observability import get_logger

def build_sensitivity_tools() -> list[types.Tool]:
    """Return MCP tool definitions for sensitivity operations."""
    study_id_schema = {
        "type": "object",
        "properties": {"study_id": {"type": "string"}},
        "required": ["study_id"],
    }
    export_schema = {
        "type": "object",
        "properties": {
            "study_id": {"type": "string"},
            "file_path": {"type": "string"},
        },
        "required": ["study_id", "file_path"],
    }
    export_result_schema = {
        "type": "object",
        "properties": {
            "study_id": {"type": "string"},
            "file_path": {"type": "string"},
            "status": {"type": "string"},
        },
        "required": ["study_id", "file_path", "status"],
    }

    return [
        types.Tool(
            name="sensitivity_analysis",
            title="Sensitivity Analysis",
            description=(
                "Run a single-variable sensitivity study by sweeping one parameter over a range "
                "and collecting requested outputs at each step. Use this to understand how a "
                "specific variable impacts key results."
            ),
            inputSchema=SensitivityAnalysisRequest.model_json_schema(),
            outputSchema=SensitivityStudyResult.model_json_schema(),
        ),
        types.Tool(
            name="parameter_sweep",
            title="Parameter Sweep",
            description=(
                "Run a multi-variable parameter sweep by evaluating combinations across multiple "
                "ranges. Use this to explore interactions between variables and their combined "
                "impact on outputs."
            ),
            inputSchema=ParameterSweepRequest.model_json_schema(),
            outputSchema=SensitivityStudyResult.model_json_schema(),
        ),
        types.Tool(
            name="optimize",
            title="Optimize",
            description=(
                "Run an optimization to find variable values that minimize or maximize an "
                "objective. Provide bounds and optional constraints to guide the solver."
            ),
            inputSchema=OptimizationRequest.model_json_schema(),
            outputSchema=OptimizationResult.model_json_schema(),
        ),
        types.Tool(
            name="get_study_status",
            title="Get Study Status",
            description=(
                "Check progress for a running sensitivity or sweep study by study_id. Returns "
                "completion counts and estimated remaining time."
            ),
            inputSchema=study_id_schema,
            outputSchema=StudyStatus.model_json_schema(),
        ),
        types.Tool(
            name="cancel_study",
            title="Cancel Study",
            description=(
                "Cancel a running study by study_id. Returns partial results collected so far "
                "and marks the study as cancelled."
            ),
            inputSchema=study_id_schema,
            outputSchema=SensitivityStudyResult.model_json_schema(),
        ),
        types.Tool(
            name="export_study_results",
            title="Export Study Results",
            description=(
                "Export completed or partial study results to a file path (CSV or JSON). "
                "Use this to persist results for external analysis."
            ),
            inputSchema=export_schema,
            outputSchema=export_result_schema,
        ),
    ]


async def handle_sensitivity_tool(
    tool_name: str, arguments: Dict[str, Any], dependencies: Any
):
    """Dispatch sensitivity tools by name."""
    logger = get_logger(__name__)
    service = getattr(dependencies, "sensitivity_service", None)

    if service is None:
        return _error_result(
            code="SERVICE_UNAVAILABLE",
            message="Sensitivity service is not configured.",
        )

    try:
        if tool_name == "sensitivity_analysis":
            payload = SensitivityAnalysisRequest.model_validate(arguments)
            result = await service.run_sensitivity_analysis(payload)
            return result.model_dump()

        if tool_name == "parameter_sweep":
            payload = ParameterSweepRequest.model_validate(arguments)
            result = await service.run_parameter_sweep(payload)
            return result.model_dump()

        if tool_name == "optimize":
            payload = OptimizationRequest.model_validate(arguments)
            result = await service.run_optimization(payload)
            return result.model_dump()

        if tool_name == "get_study_status":
            study_id = arguments["study_id"]
            result = await service.get_study_status(study_id)
            return result.model_dump()

        if tool_name == "cancel_study":
            study_id = arguments["study_id"]
            result = await service.cancel_study(study_id)
            return result.model_dump()

        if tool_name == "export_study_results":
            study_id = arguments["study_id"]
            file_path = arguments["file_path"]
            await service.export_results(study_id, file_path)
            return {"study_id": study_id, "file_path": file_path, "status": "success"}

        return types.CallToolResult(
            content=[types.TextContent(type="text", text=f"Unknown tool: {tool_name}")],
            structuredContent={"code": "UNKNOWN_TOOL", "message": f"Unknown tool: {tool_name}"},
            isError=True,
        )
    except ValidationError as exc:
        return _error_result(code="VALIDATION_ERROR", message=str(exc))
    except KeyError as exc:
        return _error_result(code="STUDY_NOT_FOUND", message=str(exc))
    except ValueError as exc:
        return _error_result(code="INVALID_ARGUMENT", message=str(exc))
    except Exception as exc:
        logger.exception("sensitivity_tool_failed", extra={"tool": tool_name})
        return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


def _error_result(code: str, message: str) -> types.CallToolResult:
    payload = SimulationErrorModel(code=code, message=message).model_dump()
    return types.CallToolResult(
        content=[types.TextContent(type="text", text=message)],
        structuredContent=payload,
        isError=True,
    )
