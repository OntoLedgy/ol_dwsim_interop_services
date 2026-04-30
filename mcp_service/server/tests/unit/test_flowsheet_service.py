# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

import asyncio
from types import SimpleNamespace

from dwsim_mcp_server.service import FlowsheetService
from dwsim_mcp_server.models.mcp_inputs import (
    AddCompoundInput,
    AddStreamInput,
    AddUnitInput,
    ConnectInput,
    DeleteObjectInput,
    ListObjectsInput,
    SetObjectParameterInput,
)


class FakeSessionClient:
    def __init__(self):
        self.calls = []

    async def run_session_operation(self, session_id, func, timeout_override=None):
        self.calls.append(session_id)
        return func()


class FakeFlowsheetClient:
    def __init__(self):
        self.calls = {}

    def add_compound(self, session_id, compound_name):
        self.calls["add_compound"] = (session_id, compound_name)
        return True

    def set_property_package(self, session_id, package_name, options):
        self.calls["set_property_package"] = (session_id, package_name, options)
        return True

    def add_stream(self, session_id, **kwargs):
        self.calls["add_stream"] = (session_id, kwargs)
        return "S1"

    def add_unit(self, session_id, **kwargs):
        self.calls["add_unit"] = (session_id, kwargs)
        return "U1", kwargs["unit_type"]

    def connect(self, session_id, **kwargs):
        self.calls["connect"] = (session_id, kwargs)
        return True

    def list_objects(self, session_id):
        self.calls["list_objects"] = session_id
        return {
            "streams": [{"id": "S1", "name": "feed"}],
            "units": [{"id": "U1", "name": "sep", "unit_type": "separator"}],
            "connections": [{"source_id": "S1", "target_id": "U1", "port_name": "Inlet"}],
        }

    def set_object_parameter(self, session_id, **kwargs):
        self.calls["set_object_parameter"] = (session_id, kwargs)
        return {"value": kwargs["value"], "previous_value": 1.0}

    def delete_object(self, session_id, **kwargs):
        self.calls["delete_object"] = (session_id, kwargs)
        return {
            "object_id": kwargs["object_id"],
            "deleted": True,
            "removed_connections": [{"source_id": "S1", "target_id": "U1", "port_name": "Inlet"}],
        }


def _service():
    flowsheet_client = FakeFlowsheetClient()
    session_client = FakeSessionClient()
    service = FlowsheetService(
        session_client=session_client,
        flowsheet_client=flowsheet_client,
    )
    return service, flowsheet_client, session_client


def test_add_compound_calls_client_and_wraps_response():
    service, client, session_client = _service()
    payload = AddCompoundInput(session_id="sess", compound_name="water")
    result = asyncio.run(service.add_compound(payload))
    assert result.added is True
    assert client.calls["add_compound"] == ("sess", "water")
    assert session_client.calls == ["sess"]


def test_add_stream_returns_stream_id():
    service, client, session_client = _service()
    payload = AddStreamInput(
        session_id="sess",
        name="feed",
        temperature=298.15,
        pressure=101325.0,
        molar_flow=10.0,
        composition={"water": 1.0},
    )
    result = asyncio.run(service.add_stream(payload))
    assert result.stream_id == "S1"
    assert client.calls["add_stream"][0] == "sess"
    assert session_client.calls == ["sess"]


def test_add_unit_returns_unit_id_and_type():
    service, client, session_client = _service()
    payload = AddUnitInput(session_id="sess", unit_type="separator", name="sep-1", parameters={})
    result = asyncio.run(service.add_unit(payload))
    assert result.unit_id == "U1"
    assert result.unit_type == "separator"
    assert client.calls["add_unit"][0] == "sess"
    assert session_client.calls == ["sess"]


def test_connect_returns_connected_flag():
    service, client, session_client = _service()
    payload = ConnectInput(session_id="sess", source_id="S1", target_id="U1", port_name="Inlet")
    result = asyncio.run(service.connect(payload))
    assert result.connected is True
    assert client.calls["connect"][0] == "sess"
    assert session_client.calls == ["sess"]


def test_list_objects_validates_output():
    service, client, session_client = _service()
    payload = ListObjectsInput(session_id="sess")
    result = asyncio.run(service.list_objects(payload))
    assert result.streams[0].id == "S1"
    assert result.units[0].id == "U1"
    assert result.connections[0].port_name == "Inlet"
    assert client.calls["list_objects"] == "sess"
    assert session_client.calls == ["sess"]


def test_set_object_parameter_normalizes_response():
    service, client, session_client = _service()
    payload = SetObjectParameterInput(
        session_id="sess",
        object_id="U1",
        parameter_name="pressure_drop",
        value=5000.0,
    )
    result = asyncio.run(service.set_object_parameter(payload))
    assert result.value == 5000.0
    assert result.previous_value == 1.0
    assert client.calls["set_object_parameter"][0] == "sess"
    assert session_client.calls == ["sess"]


def test_delete_object_normalizes_response():
    service, client, session_client = _service()
    payload = DeleteObjectInput(session_id="sess", object_id="S1")
    result = asyncio.run(service.delete_object(payload))
    assert result.deleted is True
    assert result.object_id == "S1"
    assert result.removed_connections[0].port_name == "Inlet"
    assert client.calls["delete_object"][0] == "sess"
    assert session_client.calls == ["sess"]
