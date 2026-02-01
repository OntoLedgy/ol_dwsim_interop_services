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
from dwsim_mcp_server.tools.legacy import LegacyContext


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
        session_id: str,
        variable: dict[str, Any],
        range: dict[str, Any],
        steps: int,
        outputs: list[dict[str, Any]],
        ctx: Context | None = None,
    ):
        return await _execute_tool(
            "sensitivity_analysis",
            lambda: _sensitivity_analysis(
                ctx,
                session_id=session_id,
                variable=variable,
                range=range,
                steps=steps,
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
        session_id: str,
        variables: list[dict[str, Any]],
        outputs: list[dict[str, Any]],
        max_combinations: int = 1000,
        ctx: Context | None = None,
    ):
        return await _execute_tool(
            "parameter_sweep",
            lambda: _parameter_sweep(
                ctx,
                session_id=session_id,
                variables=variables,
                outputs=outputs,
                max_combinations=max_combinations,
            ),
        )

    @mcp.tool(
        description=(
            "Run an optimization to find variable values that minimize or maximize an "
            "objective. Provide bounds and optional constraints to guide the solver."
        )
    )
    async def optimize(
        session_id: str,
        objective: dict[str, Any],
        variables: list[dict[str, Any]],
        constraints: list[dict[str, Any]] | None = None,
        max_iterations: int = 100,
        ctx: Context | None = None,
    ):
        return await _execute_tool(
            "optimize",
            lambda: _optimize(
                ctx,
                session_id=session_id,
                objective=objective,
                variables=variables,
                constraints=constraints,
                max_iterations=max_iterations,
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


def build_sensitivity_tools() -> list[types.Tool]:
    return [
        _tool("sensitivity_analysis", _sensitivity_description(), SensitivityAnalysisRequest),
        _tool("parameter_sweep", _sweep_description(), ParameterSweepRequest),
        _tool("optimize", _optimize_description(), OptimizationRequest),
        _tool("get_study_status", _status_description(), _study_status_schema()),
        _tool("cancel_study", _cancel_description(), _study_status_schema()),
        _tool("export_study_results", _export_description(), _export_schema()),
    ]


async def handle_sensitivity_tool(
    tool_name: str, arguments: Dict[str, Any], dependencies
) -> Dict[str, Any] | types.CallToolResult:
    ctx = LegacyContext(dependencies)
    handlers = {
        "sensitivity_analysis": lambda: _sensitivity_analysis(
            ctx,
            session_id=arguments.get("session_id"),
            variable=arguments.get("variable"),
            range=arguments.get("range"),
            steps=arguments.get("steps"),
            outputs=arguments.get("outputs"),
        ),
        "parameter_sweep": lambda: _parameter_sweep(
            ctx,
            session_id=arguments.get("session_id"),
            variables=arguments.get("variables"),
            max_combinations=arguments.get("max_combinations", 1000),
            outputs=arguments.get("outputs"),
        ),
        "optimize": lambda: _optimize(
            ctx,
            session_id=arguments.get("session_id"),
            objective=arguments.get("objective"),
            variables=arguments.get("variables"),
            constraints=arguments.get("constraints"),
            max_iterations=arguments.get("max_iterations", 100),
        ),
        "get_study_status": lambda: _get_study_status(
            ctx, study_id=arguments.get("study_id")
        ),
        "cancel_study": lambda: _cancel_study(ctx, study_id=arguments.get("study_id")),
        "export_study_results": lambda: _export_study_results(
            ctx,
            study_id=arguments.get("study_id"),
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
    variable: dict[str, Any],
    range: dict[str, Any],
    steps: int,
    outputs: list[dict[str, Any]],
) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = SensitivityAnalysisRequest.model_validate(
        {
            "session_id": session_id,
            "variable": variable,
            "range": range,
            "steps": steps,
            "outputs": outputs,
        }
    )
    return (await service.run_sensitivity_analysis(payload)).model_dump()


async def _parameter_sweep(
    ctx: Context | None,
    *,
    session_id: str,
    variables: list[dict[str, Any]],
    max_combinations: int,
    outputs: list[dict[str, Any]],
) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = ParameterSweepRequest.model_validate(
        {
            "session_id": session_id,
            "variables": variables,
            "max_combinations": max_combinations,
            "outputs": outputs,
        }
    )
    return (await service.run_parameter_sweep(payload)).model_dump()


async def _optimize(
    ctx: Context | None,
    *,
    session_id: str,
    objective: dict[str, Any],
    variables: list[dict[str, Any]],
    constraints: list[dict[str, Any]] | None,
    max_iterations: int,
) -> Dict[str, Any]:
    service = _get_service(ctx)
    payload = OptimizationRequest.model_validate(
        {
            "session_id": session_id,
            "objective": objective,
            "variables": variables,
            "constraints": constraints,
            "max_iterations": max_iterations,
        }
    )
    return (await service.run_optimization(payload)).model_dump()


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


def _tool(name: str, description: str, model) -> types.Tool:
    return types.Tool(
        name=name,
        description=description,
        inputSchema=_schema(model),
    )


def _schema(model) -> Dict[str, Any]:
    return model.model_json_schema()


def _study_status_schema() -> type[Dict[str, Any]]:
    class _StudyStatusPayload:
        @staticmethod
        def model_json_schema() -> Dict[str, Any]:
            return {
                "type": "object",
                "properties": {"study_id": {"type": "string"}},
                "required": ["study_id"],
            }

    return _StudyStatusPayload


def _export_schema() -> type[Dict[str, Any]]:
    class _ExportPayload:
        @staticmethod
        def model_json_schema() -> Dict[str, Any]:
            return {
                "type": "object",
                "properties": {
                    "study_id": {"type": "string"},
                    "file_path": {"type": "string"},
                },
                "required": ["study_id", "file_path"],
            }

    return _ExportPayload


def _sensitivity_description() -> str:
    return (
        "Run a single-variable sensitivity study by sweeping one parameter over a range "
        "and collecting requested outputs at each step. Use this to understand how a "
        "specific variable impacts key results."
    )


def _sweep_description() -> str:
    return (
        "Run a multi-variable parameter sweep by evaluating combinations across multiple "
        "ranges. Use this to explore interactions between variables and their combined "
        "impact on outputs."
    )


def _optimize_description() -> str:
    return (
        "Run an optimization to find variable values that minimize or maximize an "
        "objective. Provide bounds and optional constraints to guide the solver."
    )


def _status_description() -> str:
    return (
        "Check progress for a running sensitivity or sweep study by study_id. Returns "
        "completion counts and estimated remaining time."
    )


def _cancel_description() -> str:
    return (
        "Cancel a running study by study_id. Returns partial results collected so far "
        "and marks the study as cancelled."
    )


def _export_description() -> str:
    return (
        "Export completed or partial study results to a file path (CSV or JSON). "
        "Use this to persist results for external analysis."
    )
