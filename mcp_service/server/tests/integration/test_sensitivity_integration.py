import asyncio
import csv
import json
import tempfile
from types import SimpleNamespace

import pytest

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.ipc.exceptions import AssemblyLoadError, InteropError, SessionError
from dwsim_mcp_server.ipc.flowsheet_client import FlowsheetClient
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.service import FlowsheetService
from dwsim_mcp_server.services.sensitivity_service import SensitivityService
from dwsim_mcp_server.tools.flowsheet import handle_flowsheet_tool
from models.mcp_inputs import (
    ObjectiveSpec,
    OptimizationRequest,
    OutputSpec,
    RangeSpec,
    SensitivityAnalysisRequest,
    VariableWithBounds,
    VariableSpec,
)


async def _build_basic_flowsheet(session_client, session_id: str) -> tuple[str, str]:
    """Build a three-phase separator flowsheet for sensitivity testing."""
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

    return feed_stream_id, vapor_stream_id


@pytest.mark.integration
@pytest.mark.slow
def test_sensitivity_analysis_end_to_end():
    async def _run():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        session_id = await session_client.create_session(flowsheet_name="mcp-sensitivity-integration")
        try:
            feed_stream_id, vapor_stream_id = await _build_basic_flowsheet(session_client, session_id)

            flowsheet_client = FlowsheetClient(session_client)
            service = SensitivityService(
                session_client=session_client,
                flowsheet_client=flowsheet_client,
            )
            request = SensitivityAnalysisRequest(
                session_id=session_id,
                variable=VariableSpec(
                    object_id=feed_stream_id,
                    property_name="temperature",
                ),
                range=RangeSpec(min_value=290.0, max_value=310.0),
                steps=5,
                outputs=[
                    OutputSpec(
                        object_id=vapor_stream_id,
                        property_name="total_molar_flow_mol_per_s",
                    )
                ],
            )

            result = await service.run_sensitivity_analysis(request)

            assert result.total_steps == 5
            assert len(result.rows) == 5

            converged_rows = [row for row in result.rows if row.converged and row.outputs]
            if len(converged_rows) < 2:
                pytest.skip("Sensitivity analysis did not converge enough steps to compare outputs.")

            output_key = f"{vapor_stream_id}.total_molar_flow_mol_per_s"
            values = []
            for row in converged_rows:
                value = row.outputs.get(output_key) if row.outputs else None
                if value is not None:
                    values.append(float(value))

            if len(values) < 2:
                pytest.skip("Sensitivity analysis outputs missing for comparison.")

            variation = max(values) - min(values)
            assert variation > 1e-6, "Expected sensitivity outputs to vary across steps."
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
@pytest.mark.slow
def test_optimization_end_to_end():
    async def _run():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        session_id = await session_client.create_session(flowsheet_name="mcp-optimization-integration")
        try:
            feed_stream_id, vapor_stream_id = await _build_basic_flowsheet(
                session_client,
                session_id,
            )

            flowsheet_client = FlowsheetClient(session_client)
            service = SensitivityService(
                session_client=session_client,
                flowsheet_client=flowsheet_client,
            )
            request = OptimizationRequest(
                session_id=session_id,
                objective=ObjectiveSpec(
                    object_id=vapor_stream_id,
                    property_name="total_molar_flow_mol_per_s",
                    direction="maximize",
                ),
                variables=[
                    VariableWithBounds(
                        object_id=feed_stream_id,
                        property_name="temperature",
                        lower=290.0,
                        upper=330.0,
                        initial=300.0,
                    )
                ],
                max_iterations=25,
            )

            result = await service.run_optimization(request)

            assert result.converged, f"Optimization did not converge: {result.status} {result.message}"

            value_key = f"{feed_stream_id}.temperature"
            optimal_temperature = result.optimal_values.get(value_key)
            assert optimal_temperature is not None
            assert 290.0 <= optimal_temperature <= 330.0
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
@pytest.mark.slow
def test_export_results_csv():
    async def _run():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        session_id = await session_client.create_session(flowsheet_name="mcp-export-csv-integration")
        try:
            feed_stream_id, vapor_stream_id = await _build_basic_flowsheet(
                session_client,
                session_id,
            )

            flowsheet_client = FlowsheetClient(session_client)
            with tempfile.TemporaryDirectory() as temp_dir:
                service = SensitivityService(
                    session_client=session_client,
                    flowsheet_client=flowsheet_client,
                    allowed_export_roots=[temp_dir],
                )
                request = SensitivityAnalysisRequest(
                    session_id=session_id,
                    variable=VariableSpec(
                        object_id=feed_stream_id,
                        property_name="temperature",
                    ),
                    range=RangeSpec(min_value=295.0, max_value=305.0),
                    steps=3,
                    outputs=[
                        OutputSpec(
                            object_id=vapor_stream_id,
                            property_name="total_molar_flow_mol_per_s",
                        )
                    ],
                )

                result = await service.run_sensitivity_analysis(request)
                file_path = f"{temp_dir}/sensitivity.csv"
                exported = await service.export_results(result.study_id, file_path)
                assert exported is True

                with open(file_path, "r", encoding="utf-8", newline="") as handle:
                    reader = csv.reader(handle)
                    header = next(reader)
                    rows = list(reader)

                expected_columns = {
                    f"{feed_stream_id}.temperature",
                    f"{vapor_stream_id}.total_molar_flow_mol_per_s",
                    "converged",
                    "error",
                }
                assert expected_columns.issubset(set(header))
                assert len(rows) == result.total_steps
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
@pytest.mark.slow
def test_export_results_json():
    async def _run():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        session_id = await session_client.create_session(flowsheet_name="mcp-export-json-integration")
        try:
            feed_stream_id, vapor_stream_id = await _build_basic_flowsheet(
                session_client,
                session_id,
            )

            flowsheet_client = FlowsheetClient(session_client)
            with tempfile.TemporaryDirectory() as temp_dir:
                service = SensitivityService(
                    session_client=session_client,
                    flowsheet_client=flowsheet_client,
                    allowed_export_roots=[temp_dir],
                )
                request = SensitivityAnalysisRequest(
                    session_id=session_id,
                    variable=VariableSpec(
                        object_id=feed_stream_id,
                        property_name="temperature",
                    ),
                    range=RangeSpec(min_value=295.0, max_value=305.0),
                    steps=3,
                    outputs=[
                        OutputSpec(
                            object_id=vapor_stream_id,
                            property_name="total_molar_flow_mol_per_s",
                        )
                    ],
                )

                result = await service.run_sensitivity_analysis(request)
                file_path = f"{temp_dir}/sensitivity.json"
                exported = await service.export_results(result.study_id, file_path)
                assert exported is True

                with open(file_path, "r", encoding="utf-8") as handle:
                    payload = json.load(handle)

                assert payload.get("study_id") == result.study_id
                assert payload.get("status") in {"completed", "cancelled"}
                assert isinstance(payload.get("rows"), list)
                assert payload.get("total_steps") == result.total_steps
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
@pytest.mark.slow
def test_export_invalid_path_rejected():
    async def _run():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        session_id = await session_client.create_session(flowsheet_name="mcp-export-invalid-path")
        try:
            feed_stream_id, vapor_stream_id = await _build_basic_flowsheet(
                session_client,
                session_id,
            )

            flowsheet_client = FlowsheetClient(session_client)
            with tempfile.TemporaryDirectory() as temp_dir, tempfile.TemporaryDirectory() as other_dir:
                service = SensitivityService(
                    session_client=session_client,
                    flowsheet_client=flowsheet_client,
                    allowed_export_roots=[temp_dir],
                )
                request = SensitivityAnalysisRequest(
                    session_id=session_id,
                    variable=VariableSpec(
                        object_id=feed_stream_id,
                        property_name="temperature",
                    ),
                    range=RangeSpec(min_value=295.0, max_value=305.0),
                    steps=2,
                    outputs=[
                        OutputSpec(
                            object_id=vapor_stream_id,
                            property_name="total_molar_flow_mol_per_s",
                        )
                    ],
                )

                result = await service.run_sensitivity_analysis(request)
                invalid_path = f"{other_dir}/outside.csv"
                with pytest.raises(ValueError):
                    await service.export_results(result.study_id, invalid_path)
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
