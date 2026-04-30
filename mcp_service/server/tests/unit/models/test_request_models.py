# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

import pytest

from dwsim_mcp_server.models.requests.add_stream_request import AddStreamRequest
from dwsim_mcp_server.models.requests.add_unit_request import AddUnitRequest
from dwsim_mcp_server.models.requests.connect_request import ConnectRequest
from dwsim_mcp_server.models.requests.get_results_request import GetResultsRequest
from dwsim_mcp_server.models.requests.run_simulation_request import RunSimulationRequest


def test_add_stream_request_composition_sum_validation():
    with pytest.raises(ValueError):
        AddStreamRequest(
            session_id="session-1",
            name="Feed",
            composition={"water": 0.8, "ethanol": 0.4},
        )


def test_add_unit_request_rejects_non_finite_values():
    with pytest.raises(ValueError):
        AddUnitRequest(
            session_id="session-1",
            unit_type="separator",
            name="Sep",
            parameters={"pressure_drop": float("inf")},
        )


def test_connect_request_requires_port_name():
    with pytest.raises(ValueError):
        ConnectRequest(
            session_id="session-1",
            source_id="stream-1",
            target_id="unit-1",
            port_name="",
        )


def test_run_simulation_request_accepts_session_id():
    request = RunSimulationRequest(session_id="session-1")
    assert request.session_id == "session-1"


def test_get_results_request_optional_object_id():
    request = GetResultsRequest(session_id="session-1", object_id=None)
    assert request.object_id is None
