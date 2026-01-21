"""SensitivityService bridges MCP inputs to pythonnet sensitivity adapters."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
import asyncio
import csv
import itertools
import json
from pathlib import Path
import time
import uuid
from typing import Dict, List, Optional

from scipy.optimize import minimize

from models.mcp_inputs import (
    OptimizationRequest,
    ParameterSweepRequest,
    SensitivityAnalysisRequest,
)
from models.responses import OptimizationResult, ResultRow, SensitivityStudyResult, StudyStatus

from dwsim_mcp_server.ipc.flowsheet_client import FlowsheetClient
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.observability import get_logger
from dwsim_mcp_server.utils.path_validator import resolve_case_path


@dataclass
class StudyState:
    """Internal tracking state for sensitivity studies."""

    study_id: str
    status: str
    rows: List[dict]
    cancelled: bool
    start_time: datetime
    total_steps: int
    completed_steps: int


class SensitivityService:
    """Service layer for sensitivity and optimization MCP tools."""

    def __init__(
        self,
        *,
        session_client: LimitedSessionClient,
        flowsheet_client: FlowsheetClient,
        allowed_export_roots: Optional[List[str]] = None,
    ) -> None:
        self._session_client = session_client
        self._flowsheet_client = flowsheet_client
        self._active_studies: Dict[str, StudyState] = {}
        self._allowed_export_roots = allowed_export_roots or []
        self._logger = get_logger(__name__)

    async def run_sensitivity_analysis(
        self,
        request: SensitivityAnalysisRequest,
    ) -> SensitivityStudyResult:
        """Run a single-variable sensitivity analysis study."""
        study_id = str(uuid.uuid4())
        start_time = time.time()
        total_steps = request.steps
        state = StudyState(
            study_id=study_id,
            status="running",
            rows=[],
            cancelled=False,
            start_time=datetime.utcnow(),
            total_steps=total_steps,
            completed_steps=0,
        )
        self._active_studies[study_id] = state

        span = request.range.max_value - request.range.min_value
        step_values = [
            request.range.min_value + (span * i / (total_steps - 1))
            for i in range(total_steps)
        ]

        variable_key = f"{request.variable.object_id}.{request.variable.property_name}"
        output_specs = list(request.outputs)

        for step_value in step_values:
            if state.cancelled:
                break
            inputs = {variable_key: float(step_value)}
            try:
                def _set_parameter() -> None:
                    self._flowsheet_client.set_object_parameter(
                        request.session_id,
                        object_id=request.variable.object_id,
                        parameter_name=request.variable.property_name,
                        value=step_value,
                    )

                await self._session_client.run_session_operation(
                    request.session_id,
                    _set_parameter,
                )

                run_result = await self._session_client.run_calculation(
                    request.session_id,
                )
                status = (run_result or {}).get("status")
                if status != "converged":
                    message = None
                    if isinstance(run_result, dict):
                        messages = run_result.get("messages") or []
                        if messages:
                            message = str(messages[0])
                    state.rows.append(
                        ResultRow(
                            inputs=inputs,
                            outputs=None,
                            converged=False,
                            error=message or "Simulation did not converge",
                        ).model_dump()
                    )
                    continue

                outputs: Dict[str, Optional[float]] = {}
                for output in output_specs:
                    output_key = f"{output.object_id}.{output.property_name}"
                    outputs[output_key] = await self._get_object_property(
                        request.session_id,
                        output.object_id,
                        output.property_name,
                    )

                state.rows.append(
                    ResultRow(
                        inputs=inputs,
                        outputs=outputs,
                        converged=True,
                        error=None,
                    ).model_dump()
                )
            except Exception as exc:
                self._logger.warning(
                    "sensitivity_step_failed",
                    study_id=study_id,
                    session_id=request.session_id,
                    error=str(exc),
                )
                state.rows.append(
                    ResultRow(
                        inputs=inputs,
                        outputs=None,
                        converged=False,
                        error=str(exc),
                    ).model_dump()
                )
            finally:
                state.completed_steps += 1

        state.status = "cancelled" if state.cancelled else "completed"
        elapsed_seconds = time.time() - start_time
        return SensitivityStudyResult(
            study_id=study_id,
            status=state.status,
            rows=[ResultRow.model_validate(row) for row in state.rows],
            completed_steps=state.completed_steps,
            total_steps=state.total_steps,
            elapsed_seconds=elapsed_seconds,
            cancelled=state.cancelled,
        )

    async def _get_object_property(
        self,
        session_id: str,
        object_id: str,
        property_name: str,
    ) -> Optional[float]:
        results = await self._session_client.get_calculation_results(
            session_id,
            object_id=object_id,
        )
        stream_results = (results or {}).get("stream_results") or []
        if not stream_results:
            raise KeyError(f"No results for object '{object_id}'.")

        stream = stream_results[0]
        if property_name in stream:
            return _coerce_to_float(stream.get(property_name))

        phases = stream.get("phases") or []
        for phase in phases:
            if property_name in phase:
                return _coerce_to_float(phase.get(property_name))
            properties = phase.get("properties") or {}
            if property_name in properties:
                return _coerce_to_float(properties.get(property_name))

        raise KeyError(
            f"Property '{property_name}' not found for object '{object_id}'."
        )


    async def run_parameter_sweep(
        self,
        request: ParameterSweepRequest,
    ) -> SensitivityStudyResult:
        """Run a multi-variable parameter sweep study."""
        study_id = str(uuid.uuid4())
        start_time = time.time()

        step_values_per_variable: List[List[float]] = []
        variable_keys: List[str] = []

        for variable in request.variables:
            span = variable.range.max_value - variable.range.min_value
            steps = variable.steps
            step_values = [
                variable.range.min_value + (span * i / (steps - 1))
                for i in range(steps)
            ]
            step_values_per_variable.append(step_values)
            variable_keys.append(f"{variable.object_id}.{variable.property_name}")

        total_combinations = 1
        for values in step_values_per_variable:
            total_combinations *= len(values)

        if total_combinations > request.max_combinations:
            raise ValueError(
                "Parameter sweep has "
                f"{total_combinations} combinations which exceeds max_combinations "
                f"{request.max_combinations}. Reduce steps or variables."
            )

        state = StudyState(
            study_id=study_id,
            status="running",
            rows=[],
            cancelled=False,
            start_time=datetime.utcnow(),
            total_steps=total_combinations,
            completed_steps=0,
        )
        self._active_studies[study_id] = state

        output_specs = list(request.outputs)

        for combination in itertools.product(*step_values_per_variable):
            if state.cancelled:
                break

            inputs = {
                variable_keys[index]: float(value)
                for index, value in enumerate(combination)
            }
            try:
                def _set_parameters() -> None:
                    for variable, value in zip(request.variables, combination):
                        self._flowsheet_client.set_object_parameter(
                            request.session_id,
                            object_id=variable.object_id,
                            parameter_name=variable.property_name,
                            value=value,
                        )

                await self._session_client.run_session_operation(
                    request.session_id,
                    _set_parameters,
                )

                run_result = await self._session_client.run_calculation(
                    request.session_id,
                )
                status = (run_result or {}).get("status")
                if status != "converged":
                    message = None
                    if isinstance(run_result, dict):
                        messages = run_result.get("messages") or []
                        if messages:
                            message = str(messages[0])
                    state.rows.append(
                        ResultRow(
                            inputs=inputs,
                            outputs=None,
                            converged=False,
                            error=message or "Simulation did not converge",
                        ).model_dump()
                    )
                    continue

                outputs: Dict[str, Optional[float]] = {}
                for output in output_specs:
                    output_key = f"{output.object_id}.{output.property_name}"
                    outputs[output_key] = await self._get_object_property(
                        request.session_id,
                        output.object_id,
                        output.property_name,
                    )

                state.rows.append(
                    ResultRow(
                        inputs=inputs,
                        outputs=outputs,
                        converged=True,
                        error=None,
                    ).model_dump()
                )
            except Exception as exc:
                self._logger.warning(
                    "parameter_sweep_step_failed",
                    study_id=study_id,
                    session_id=request.session_id,
                    error=str(exc),
                )
                state.rows.append(
                    ResultRow(
                        inputs=inputs,
                        outputs=None,
                        converged=False,
                        error=str(exc),
                    ).model_dump()
                )
            finally:
                state.completed_steps += 1

        state.status = "cancelled" if state.cancelled else "completed"
        elapsed_seconds = time.time() - start_time
        return SensitivityStudyResult(
            study_id=study_id,
            status=state.status,
            rows=[ResultRow.model_validate(row) for row in state.rows],
            completed_steps=state.completed_steps,
            total_steps=state.total_steps,
            elapsed_seconds=elapsed_seconds,
            cancelled=state.cancelled,
        )

    async def run_optimization(
        self,
        request: OptimizationRequest,
    ) -> OptimizationResult:
        """Run an optimization study."""
        study_id = str(uuid.uuid4())
        constraints = list(request.constraints or [])
        bounds = [(variable.lower, variable.upper) for variable in request.variables]
        initial_guess: List[float] = []
        for variable in request.variables:
            if variable.initial is None:
                initial_guess.append((variable.lower + variable.upper) / 2.0)
                continue
            clamped = min(max(variable.initial, variable.lower), variable.upper)
            initial_guess.append(clamped)

        main_loop = asyncio.get_running_loop()
        best_solution: Dict[str, object] = {
            "score": float("inf"),
            "values": None,
            "objective": None,
        }
        penalty_weight = 1e6

        async def _evaluate_candidate(values: List[float]) -> Dict[str, object]:
            def _set_parameters() -> None:
                for variable, value in zip(request.variables, values):
                    self._flowsheet_client.set_object_parameter(
                        request.session_id,
                        object_id=variable.object_id,
                        parameter_name=variable.property_name,
                        value=value,
                    )

            await self._session_client.run_session_operation(
                request.session_id,
                _set_parameters,
            )

            run_result = await self._session_client.run_calculation(
                request.session_id,
            )
            status = (run_result or {}).get("status")
            if status != "converged":
                message = None
                if isinstance(run_result, dict):
                    messages = run_result.get("messages") or []
                    if messages:
                        message = str(messages[0])
                raise RuntimeError(message or "Simulation did not converge")

            objective_value = await self._get_object_property(
                request.session_id,
                request.objective.object_id,
                request.objective.property_name,
            )
            if objective_value is None:
                raise RuntimeError("Objective value not found")

            constraint_values: List[Optional[float]] = []
            for constraint in constraints:
                constraint_values.append(
                    await self._get_object_property(
                        request.session_id,
                        constraint.object_id,
                        constraint.property_name,
                    )
                )

            return {
                "objective": float(objective_value),
                "constraints": constraint_values,
            }

        def _objective_func(values: List[float]) -> float:
            try:
                future = asyncio.run_coroutine_threadsafe(
                    _evaluate_candidate(list(values)),
                    main_loop,
                )
                evaluation = future.result(timeout=300)
            except Exception as exc:
                self._logger.warning(
                    "optimization_step_failed",
                    study_id=study_id,
                    session_id=request.session_id,
                    error=str(exc),
                )
                return penalty_weight

            objective_value = evaluation["objective"]
            base_value = (
                -objective_value
                if request.objective.direction == "maximize"
                else objective_value
            )

            penalty = 0.0
            for constraint, actual in zip(constraints, evaluation["constraints"]):
                if actual is None:
                    penalty += penalty_weight
                    continue
                if constraint.operator == "<=":
                    violation = max(0.0, actual - constraint.threshold)
                elif constraint.operator == ">=":
                    violation = max(0.0, constraint.threshold - actual)
                else:
                    violation = abs(actual - constraint.threshold)
                penalty += penalty_weight * (violation ** 2)

            score = base_value + penalty
            if score < best_solution["score"]:
                best_solution["score"] = score
                best_solution["values"] = list(values)
                best_solution["objective"] = objective_value
            return score

        result = await main_loop.run_in_executor(
            None,
            lambda: minimize(
                _objective_func,
                x0=initial_guess,
                bounds=bounds,
                method="L-BFGS-B",
                options={"maxiter": request.max_iterations},
            ),
        )

        best_values = best_solution["values"] or list(result.x)
        best_objective = best_solution["objective"]

        status: str
        message: Optional[str] = None
        if result.success:
            status = "converged"
        elif result.status == 1 or result.nit >= request.max_iterations:
            status = "max_iterations"
            message = "Optimization reached max iterations; returning best solution found."
        else:
            status = "error"
            message = result.message if isinstance(result.message, str) else None

        if best_objective is None:
            best_objective = float("nan")
            if status == "converged":
                status = "error"
                message = message or "Optimization did not produce a valid objective."

        optimal_values = {
            f"{variable.object_id}.{variable.property_name}": float(value)
            for variable, value in zip(request.variables, best_values)
        }

        return OptimizationResult(
            study_id=study_id,
            status=status,
            optimal_values=optimal_values,
            objective_value=float(best_objective),
            converged=status == "converged",
            message=message,
        )

    async def get_study_status(self, study_id: str) -> StudyStatus:
        """Get progress status for a sensitivity study."""
        state = self._active_studies.get(study_id)
        if state is None:
            raise KeyError(f"Study '{study_id}' not found.")

        elapsed_seconds = (datetime.utcnow() - state.start_time).total_seconds()
        estimated_remaining_seconds: Optional[float] = None
        if state.completed_steps > 0:
            avg_time_per_step = elapsed_seconds / state.completed_steps
            remaining_steps = max(state.total_steps - state.completed_steps, 0)
            estimated_remaining_seconds = avg_time_per_step * remaining_steps

        return StudyStatus(
            study_id=state.study_id,
            status=state.status,
            completed_steps=state.completed_steps,
            total_steps=state.total_steps,
            estimated_remaining_seconds=estimated_remaining_seconds,
        )

    async def cancel_study(self, study_id: str) -> SensitivityStudyResult:
        """Cancel a sensitivity study and return partial results."""
        state = self._active_studies.get(study_id)
        if state is None:
            raise KeyError(f"Study '{study_id}' not found.")

        state.cancelled = True
        state.status = "cancelled"
        await asyncio.sleep(0.1)

        elapsed_seconds = (datetime.utcnow() - state.start_time).total_seconds()
        return SensitivityStudyResult(
            study_id=state.study_id,
            status=state.status,
            rows=[ResultRow.model_validate(row) for row in state.rows],
            completed_steps=state.completed_steps,
            total_steps=state.total_steps,
            elapsed_seconds=elapsed_seconds,
            cancelled=state.cancelled,
        )

    async def export_results(self, study_id: str, file_path: str) -> bool:
        """Export study results to the requested file path."""
        state = self._active_studies.get(study_id)
        if state is None:
            raise KeyError(f"Study '{study_id}' not found.")

        if self._allowed_export_roots:
            resolved_path = resolve_case_path(file_path, self._allowed_export_roots)
        else:
            resolved_path = Path(file_path).expanduser().resolve(strict=False)

        extension = resolved_path.suffix.lower()
        if extension not in {".csv", ".json"}:
            raise ValueError(
                "Unsupported export format. Use a .csv or .json file extension."
            )

        result = SensitivityStudyResult(
            study_id=state.study_id,
            status=state.status,
            rows=[ResultRow.model_validate(row) for row in state.rows],
            completed_steps=state.completed_steps,
            total_steps=state.total_steps,
            elapsed_seconds=(
                datetime.utcnow() - state.start_time
            ).total_seconds(),
            cancelled=state.cancelled,
        )

        if extension == ".json":
            with resolved_path.open("w", encoding="utf-8") as handle:
                json.dump(result.model_dump(), handle, indent=2)
            return True

        input_keys: set[str] = set()
        output_keys: set[str] = set()
        for row in state.rows:
            inputs = row.get("inputs") or {}
            outputs = row.get("outputs") or {}
            input_keys.update(inputs.keys())
            output_keys.update(outputs.keys())

        ordered_input_keys = sorted(input_keys)
        ordered_output_keys = sorted(output_keys)
        header = [*ordered_input_keys, *ordered_output_keys, "converged", "error"]

        with resolved_path.open("w", encoding="utf-8", newline="") as handle:
            writer = csv.writer(handle)
            writer.writerow(header)
            for row in state.rows:
                inputs = row.get("inputs") or {}
                outputs = row.get("outputs") or {}
                row_values = [
                    inputs.get(key) for key in ordered_input_keys
                ] + [
                    outputs.get(key) for key in ordered_output_keys
                ] + [
                    row.get("converged"),
                    row.get("error"),
                ]
                writer.writerow(row_values)

        return True


def _coerce_to_float(value: Optional[object]) -> Optional[float]:
    """Convert a value to float, returning None for None input."""
    if value is None:
        return None
    try:
        return float(value)
    except (TypeError, ValueError) as exc:
        raise ValueError(f"Non-numeric property value: {value!r}") from exc
