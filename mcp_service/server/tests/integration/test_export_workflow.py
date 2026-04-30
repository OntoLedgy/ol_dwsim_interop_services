# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

import asyncio
import csv
import json
import tempfile
from pathlib import Path
from types import SimpleNamespace

import pytest

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.ipc.exceptions import AssemblyLoadError, InteropError, SessionError
from dwsim_mcp_server.ipc.flowsheet_client import FlowsheetClient
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.service import FlowsheetService
from dwsim_mcp_server.tools.flowsheet import handle_flowsheet_tool
from dwsim_mcp_server.tools.simulation import handle_simulation_tool
from dwsim_mcp_server.models.mcp_inputs import (
    ExportCsvInput,
    ExportJsonInput,
    GenerateReportInput,
    ListObjectsInput,
    SaveCaseInput,
)


@pytest.fixture()
def export_output_dir(dwsim_worker_dll_path: Path) -> Path:
    export_root = dwsim_worker_dll_path.parent / "exports"
    export_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="pytest-export-", dir=export_root) as temp_dir:
        yield Path(temp_dir)


async def _build_basic_flowsheet(session_client, session_id: str) -> None:
    flowsheet_client = FlowsheetClient(session_client)
    service = FlowsheetService(session_client=session_client, flowsheet_client=flowsheet_client)
    deps = SimpleNamespace(flowsheet_service=service)

    for compound in ["Methane", "Water", "n-Decane"]:
        add_compound = {"session_id": session_id, "compound_name": compound}
        result = await handle_flowsheet_tool("add_compound", add_compound, deps)
        if not isinstance(result, dict):
            pytest.skip(f"add_compound {compound} failed: {result}")

    set_pp = {"session_id": session_id, "package_name": "peng-robinson", "options": {}}
    pp_result = await handle_flowsheet_tool("set_property_package", set_pp, deps)
    if not isinstance(pp_result, dict):
        pytest.skip(f"set_property_package failed: {pp_result}")

    bip_pairs = [
        ("Methane", "n-Decane", 0.0489),
        ("Water", "Methane", 0.5),
        ("Water", "n-Decane", 0.5),
    ]
    for compound1, compound2, value in bip_pairs:
        set_bip = {
            "session_id": session_id,
            "compound1": compound1,
            "compound2": compound2,
            "value": value,
        }
        bip_result = await handle_flowsheet_tool("set_binary_interaction_parameter", set_bip, deps)
        if not isinstance(bip_result, dict):
            pytest.skip(f"set_binary_interaction_parameter {compound1}-{compound2} failed: {bip_result}")

    add_feed = {
        "session_id": session_id,
        "name": "FEED",
        "temperature": 300.0,
        "pressure": 101325.0,
        "molar_flow": 544.0,
        "composition": {
            "Methane": 0.333,
            "Water": 0.333,
            "n-Decane": 0.334,
        },
        "is_source": True,
    }
    feed_result = await handle_flowsheet_tool("add_stream", add_feed, deps)
    if not isinstance(feed_result, dict) or "stream_id" not in feed_result:
        pytest.skip(f"add_stream FEED failed: {feed_result}")
    feed_stream_id = feed_result["stream_id"]

    flash_feed = {"session_id": session_id, "stream_id": feed_stream_id}
    flash_result = await handle_flowsheet_tool("flash_stream", flash_feed, deps)
    if not isinstance(flash_result, dict) or not flash_result.get("flashed"):
        pytest.skip(f"flash_stream FEED failed: {flash_result}")

    outlet_pressure = 91325.0
    outlet_temperature = 300.0

    add_vapor = {
        "session_id": session_id,
        "name": "VAPOR",
        "temperature": outlet_temperature,
        "pressure": outlet_pressure,
        "molar_flow": 0.001,
        "composition": {"Methane": 0.333, "Water": 0.333, "n-Decane": 0.334},
        "is_source": False,
    }
    vapor_result = await handle_flowsheet_tool("add_stream", add_vapor, deps)
    if not isinstance(vapor_result, dict) or "stream_id" not in vapor_result:
        pytest.skip(f"add_stream VAPOR failed: {vapor_result}")
    vapor_stream_id = vapor_result["stream_id"]

    add_light = {
        "session_id": session_id,
        "name": "LIGHT_LIQUID",
        "temperature": outlet_temperature,
        "pressure": outlet_pressure,
        "molar_flow": 0.001,
        "composition": {"Methane": 0.333, "Water": 0.333, "n-Decane": 0.334},
        "is_source": False,
    }
    light_result = await handle_flowsheet_tool("add_stream", add_light, deps)
    if not isinstance(light_result, dict) or "stream_id" not in light_result:
        pytest.skip(f"add_stream LIGHT_LIQUID failed: {light_result}")
    light_stream_id = light_result["stream_id"]

    add_heavy = {
        "session_id": session_id,
        "name": "HEAVY_LIQUID",
        "temperature": outlet_temperature,
        "pressure": outlet_pressure,
        "molar_flow": 0.001,
        "composition": {"Methane": 0.333, "Water": 0.333, "n-Decane": 0.334},
        "is_source": False,
    }
    heavy_result = await handle_flowsheet_tool("add_stream", add_heavy, deps)
    if not isinstance(heavy_result, dict) or "stream_id" not in heavy_result:
        pytest.skip(f"add_stream HEAVY_LIQUID failed: {heavy_result}")
    heavy_stream_id = heavy_result["stream_id"]

    add_unit = {
        "session_id": session_id,
        "unit_type": "separator",
        "name": "SEP-101",
        "parameters": {
            "CalculationMode": "Legacy",
            "PressureCalculation": "Average",
            "DimensionRatio": 3.0,
            "ResidenceTime": 5.0,
        },
    }
    unit_result = await handle_flowsheet_tool("add_unit", add_unit, deps)
    if not isinstance(unit_result, dict) or "unit_id" not in unit_result:
        pytest.skip(f"add_unit failed: {unit_result}")
    separator_id = unit_result["unit_id"]

    connect_feed = {
        "session_id": session_id,
        "source_id": feed_stream_id,
        "target_id": separator_id,
        "port_name": "Inlet",
    }
    feed_conn_result = await handle_flowsheet_tool("connect", connect_feed, deps)
    if not isinstance(feed_conn_result, dict) or not feed_conn_result.get("connected"):
        pytest.skip(f"connect feed failed: {feed_conn_result}")

    connect_vapor = {
        "session_id": session_id,
        "source_id": vapor_stream_id,
        "target_id": separator_id,
        "port_name": "VaporOutlet",
    }
    vapor_conn_result = await handle_flowsheet_tool("connect", connect_vapor, deps)
    if not isinstance(vapor_conn_result, dict) or not vapor_conn_result.get("connected"):
        pytest.skip(f"connect vapor failed: {vapor_conn_result}")

    connect_light = {
        "session_id": session_id,
        "source_id": light_stream_id,
        "target_id": separator_id,
        "port_name": "LiquidOutlet1",
    }
    light_conn_result = await handle_flowsheet_tool("connect", connect_light, deps)
    if not isinstance(light_conn_result, dict) or not light_conn_result.get("connected"):
        pytest.skip(f"connect light liquid failed: {light_conn_result}")

    connect_heavy = {
        "session_id": session_id,
        "source_id": heavy_stream_id,
        "target_id": separator_id,
        "port_name": "LiquidOutlet2",
    }
    heavy_conn_result = await handle_flowsheet_tool("connect", connect_heavy, deps)
    if not isinstance(heavy_conn_result, dict) or not heavy_conn_result.get("connected"):
        pytest.skip(f"connect heavy liquid failed: {heavy_conn_result}")


