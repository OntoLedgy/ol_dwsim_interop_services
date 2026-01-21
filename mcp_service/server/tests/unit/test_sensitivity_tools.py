import asyncio
from types import SimpleNamespace

import pytest
from mcp import types

from dwsim_mcp_server.tools.sensitivity import handle_sensitivity_tool
from models.responses import OptimizationResult, ResultRow, SensitivityStudyResult, StudyStatus


class FakeSensitivityService:
    def __init__(self):
        self.calls = {}

    async def run_sensitivity_analysis(self, payload):
        self.calls["run_sensitivity_analysis"] = payload
        return SensitivityStudyResult(
            study_id="study-1",
            status="completed",
            rows=[
                ResultRow(
                    inputs={"x": 1.0},
                    outputs={"y": 2.0},
                    converged=True,
                    error=None,
                )
            ],
            completed_steps=2,
            total_steps=2,
            elapsed_seconds=1.5,
            cancelled=False,
        )

    async def run_parameter_sweep(self, payload):
        self.calls["run_parameter_sweep"] = payload
        return SensitivityStudyResult(
            study_id="study-2",
            status="completed",
            rows=[],
            completed_steps=4,
            total_steps=4,
            elapsed_seconds=2.5,
            cancelled=False,
        )

    async def run_optimization(self, payload):
        self.calls["run_optimization"] = payload
        return OptimizationResult(
            study_id="study-3",
            status="converged",
            optimal_values={"x": 1.2},
            objective_value=0.5,
            converged=True,
            message="ok",
        )

    async def get_study_status(self, study_id):
        self.calls["get_study_status"] = study_id
        return StudyStatus(
            study_id=study_id,
            status="running",
            completed_steps=1,
            total_steps=5,
            estimated_remaining_seconds=12.0,
        )

    async def cancel_study(self, study_id):
        self.calls["cancel_study"] = study_id
        return SensitivityStudyResult(
            study_id=study_id,
            status="cancelled",
            rows=[],
            completed_steps=1,
            total_steps=5,
            elapsed_seconds=3.2,
            cancelled=True,
        )

    async def export_results(self, study_id, file_path):
        self.calls["export_results"] = {"study_id": study_id, "file_path": file_path}


def test_sensitivity_analysis_happy_path_dispatches():
    service = FakeSensitivityService()
    deps = SimpleNamespace(sensitivity_service=service)
    result = asyncio.run(
        handle_sensitivity_tool(
            "sensitivity_analysis",
            {
                "session_id": "sess",
                "variable": {"object_id": "obj-1", "property_name": "temperature"},
                "range": {"min_value": 1.0, "max_value": 2.0},
                "steps": 2,
                "outputs": [{"object_id": "obj-1", "property_name": "pressure"}],
            },
            deps,
        )
    )
    assert result["study_id"] == "study-1"
    assert service.calls["run_sensitivity_analysis"].session_id == "sess"


def test_parameter_sweep_happy_path_dispatches():
    service = FakeSensitivityService()
    deps = SimpleNamespace(sensitivity_service=service)
    result = asyncio.run(
        handle_sensitivity_tool(
            "parameter_sweep",
            {
                "session_id": "sess",
                "variables": [
                    {
                        "object_id": "obj-1",
                        "property_name": "temperature",
                        "range": {"min_value": 1.0, "max_value": 2.0},
                        "steps": 2,
                    }
                ],
                "max_combinations": 10,
                "outputs": [{"object_id": "obj-1", "property_name": "pressure"}],
            },
            deps,
        )
    )
    assert result["study_id"] == "study-2"
    assert service.calls["run_parameter_sweep"].session_id == "sess"


def test_optimize_happy_path_dispatches():
    service = FakeSensitivityService()
    deps = SimpleNamespace(sensitivity_service=service)
    result = asyncio.run(
        handle_sensitivity_tool(
            "optimize",
            {
                "session_id": "sess",
                "objective": {
                    "object_id": "obj-1",
                    "property_name": "efficiency",
                    "direction": "maximize",
                },
                "variables": [
                    {
                        "object_id": "obj-1",
                        "property_name": "temperature",
                        "lower": 1.0,
                        "upper": 2.0,
                        "initial": 1.5,
                    }
                ],
                "constraints": [
                    {
                        "object_id": "obj-1",
                        "property_name": "pressure",
                        "operator": ">=",
                        "threshold": 1.0,
                    }
                ],
                "max_iterations": 50,
            },
            deps,
        )
    )
    assert result["study_id"] == "study-3"
    assert result["status"] == "converged"
    assert service.calls["run_optimization"].session_id == "sess"


def test_get_study_status_happy_path_dispatches():
    service = FakeSensitivityService()
    deps = SimpleNamespace(sensitivity_service=service)
    result = asyncio.run(
        handle_sensitivity_tool("get_study_status", {"study_id": "study-4"}, deps)
    )
    assert result["study_id"] == "study-4"
    assert result["status"] == "running"
    assert service.calls["get_study_status"] == "study-4"


def test_cancel_study_happy_path_dispatches():
    service = FakeSensitivityService()
    deps = SimpleNamespace(sensitivity_service=service)
    result = asyncio.run(
        handle_sensitivity_tool("cancel_study", {"study_id": "study-5"}, deps)
    )
    assert result["study_id"] == "study-5"
    assert result["status"] == "cancelled"
    assert service.calls["cancel_study"] == "study-5"


def test_export_study_results_happy_path_dispatches():
    service = FakeSensitivityService()
    deps = SimpleNamespace(sensitivity_service=service)
    result = asyncio.run(
        handle_sensitivity_tool(
            "export_study_results",
            {"study_id": "study-6", "file_path": "output.csv"},
            deps,
        )
    )
    assert result["status"] == "success"
    assert service.calls["export_results"] == {
        "study_id": "study-6",
        "file_path": "output.csv",
    }


def test_validation_error_returns_call_tool_result():
    service = FakeSensitivityService()
    deps = SimpleNamespace(sensitivity_service=service)
    result = asyncio.run(
        handle_sensitivity_tool("sensitivity_analysis", {"session_id": "sess"}, deps)
    )
    assert isinstance(result, types.CallToolResult)
    assert result.isError is True
    assert result.structuredContent["code"] == "VALIDATION_ERROR"


def test_missing_service_returns_error():
    deps = SimpleNamespace(sensitivity_service=None)
    result = asyncio.run(
        handle_sensitivity_tool(
            "sensitivity_analysis",
            {"session_id": "sess"},
            deps,
        )
    )
    assert isinstance(result, types.CallToolResult)
    assert result.isError is True
    assert result.structuredContent["code"] == "SERVICE_UNAVAILABLE"


def test_study_not_found_returns_error():
    class MissingStudyService(FakeSensitivityService):
        async def get_study_status(self, study_id):
            raise KeyError(f"Study {study_id} not found")

    service = MissingStudyService()
    deps = SimpleNamespace(sensitivity_service=service)
    result = asyncio.run(
        handle_sensitivity_tool("get_study_status", {"study_id": "missing"}, deps)
    )
    assert isinstance(result, types.CallToolResult)
    assert result.isError is True
    assert result.structuredContent["code"] == "STUDY_NOT_FOUND"
