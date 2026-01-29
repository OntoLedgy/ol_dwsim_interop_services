import asyncio
from types import SimpleNamespace

import pytest
from mcp import types

from dwsim_mcp_server.tools.flowsheet import handle_flowsheet_tool
from dwsim_mcp_server.models.mcp_inputs import AddCompoundOutput


class FakeFlowsheetService:
    def __init__(self):
        self.calls = {}

    async def add_compound(self, payload):
        self.calls["add_compound"] = payload
        return AddCompoundOutput(compound_name=payload.compound_name, added=True)

    async def add_stream(self, payload):
        self.calls["add_stream"] = payload
        return SimpleNamespace(stream_id="S1", name=payload.name, model_dump=lambda: {"stream_id": "S1", "name": payload.name})


def test_add_compound_tool_happy_path():
    service = FakeFlowsheetService()
    deps = SimpleNamespace(flowsheet_service=service)
    result = asyncio.run(
        handle_flowsheet_tool(
            "add_compound",
            {"session_id": "sess", "compound_name": "water"},
            deps,
        )
    )
    assert result["compound_name"] == "water"
    assert result["added"] is True
    assert service.calls["add_compound"].session_id == "sess"


def test_validation_error_returns_call_tool_result():
    service = FakeFlowsheetService()
    deps = SimpleNamespace(flowsheet_service=service)
    result = asyncio.run(handle_flowsheet_tool("add_stream", {"session_id": "sess"}, deps))
    assert isinstance(result, types.CallToolResult)
    assert result.isError is True
    assert result.structuredContent["code"] == "VALIDATION_ERROR"


def test_missing_service_returns_error():
    deps = SimpleNamespace(flowsheet_service=None)
    result = asyncio.run(handle_flowsheet_tool("add_compound", {"session_id": "sess", "compound_name": "water"}, deps))
    assert isinstance(result, types.CallToolResult)
    assert result.isError is True
    assert result.structuredContent["code"] == "SERVICE_UNAVAILABLE"
