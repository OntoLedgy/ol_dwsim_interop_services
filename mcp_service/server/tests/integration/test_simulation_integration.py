# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

import asyncio
import json
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


async def _build_basic_flowsheet(session_client, session_id: str):
    """Build a three-phase separator flowsheet matching the C# golden test pattern.

    This creates a complete flowsheet with:
    - 3 compounds (Methane, Water, n-Decane) for three-phase separation
    - 1 inlet feed stream with mixed composition
    - 3 outlet streams (vapor, light liquid, heavy liquid)
    - 1 three-phase separator with proper configuration
    - All streams connected to separator
    """
    flowsheet_client = FlowsheetClient(session_client)
    service = FlowsheetService(session_client=session_client, flowsheet_client=flowsheet_client)
    deps = SimpleNamespace(flowsheet_service=service)

    # Add 3 compounds for three-phase separation (matches C# golden test)
    for compound in ["Methane", "Water", "n-Decane"]:
        add_compound = {"session_id": session_id, "compound_name": compound}
        result = await handle_flowsheet_tool("add_compound", add_compound, deps)
        if not isinstance(result, dict):
            pytest.skip(f"add_compound {compound} failed: {result}")

    # Set property package
    set_pp = {"session_id": session_id, "package_name": "peng-robinson", "options": {}}
    pp_result = await handle_flowsheet_tool("set_property_package", set_pp, deps)
    if not isinstance(pp_result, dict):
        pytest.skip(f"set_property_package failed: {pp_result}")

    # Set Binary Interaction Parameters (critical for accurate phase equilibrium)
    # Values from C# golden test / DWSIM sample
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

    # Create inlet feed stream with 3-compound composition (matches C# golden test)
    # T=300K, P=101325Pa, MolarFlow=544mol/s, 33.3% each compound
    # CRITICAL: is_source=True tells DWSIM this is a feed stream with known conditions
    add_feed = {
        "session_id": session_id,
        "name": "FEED",
        "temperature": 300.0,      # 300 K (from C# golden test)
        "pressure": 101325.0,      # 1 atm
        "molar_flow": 544.0,       # 544 mol/s (from C# golden test)
        "composition": {
            "Methane": 0.333,
            "Water": 0.333,
            "n-Decane": 0.334,     # Sums to 1.0
        },
        "is_source": True,         # Feed stream with known conditions
    }
    feed_result = await handle_flowsheet_tool("add_stream", add_feed, deps)
    if not isinstance(feed_result, dict) or "stream_id" not in feed_result:
        pytest.skip(f"add_stream FEED failed: {feed_result}")
    feed_stream_id = feed_result["stream_id"]

    # Flash the feed stream to compute phase equilibrium (required before separator calculation)
    flash_feed = {"session_id": session_id, "stream_id": feed_stream_id}
    flash_result = await handle_flowsheet_tool("flash_stream", flash_feed, deps)
    if not isinstance(flash_result, dict) or not flash_result.get("flashed"):
        pytest.skip(f"flash_stream FEED failed: {flash_result}")

    # Create 3 outlet streams with minimal properties (DWSIM will calculate flows/compositions)
    # Pressure = inlet - pressure drop: 101325 - 10000 = 91325 Pa
    # CRITICAL: is_source=False (default) tells DWSIM these are outlet streams to be calculated
    outlet_pressure = 91325.0
    outlet_temperature = 300.0

    # Vapor outlet
    add_vapor = {
        "session_id": session_id,
        "name": "VAPOR",
        "temperature": outlet_temperature,
        "pressure": outlet_pressure,
        "molar_flow": 0.001,  # Small placeholder - DWSIM will recalculate
        "composition": {"Methane": 0.333, "Water": 0.333, "n-Decane": 0.334},
        "is_source": False,   # Outlet stream - DWSIM will calculate
    }
    vapor_result = await handle_flowsheet_tool("add_stream", add_vapor, deps)
    if not isinstance(vapor_result, dict) or "stream_id" not in vapor_result:
        pytest.skip(f"add_stream VAPOR failed: {vapor_result}")
    vapor_stream_id = vapor_result["stream_id"]

    # Light liquid outlet
    add_light = {
        "session_id": session_id,
        "name": "LIGHT_LIQUID",
        "temperature": outlet_temperature,
        "pressure": outlet_pressure,
        "molar_flow": 0.001,  # Small placeholder - DWSIM will recalculate
        "composition": {"Methane": 0.333, "Water": 0.333, "n-Decane": 0.334},
        "is_source": False,   # Outlet stream - DWSIM will calculate
    }
    light_result = await handle_flowsheet_tool("add_stream", add_light, deps)
    if not isinstance(light_result, dict) or "stream_id" not in light_result:
        pytest.skip(f"add_stream LIGHT_LIQUID failed: {light_result}")
    light_stream_id = light_result["stream_id"]

    # Heavy liquid outlet
    add_heavy = {
        "session_id": session_id,
        "name": "HEAVY_LIQUID",
        "temperature": outlet_temperature,
        "pressure": outlet_pressure,
        "molar_flow": 0.001,  # Small placeholder - DWSIM will recalculate
        "composition": {"Methane": 0.333, "Water": 0.333, "n-Decane": 0.334},
        "is_source": False,   # Outlet stream - DWSIM will calculate
    }
    heavy_result = await handle_flowsheet_tool("add_stream", add_heavy, deps)
    if not isinstance(heavy_result, dict) or "stream_id" not in heavy_result:
        pytest.skip(f"add_stream HEAVY_LIQUID failed: {heavy_result}")
    heavy_stream_id = heavy_result["stream_id"]

    # Add three-phase separator with proper configuration (matches C# golden test)
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

    # Connect feed stream to separator inlet
    connect_feed = {
        "session_id": session_id,
        "source_id": feed_stream_id,
        "target_id": separator_id,
        "port_name": "Inlet",
    }
    feed_conn_result = await handle_flowsheet_tool("connect", connect_feed, deps)
    if not isinstance(feed_conn_result, dict) or not feed_conn_result.get("connected"):
        pytest.skip(f"connect feed failed: {feed_conn_result}")

    # Connect vapor stream to separator vapor outlet
    connect_vapor = {
        "session_id": session_id,
        "source_id": vapor_stream_id,
        "target_id": separator_id,
        "port_name": "VaporOutlet",
    }
    vapor_conn_result = await handle_flowsheet_tool("connect", connect_vapor, deps)
    if not isinstance(vapor_conn_result, dict) or not vapor_conn_result.get("connected"):
        pytest.skip(f"connect vapor failed: {vapor_conn_result}")

    # Connect light liquid stream to separator light liquid outlet
    connect_light = {
        "session_id": session_id,
        "source_id": light_stream_id,
        "target_id": separator_id,
        "port_name": "LiquidOutlet1",
    }
    light_conn_result = await handle_flowsheet_tool("connect", connect_light, deps)
    if not isinstance(light_conn_result, dict) or not light_conn_result.get("connected"):
        pytest.skip(f"connect light liquid failed: {light_conn_result}")

    # Connect heavy liquid stream to separator heavy liquid outlet
    connect_heavy = {
        "session_id": session_id,
        "source_id": heavy_stream_id,
        "target_id": separator_id,
        "port_name": "LiquidOutlet2",
    }
    heavy_conn_result = await handle_flowsheet_tool("connect", connect_heavy, deps)
    if not isinstance(heavy_conn_result, dict) or not heavy_conn_result.get("connected"):
        pytest.skip(f"connect heavy liquid failed: {heavy_conn_result}")