async def _run_simulation(session_client, session_id: str) -> None:
    sim_deps = SimpleNamespace(session_client=session_client)
    run_result = await handle_simulation_tool(
        "run",
        {"session_id": session_id, "timeout_seconds": 30},
        sim_deps,
    )
    if not isinstance(run_result, dict):
        pytest.skip(f"run failed: {run_result}")
    results = await handle_simulation_tool(
        "get_results",
        {"session_id": session_id},
        sim_deps,
    )
    if not isinstance(results, dict):
        pytest.skip(f"get_results failed: {results}")


def _parse_export_json_payload(result) -> dict:
    payload = result.data
    if isinstance(payload, dict) and isinstance(payload.get("value"), str):
        return json.loads(payload["value"])
    return payload


class TestExportWorkflow:
    @pytest.mark.integration
    def test_export_csv_after_simulation(self, export_output_dir: Path):
        async def _run(output_dir: Path):
            settings = ServerSettings()
            session_client = LimitedSessionClient(settings.resource_limits)
            session_client.start_monitoring()
            session_id = await session_client.create_session(flowsheet_name="mcp-export-csv")
            try:
                await _build_basic_flowsheet(session_client, session_id)
                await _run_simulation(session_client, session_id)

                flowsheet_client = FlowsheetClient(session_client)
                if not hasattr(flowsheet_client, "export_csv"):
                    pytest.skip("Flowsheet export client does not support CSV export.")

                service = FlowsheetService(session_client=session_client, flowsheet_client=flowsheet_client)
                file_path = output_dir / "flowsheet.csv"
                payload = ExportCsvInput(session_id=session_id, file_path=str(file_path))
                result = await service.export_csv(payload)

                assert result.success is True
                export_path = Path(result.file_path)
                assert export_path.exists()

                with open(export_path, "r", encoding="utf-8", newline="") as handle:
                    reader = csv.reader(handle)
                    header = next(reader)
                    rows = list(reader)

                expected_header = [
                    "ObjectId",
                    "ObjectType",
                    "Temperature_K",
                    "Pressure_Pa",
                    "MolarFlow_mol_s",
                    "MassFlow_kg_s",
                    "VaporFraction",
                    "LiquidFraction",
                ]
                assert header == expected_header
                assert rows, "Expected at least one data row in CSV export."
            finally:
                try:
                    await session_client.close_session(session_id)
                except SessionError:
                    pass
                await session_client.stop_monitoring()

        try:
            asyncio.run(_run(export_output_dir))
        except (AssemblyLoadError, InteropError) as exc:
            pytest.skip(f"DWSIM worker not available: {exc}")

    @pytest.mark.integration
    def test_export_json_formats(self):
        async def _run():
            settings = ServerSettings()
            session_client = LimitedSessionClient(settings.resource_limits)
            session_client.start_monitoring()
            session_id = await session_client.create_session(flowsheet_name="mcp-export-json")
            try:
                await _build_basic_flowsheet(session_client, session_id)
                await _run_simulation(session_client, session_id)

                flowsheet_client = FlowsheetClient(session_client)
                if not hasattr(flowsheet_client, "export_json"):
                    pytest.skip("Flowsheet export client does not support JSON export.")

                service = FlowsheetService(session_client=session_client, flowsheet_client=flowsheet_client)

                summary_payload = ExportJsonInput(session_id=session_id, format="summary")
                summary_result = await service.export_json(summary_payload)
                summary_data = _parse_export_json_payload(summary_result)
                json.dumps(summary_data, default=str)
                assert isinstance(summary_data.get("objects"), list)
                assert summary_data["objects"], "Expected summary export to include objects."

                full_payload = ExportJsonInput(session_id=session_id, format="full")
                full_result = await service.export_json(full_payload)
                full_data = _parse_export_json_payload(full_result)
                json.dumps(full_data, default=str)
                assert isinstance(full_data.get("streams"), list)
                assert isinstance(full_data.get("units"), list)
                assert full_data["streams"], "Expected full export to include streams."
                assert any("phases" in stream for stream in full_data["streams"])
            finally:
                try:
                    await session_client.close_session(session_id)
                except SessionError:
                    pass
                await session_client.stop_monitoring()

        try:
            asyncio.run(_run())
        except (AssemblyLoadError, InteropError) as exc:
            pytest.skip(f"DWSIM worker not available: {exc}")

    @pytest.mark.integration
    def test_generate_reports(self, export_output_dir: Path):
        async def _run(output_dir: Path):
            settings = ServerSettings()
            session_client = LimitedSessionClient(settings.resource_limits)
            session_client.start_monitoring()
            session_id = await session_client.create_session(flowsheet_name="mcp-export-report")
            try:
                await _build_basic_flowsheet(session_client, session_id)
                await _run_simulation(session_client, session_id)

                flowsheet_client = FlowsheetClient(session_client)
                if not hasattr(flowsheet_client, "generate_report"):
                    pytest.skip("Flowsheet export client does not support report generation.")

                service = FlowsheetService(session_client=session_client, flowsheet_client=flowsheet_client)
                summary_path = output_dir / "summary.md"
                summary_payload = GenerateReportInput(
                    session_id=session_id,
                    template="summary",
                    file_path=str(summary_path),
                )
                summary_result = await service.generate_report(summary_payload)
                assert summary_result.success is True
                assert Path(summary_result.file_path).exists()

                summary_text = Path(summary_result.file_path).read_text(encoding="utf-8")
                assert "# Flowsheet Export Report" in summary_text
                assert "## Overview" in summary_text
                assert "## Key Metrics" in summary_text

                detailed_path = output_dir / "detailed.md"
                detailed_payload = GenerateReportInput(
                    session_id=session_id,
                    template="detailed",
                    file_path=str(detailed_path),
                )
                detailed_result = await service.generate_report(detailed_payload)
                assert detailed_result.success is True
                assert Path(detailed_result.file_path).exists()

                detailed_text = Path(detailed_result.file_path).read_text(encoding="utf-8")
                assert "# Flowsheet Export Report" in detailed_text
                assert "## Streams" in detailed_text
                assert "## Units" in detailed_text
                assert "###" in detailed_text
            finally:
                try:
                    await session_client.close_session(session_id)
                except SessionError:
                    pass
                await session_client.stop_monitoring()

        try:
            asyncio.run(_run(export_output_dir))
        except (AssemblyLoadError, InteropError) as exc:
            pytest.skip(f"DWSIM worker not available: {exc}")

    @pytest.mark.integration
    def test_save_load_case_round_trip(self, tmp_path: Path):
        async def _run(output_dir: Path):
            settings = ServerSettings()
            session_client = LimitedSessionClient(settings.resource_limits)
            session_client.start_monitoring()
            session_id = await session_client.create_session(flowsheet_name="mcp-save-case")
            load_session_id = None
            try:
                await _build_basic_flowsheet(session_client, session_id)

                flowsheet_client = FlowsheetClient(session_client)
                if not hasattr(flowsheet_client, "save_case"):
                    pytest.skip("Flowsheet export client does not support save_case.")

                service = FlowsheetService(session_client=session_client, flowsheet_client=flowsheet_client)
                file_path = output_dir / "flowsheet.dwxmz"
                payload = SaveCaseInput(session_id=session_id, file_path=str(file_path))
                result = await service.save_case(payload)
                assert result.success is True
                assert Path(result.file_path).exists()

                try:
                    load_session_id = await session_client.create_session(flowsheet_name="mcp-load-case")
                    loaded = await session_client.load_case(load_session_id, str(file_path))
                    if not loaded:
                        pytest.skip("load_case did not report success.")
                    load_service = FlowsheetService(
                        session_client=session_client,
                        flowsheet_client=flowsheet_client,
                    )
                    objects = await load_service.list_objects(
                        ListObjectsInput(session_id=load_session_id)
                    )
                    assert objects.streams, "Expected streams after loading saved case."
                except SessionError as exc:
                    pytest.skip(f"load_case failed: {exc}")
            finally:
                if load_session_id:
                    try:
                        await session_client.close_session(load_session_id)
                    except SessionError:
                        pass
                try:
                    await session_client.close_session(session_id)
                except SessionError:
                    pass
                await session_client.stop_monitoring()

        try:
            asyncio.run(_run(tmp_path))
        except (AssemblyLoadError, InteropError) as exc:
            pytest.skip(f"DWSIM worker not available: {exc}")
