"""Sensitivity analysis MCP tools."""

from __future__ import annotations

from typing import Any, Awaitable, Callable, Dict

from mcp import types
from mcp.server.fastmcp.server import Context
from pydantic import ValidationError

from dwsim_mcp_server.models.errors.simulation_error import SimulationError as SimulationErrorModel
from dwsim_mcp_server.models.mcp_inputs import (
    OptimizationRequest,
    ParameterSweepRequest,
    SensitivityAnalysisRequest,
)

from dwsim_mcp_server.observability import get_logger


class _ServiceUnavailable(Exception):
    pass


def register_sensitivity_tools(mcp) -> None:
    """Register sensitivity tools with FastMCP."""

    @mcp.tool(
        description=(
            "Run a single-variable sensitivity study by sweeping one parameter over a range "
            "and collecting requested outputs at each step. Use this to understand how a "
            "specific variable impacts key results."
        )
    )
    async def sensitivity_analysis(
        session_id: str, variable_name: str, start_value: float, end_value: float, step_count: int, outputs: list[str], ctx: Context | None = None
    ):
        return await _execute_tool(
            "sensitivity_analysis",
            lambda: _sensitivity_analysis(
                ctx,
                session_id=session_id,
                variable_name=variable_name,
                start_value=start_value,
                end_value=end_value,
                step_count=step_count,
                outputs=outputs,
            ),
        )

    @mcp.tool(
        description=(
            "Run a multi-variable parameter sweep by evaluating combinations across multiple "
            "ranges. Use this to explore interactions between variables and their combined "
            "impact on outputs."
        )
    )
    async def parameter_sweep(
        session_id: str, variables: list[dict[str, Any]], outputs: list[str], ctx: Context | None = None
    ):
        return await _execute_tool(
            "parameter_sweep",
            lambda: _parameter_sweep(
                ctx,
                session_id=session_id,
                variables=variables,
                outputs=outputs,
            ),
        )

    @mcp.tool(
        description=(
            "Run an optimization to find variable values that minimize or maximize an "
            "objective. Provide bounds and optional constraints to guide the solver."
        )
    )
    async def optimize(
        session_id: str, objective: dict[str, Any], variables: list[dict[str, Any]], constraints: list[dict[str, Any]] | None = None, ctx: Context | None = None
    ):
        return await _execute_tool(
            "optimize",
            lambda: _optimize(
                ctx,
                session_id=session_id,
                objective=objective,
                variables=variables,
                constraints=constraints,
            ),
        )

    @mcp.tool(
        description=(
            "Check progress for a running sensitivity or sweep study by study_id. Returns "
            "completion counts and estimated remaining time."
        )
    )
    async def get_study_status(study_id: str, ctx: Context | None = None):
        return await _execute_tool(
            "get_study_status",
            lambda: _get_study_status(ctx, study_id=study_id),
        )

    @mcp.tool(
        description=(
            "Cancel a running study by study_id. Returns partial results collected so far "
            "and marks the study as cancelled."
        )
    )
    async def cancel_study(study_id: str, ctx: Context | None = None):
        return await _execute_tool(
            "cancel_study",
            lambda: _cancel_study(ctx, study_id=study_id),
        )

    @mcp.tool(
        description=(
            "Export completed or partial study results to a file path (CSV or JSON). "
            "Use this to persist results for external analysis."
        )
    )
    async def export_study_results(study_id: str, file_path: str, ctx: Context | None = None):
        return await _execute_tool(
            "export_study_results",
            lambda: _export_study_results(ctx, study_id=study_id, file_path=file_path),
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
    if isinstance(exc, KeyError):
        return _error_result(code="STUDY_NOT_FOUND", message=str(exc))
    if isinstance(exc, ValueError):
        return _error_result(code="INVALID_ARGUMENT", message=str(exc))
    logger.exception("sensitivity_tool_failed", extra={"tool": tool_name})
    return _error_result(code="UNEXPECTED_ERROR", message=str(exc))


def _get_service(ctx: Context | None):
    service = ctx.request_context.lifespan_context.sensitivity_service
    if service is None:
        raise _ServiceUnavailable("Sensitivity service is not configured.")
    return service


async def _sensitivity_analysis(
    ctx: Context | None,
    *,
    session_id: str,
    variable_name: str,
    start_value: float,
    end_value: float,
    step_count: int,
    outputs: list[str],
) -> Dict[str, Any]:
    payload = SensitivityAnalysisRequest.model_validate(
        {
            "session_id": session_id,
            "variable_name": variable_name,
            "start_value": start_value,
            "end_value": end_value,
            "step_count": step_count,
            "outputs": outputs,
        }
    )
    return (await _get_service(ctx).run_sensitivity_analysis(payload)).model_dump()


async def _parameter_sweep(
    ctx: Context | None,
    *,
    session_id: str,
    variables: list[dict[str, Any]],
    outputs: list[str],
) -> Dict[str, Any]:
    payload = ParameterSweepRequest.model_validate(
        {"session_id": session_id, "variables": variables, "outputs": outputs}
    )
    return (await _get_service(ctx).run_parameter_sweep(payload)).model_dump()


async def _optimize(
    ctx: Context | None,
    *,
    session_id: str,
    objective: dict[str, Any],
    variables: list[dict[str, Any]],
    constraints: list[dict[str, Any]] | None,
) -> Dict[str, Any]:
    payload = OptimizationRequest.model_validate(
        {
            "session_id": session_id,
            "objective": objective,
            "variables": variables,
            "constraints": constraints,
        }
    )
    return (await _get_service(ctx).run_optimization(payload)).model_dump()


async def _get_study_status(ctx: Context | None, *, study_id: str) -> Dict[str, Any]:
    return (await _get_service(ctx).get_study_status(study_id)).model_dump()


async def _cancel_study(ctx: Context | None, *, study_id: str) -> Dict[str, Any]:
    return (await _get_service(ctx).cancel_study(study_id)).model_dump()


async def _export_study_results(
    ctx: Context | None, *, study_id: str, file_path: str
) -> Dict[str, Any]:
    await _get_service(ctx).export_results(study_id, file_path)
    return {"study_id": study_id, "file_path": file_path, "status": "success"}


def _error_result(code: str, message: str) -> types.CallToolResult:
    payload = SimulationErrorModel(code=code, message=message).model_dump()
    return types.CallToolResult(
        content=[types.TextContent(type="text", text=message)],
        structuredContent=payload,
        isError=True,
    )
