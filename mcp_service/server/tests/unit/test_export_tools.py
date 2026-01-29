from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest
from mcp import types as mcp_types

from dwsim_mcp_server.tools.compound import build_compound_tools, handle_compound_tool
from dwsim_mcp_server.tools.export import build_export_tools, handle_export_tool
from dwsim_mcp_server.models.mcp_inputs.compound_validation import (
    CompoundInfo,
    CompoundValidationResult,
    ListCompoundsOutput,
    ValidateCompoundsOutput,
)
from dwsim_mcp_server.models.mcp_inputs.export_inputs import ExportCsvOutput, ExportJsonOutput


class TestExportTools:
    @pytest.mark.asyncio
    async def test_handle_export_tool_dispatches_export_csv(self):
        service = SimpleNamespace(
            export_csv=AsyncMock(
                return_value=ExportCsvOutput(
                    success=True,
                    file_path="out.csv",
                    row_count=3,
                )
            )
        )
        deps = SimpleNamespace(flowsheet_service=service)

        result = await handle_export_tool(
            "export_csv",
            {"session_id": "sess", "file_path": "out.csv"},
            deps,
        )

        service.export_csv.assert_awaited_once()
        payload = service.export_csv.call_args.args[0]
        assert payload.session_id == "sess"
        assert result == {"success": True, "file_path": "out.csv", "row_count": 3}

    @pytest.mark.asyncio
    async def test_handle_export_tool_dispatches_export_json(self):
        service = SimpleNamespace(
            export_json=AsyncMock(
                return_value=ExportJsonOutput(data={"status": "ok"})
            )
        )
        deps = SimpleNamespace(flowsheet_service=service)

        result = await handle_export_tool(
            "export_json",
            {"session_id": "sess", "format": "summary"},
            deps,
        )

        service.export_json.assert_awaited_once()
        payload = service.export_json.call_args.args[0]
        assert payload.format == "summary"
        assert result == {"data": {"status": "ok"}}

    @pytest.mark.asyncio
    async def test_handle_export_tool_returns_error_for_unknown_tool(self):
        service = SimpleNamespace(export_csv=AsyncMock())
        deps = SimpleNamespace(flowsheet_service=service)

        result = await handle_export_tool("unknown_export", {}, deps)

        assert isinstance(result, mcp_types.CallToolResult)
        assert result.isError is True
        assert result.structuredContent["code"] == "UNKNOWN_TOOL"

    @pytest.mark.asyncio
    async def test_handle_export_tool_returns_validation_error(self):
        service = SimpleNamespace(export_csv=AsyncMock())
        deps = SimpleNamespace(flowsheet_service=service)

        result = await handle_export_tool("export_csv", {"session_id": "sess"}, deps)

        assert isinstance(result, mcp_types.CallToolResult)
        assert result.isError is True
        assert result.structuredContent["code"] == "VALIDATION_ERROR"

    def test_build_export_tools_returns_expected_names(self):
        tools = build_export_tools()

        assert len(tools) == 4
        assert {tool.name for tool in tools} == {
            "export_csv",
            "export_json",
            "generate_report",
            "save_case",
        }


class TestCompoundTools:
    @pytest.mark.asyncio
    async def test_handle_compound_tool_dispatches_validate_compounds(self):
        service = SimpleNamespace(
            validate_compounds=AsyncMock(
                return_value=ValidateCompoundsOutput(
                    results=[
                        CompoundValidationResult(
                            input_name="water",
                            valid=True,
                            canonical_name="Water",
                            alias_used=False,
                            suggestions=[],
                        )
                    ]
                )
            )
        )
        deps = SimpleNamespace(flowsheet_service=service)

        result = await handle_compound_tool(
            "validate_compounds",
            {"session_id": "sess", "compound_names": ["water"]},
            deps,
        )

        service.validate_compounds.assert_awaited_once()
        payload = service.validate_compounds.call_args.args[0]
        assert payload.session_id == "sess"
        assert result["results"][0]["canonical_name"] == "Water"

    @pytest.mark.asyncio
    async def test_handle_compound_tool_dispatches_list_available_compounds(self):
        service = SimpleNamespace(
            list_compounds=AsyncMock(
                return_value=ListCompoundsOutput(
                    compounds=[
                        CompoundInfo(
                            name="Water",
                            category="Inorganic",
                            aliases=["H2O"],
                            formula="H2O",
                        )
                    ],
                    total_count=1,
                    has_more=False,
                )
            )
        )
        deps = SimpleNamespace(flowsheet_service=service)

        result = await handle_compound_tool(
            "list_available_compounds",
            {"session_id": "sess", "limit": 10, "offset": 0},
            deps,
        )

        service.list_compounds.assert_awaited_once()
        payload = service.list_compounds.call_args.args[0]
        assert payload.limit == 10
        assert result["total_count"] == 1

    @pytest.mark.asyncio
    async def test_handle_compound_tool_returns_error_for_unknown_tool(self):
        service = SimpleNamespace(validate_compounds=AsyncMock())
        deps = SimpleNamespace(flowsheet_service=service)

        result = await handle_compound_tool("unknown_compound", {}, deps)

        assert isinstance(result, mcp_types.CallToolResult)
        assert result.isError is True
        assert result.structuredContent["code"] == "UNKNOWN_TOOL"

    @pytest.mark.asyncio
    async def test_handle_compound_tool_returns_validation_error(self):
        service = SimpleNamespace(validate_compounds=AsyncMock())
        deps = SimpleNamespace(flowsheet_service=service)

        result = await handle_compound_tool(
            "validate_compounds",
            {"session_id": "sess", "compound_names": []},
            deps,
        )

        assert isinstance(result, mcp_types.CallToolResult)
        assert result.isError is True
        assert result.structuredContent["code"] == "VALIDATION_ERROR"

    def test_build_compound_tools_returns_expected_names(self):
        tools = build_compound_tools()

        assert len(tools) == 2
        assert {tool.name for tool in tools} == {
            "validate_compounds",
            "list_available_compounds",
        }