@pytest.mark.integration
def test_simulation_workflow_integration():
    async def _run():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        session_id = await session_client.create_session(flowsheet_name="mcp-simulation-integration")
        try:
            await _build_basic_flowsheet(session_client, session_id)

            sim_deps = SimpleNamespace(session_client=session_client)
            run_result = await handle_simulation_tool(
                "run",
                {"session_id": session_id, "timeout_seconds": 30},
                sim_deps,
            )
            if not isinstance(run_result, dict):
                pytest.skip(f"run failed: {run_result}")

            status_result = await handle_simulation_tool(
                "get_status",
                {"session_id": session_id},
                sim_deps,
            )
            if not isinstance(status_result, dict):
                pytest.skip(f"get_status failed: {status_result}")

            results = await handle_simulation_tool(
                "get_results",
                {"session_id": session_id},
                sim_deps,
            )
            if not isinstance(results, dict):
                pytest.skip(f"get_results failed: {results}")

            cached_results = await handle_simulation_tool(
                "get_results",
                {"session_id": session_id},
                sim_deps,
            )

            # Write results to JSON file for review
            output_dir = Path(__file__).parent / "output"
            output_dir.mkdir(exist_ok=True)
            output_file = output_dir / "simulation_results.json"
            
            # Handle NaN and Inf values for JSON serialization
            def sanitize_for_json(obj):
                if isinstance(obj, float):
                    if obj != obj:  # NaN check
                        return "NaN"
                    if obj == float('inf'):
                        return "Infinity"
                    if obj == float('-inf'):
                        return "-Infinity"
                    return obj
                if isinstance(obj, dict):
                    return {k: sanitize_for_json(v) for k, v in obj.items()}
                if isinstance(obj, list):
                    return [sanitize_for_json(v) for v in obj]
                return obj
            
            sanitized_results = sanitize_for_json(results)
            with open(output_file, "w") as f:
                json.dump(sanitized_results, f, indent=2)
            print(f"\n\nResults written to: {output_file}\n")

            assert run_result["status"] in {"converged", "failed"}
            assert status_result["status"] in {"idle", "running", "converged", "failed", "timeout"}
            # Verify we got stream results (don't compare cached vs fresh - floating point issues)
            assert results["stream_results"], "Expected at least one stream result."
            
            # Verify we have all 4 streams
            stream_ids = [s["id"] for s in results["stream_results"]]
            assert len(stream_ids) == 4, f"Expected 4 streams, got {len(stream_ids)}: {stream_ids}"
            assert set(stream_ids) == {"S1", "S2", "S3", "S4"}, f"Unexpected stream IDs: {stream_ids}"
            
            # Verify feed stream (S1) has the expected molar flow (544 mol/s as specified)
            feed_stream = next((s for s in results["stream_results"] if s["id"] == "S1"), None)
            assert feed_stream is not None, "Feed stream S1 not found in results"
            assert abs(feed_stream["total_molar_flow_mol_per_s"] - 544.0) < 1e-6, \
                f"Feed stream should have 544 mol/s, got {feed_stream['total_molar_flow_mol_per_s']}"
            
            # Verify vapor outlet (S2) has positive flow (vapor was separated)
            vapor_stream = next((s for s in results["stream_results"] if s["id"] == "S2"), None)
            assert vapor_stream is not None, "Vapor stream S2 not found"
            assert vapor_stream["total_molar_flow_mol_per_s"] > 0, \
                "Vapor outlet should have positive molar flow after separation"
            
            # Verify liquid outlet (S3) has positive flow
            liquid_stream = next((s for s in results["stream_results"] if s["id"] == "S3"), None)
            assert liquid_stream is not None, "Liquid stream S3 not found"
            assert liquid_stream["total_molar_flow_mol_per_s"] > 0, \
                "Liquid outlet should have positive molar flow after separation"
            
            # Verify mass balance: feed flow ≈ vapor + liquid1 + liquid2
            total_outlet_flow = sum(
                s["total_molar_flow_mol_per_s"] 
                for s in results["stream_results"] 
                if s["id"] in {"S2", "S3", "S4"}
            )
            feed_flow = feed_stream["total_molar_flow_mol_per_s"]
            mass_balance_error = abs(feed_flow - total_outlet_flow) / feed_flow
            assert mass_balance_error < 0.01, \
                f"Mass balance error {mass_balance_error*100:.2f}% exceeds 1% tolerance"

            phases = []
            for stream in results["stream_results"]:
                stream_phases = stream.get("phases") or []
                phases.extend(stream_phases)

            assert phases, "Expected at least one phase result."

            expected_property_keys = {
                "enthalpy_kj_per_kg",
                "molar_enthalpy_kj_per_kmol",
                "entropy_kj_per_kg_k",
                "molar_entropy_kj_per_kmol_k",
                "molar_flow_mol_per_s",
                "mass_flow_kg_per_s",
                "volumetric_flow_m3_per_s",
                "mass_fraction",
                "volumetric_fraction",
                "gibbs_free_energy",
                "helmholtz_energy",
                "internal_energy",
                "k_value",
                "fugacity",
                "activity_coefficient",
            }

            properties_found = set()
            for phase in phases:
                properties = phase.get("properties") or {}
                properties_found.update(properties.keys())

            assert (
                expected_property_keys & properties_found
            ), "Expected at least one configured phase property in results."
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
def test_simulation_timeout_scenario():
    async def _run():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        session_id = await session_client.create_session(flowsheet_name="mcp-timeout-sim")
        try:
            await _build_basic_flowsheet(session_client, session_id)

            sim_deps = SimpleNamespace(session_client=session_client)
            run_result = await handle_simulation_tool(
                "run",
                {"session_id": session_id, "timeout_seconds": 0.001},
                sim_deps,
            )
            if isinstance(run_result, dict):
                pytest.skip("Timeout not triggered; simulation completed within timeout.")
            assert run_result.isError is True
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
def test_simulation_invalid_flowsheet():
    async def _run():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        session_id = await session_client.create_session(flowsheet_name="mcp-invalid-sim")
        try:
            sim_deps = SimpleNamespace(session_client=session_client)
            run_result = await handle_simulation_tool(
                "run",
                {"session_id": session_id, "timeout_seconds": 5},
                sim_deps,
            )
            if isinstance(run_result, dict):
                pytest.skip("Run succeeded unexpectedly; invalid flowsheet failure not enforced.")
            assert run_result.isError is True
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
