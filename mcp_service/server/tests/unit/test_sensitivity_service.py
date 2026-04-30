# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# This file is part of the OntoLedgy Thermodynamics Architecture and is
# dual-licensed:
#
#   1. Open source under the GNU Affero General Public License v3.0 or
#      later (AGPL-3.0-or-later). See the LICENSE file in the repository
#      root for the full licence text and NOTICE for attribution.
#   2. Commercial under a separate proprietary licence offered by
#      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

from datetime import datetime, timedelta

import pytest

from dwsim_mcp_server.services.sensitivity_service import SensitivityService, StudyState
from dwsim_mcp_server.models.mcp_inputs import (
    OutputSpec,
    ParameterSweepRequest,
    RangeSpec,
    SensitivityAnalysisRequest,
    SweepVariableSpec,
    VariableSpec,
)
from dwsim_mcp_server.models.responses import ResultRow


class FakeSessionClient:
    def __init__(self, run_statuses=None):
        self.run_statuses = list(run_statuses or [])
        self.run_calculation_calls = 0
        self.run_session_calls = 0

    async def run_calculation(self, session_id, **kwargs):
        self.run_calculation_calls += 1
        if self.run_statuses:
            return {"status": self.run_statuses.pop(0)}
        return {"status": "converged"}

    async def run_session_operation(self, session_id, func, **kwargs):
        self.run_session_calls += 1
        func()
        return None

    async def get_calculation_results(self, session_id, **kwargs):
        return {"stream_results": [{"temperature": 300.0, "pressure": 101.0}]}


class FakeFlowsheetClient:
    def __init__(self):
        self.calls = []

    def set_object_parameter(self, session_id, *, object_id, parameter_name, value):
        self.calls.append(
            {
                "session_id": session_id,
                "object_id": object_id,
                "parameter_name": parameter_name,
                "value": value,
            }
        )


def _service(session_client=None, flowsheet_client=None):
    return SensitivityService(
        session_client=session_client or FakeSessionClient(),
        flowsheet_client=flowsheet_client or FakeFlowsheetClient(),
    )


@pytest.mark.asyncio
async def test_run_sensitivity_analysis_generates_rows():
    service = _service()
    request = SensitivityAnalysisRequest(
        session_id="sess-1",
        variable=VariableSpec(object_id="obj-1", property_name="temperature"),
        range=RangeSpec(min_value=1.0, max_value=5.0),
        steps=5,
        outputs=[OutputSpec(object_id="obj-1", property_name="pressure")],
    )

    result = await service.run_sensitivity_analysis(request)

    assert result.total_steps == 5
    assert result.completed_steps == 5
    assert len(result.rows) == 5
    assert all(row.converged for row in result.rows)


@pytest.mark.asyncio
async def test_run_sensitivity_analysis_step_failure_continues():
    session_client = FakeSessionClient(run_statuses=["converged", "error", "converged"])
    service = _service(session_client=session_client)
    request = SensitivityAnalysisRequest(
        session_id="sess-2",
        variable=VariableSpec(object_id="obj-1", property_name="temperature"),
        range=RangeSpec(min_value=1.0, max_value=3.0),
        steps=3,
        outputs=[OutputSpec(object_id="obj-1", property_name="pressure")],
    )

    result = await service.run_sensitivity_analysis(request)

    assert len(result.rows) == 3
    assert result.rows[1].converged is False
    assert result.rows[1].outputs is None
    assert result.rows[1].error


@pytest.mark.asyncio
async def test_run_parameter_sweep_generates_combinations():
    service = _service()
    request = ParameterSweepRequest(
        session_id="sess-3",
        variables=[
            SweepVariableSpec(
                object_id="obj-1",
                property_name="temperature",
                range=RangeSpec(min_value=1.0, max_value=3.0),
                steps=3,
            ),
            SweepVariableSpec(
                object_id="obj-2",
                property_name="pressure",
                range=RangeSpec(min_value=10.0, max_value=30.0),
                steps=3,
            ),
        ],
        max_combinations=20,
        outputs=[OutputSpec(object_id="obj-1", property_name="temperature")],
    )

    result = await service.run_parameter_sweep(request)

    assert result.total_steps == 9
    assert result.completed_steps == 9
    assert len(result.rows) == 9


@pytest.mark.asyncio
async def test_run_parameter_sweep_rejects_max_combinations():
    service = _service()
    request = ParameterSweepRequest(
        session_id="sess-4",
        variables=[
            SweepVariableSpec(
                object_id="obj-1",
                property_name="temperature",
                range=RangeSpec(min_value=1.0, max_value=3.0),
                steps=3,
            ),
            SweepVariableSpec(
                object_id="obj-2",
                property_name="pressure",
                range=RangeSpec(min_value=10.0, max_value=30.0),
                steps=3,
            ),
        ],
        max_combinations=8,
        outputs=[OutputSpec(object_id="obj-1", property_name="temperature")],
    )

    with pytest.raises(ValueError, match="exceeds max_combinations"):
        await service.run_parameter_sweep(request)


@pytest.mark.asyncio
async def test_cancel_study_sets_flag_returns_partial_results():
    service = _service()
    row = ResultRow(
        inputs={"obj-1.temperature": 1.0},
        outputs={"obj-1.pressure": 101.0},
        converged=True,
        error=None,
    ).model_dump()
    state = StudyState(
        study_id="study-1",
        status="running",
        rows=[row],
        cancelled=False,
        start_time=datetime.utcnow() - timedelta(seconds=5),
        total_steps=5,
        completed_steps=1,
    )
    service._active_studies[state.study_id] = state

    result = await service.cancel_study(state.study_id)

    assert state.cancelled is True
    assert result.status == "cancelled"
    assert result.cancelled is True
    assert len(result.rows) == 1


@pytest.mark.asyncio
async def test_get_study_status_returns_progress_and_estimates():
    service = _service()
    state = StudyState(
        study_id="study-2",
        status="running",
        rows=[],
        cancelled=False,
        start_time=datetime.utcnow() - timedelta(seconds=10),
        total_steps=4,
        completed_steps=2,
    )
    service._active_studies[state.study_id] = state

    status = await service.get_study_status(state.study_id)

    assert status.completed_steps == 2
    assert status.total_steps == 4
    assert status.estimated_remaining_seconds == pytest.approx(10.0, rel=0.3)
