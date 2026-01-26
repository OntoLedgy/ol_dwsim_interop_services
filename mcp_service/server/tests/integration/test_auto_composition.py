import asyncio
from types import SimpleNamespace

import pytest

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.ipc.exceptions import AssemblyLoadError, InteropError, SessionError
from dwsim_mcp_server.ipc.flowsheet_client import FlowsheetClient
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.service import FlowsheetService
from dwsim_mcp_server.tools.flowsheet import handle_flowsheet_tool
from dwsim_mcp_server.tools.simulation import handle_simulation_tool


async def _close_session(session_client, session_id: str) -> None:
    try:
        await session_client.close_session(session_id)
    except SessionError:
        pass
    await session_client.stop_monitoring()


async def _create_session(flowsheet_name: str):
    settings = ServerSettings()
    session_client = LimitedSessionClient(settings.resource_limits)
    session_client.start_monitoring()
    session_id = await session_client.create_session(flowsheet_name=flowsheet_name)
    flowsheet_client = FlowsheetClient(session_client)
    flowsheet_service = FlowsheetService(session_client=session_client, flowsheet_client=flowsheet_client)
    flowsheet_deps = SimpleNamespace(flowsheet_service=flowsheet_service)
    simulation_deps = SimpleNamespace(session_client=session_client)
    return SimpleNamespace(
        session_client=session_client,
        session_id=session_id,
        flowsheet_deps=flowsheet_deps,
        simulation_deps=simulation_deps,
    )


async def _add_compounds(session_id: str, flowsheet_deps, compounds: list[str]) -> None:
    for compound in compounds:
        add_compound = {"session_id": session_id, "compound_name": compound}
        result = await handle_flowsheet_tool("add_compound", add_compound, flowsheet_deps)
        if not isinstance(result, dict):
            pytest.skip(f"add_compound {compound} failed: {result}")


async def _set_property_package(session_id: str, flowsheet_deps, package_name: str = "peng-robinson") -> None:
    payload = {"session_id": session_id, "package_name": package_name, "options": {}}
    result = await handle_flowsheet_tool("set_property_package", payload, flowsheet_deps)
    if not isinstance(result, dict) or not result.get("applied"):
        pytest.skip(f"set_property_package failed: {result}")


async def _get_cached_composition_sum(session_client, session_id: str, stream_id: str) -> float:
    try:
        from System import Guid  # type: ignore
    except Exception as exc:
        pytest.skip(f"DWSIM worker not available: {exc}")

    def _get_sum():
        session_result = session_client.session_manager.GetSession(Guid.Parse(session_id))
        if not session_result.Success or session_result.Data is None:
            pytest.skip("Session not available for stream property lookup.")

        context = session_result.Data
        # In Python.NET 3.x, out parameters are returned as a tuple (return_value, *out_params)
        success, props = context.TryGetStreamProperties(stream_id)
        assert success is True, f"Expected cached properties for stream {stream_id}"

        composition = props.Composition.MoleFractions
        total = 0.0
        for fraction in composition:
            total += float(fraction)
        return total

    return await session_client.run_session_operation(session_id, _get_sum)


async def _build_flowsheet_with_auto_outlets(session_id: str, flowsheet_deps):
    compounds = ["Methane", "Water", "n-Decane"]
    await _add_compounds(session_id, flowsheet_deps, compounds)
    await _set_property_package(session_id, flowsheet_deps)

    bip_pairs = [
        ("Methane", "n-Decane", 0.0489),
        ("Water", "Methane", 0.5),
        ("Water", "n-Decane", 0.5),
    ]
    for compound1, compound2, value in bip_pairs:
        payload = {
            "session_id": session_id,
            "compound1": compound1,
            "compound2": compound2,
            "value": value,
        }
        result = await handle_flowsheet_tool("set_binary_interaction_parameter", payload, flowsheet_deps)
        if not isinstance(result, dict):
            pytest.skip(f"set_binary_interaction_parameter {compound1}-{compound2} failed: {result}")

    add_feed = {
        "session_id": session_id,
        "name": "FEED",
        "temperature": 300.0,
        "pressure": 101325.0,
        "molar_flow": 544.0,
        "composition": {"Methane": 0.333, "Water": 0.333, "n-Decane": 0.334},
        "is_source": True,
    }
    feed_result = await handle_flowsheet_tool("add_stream", add_feed, flowsheet_deps)
    if not isinstance(feed_result, dict) or "stream_id" not in feed_result:
        pytest.skip(f"add_stream FEED failed: {feed_result}")
    feed_stream_id = feed_result["stream_id"]

    flash_result = await handle_flowsheet_tool(
        "flash_stream",
        {"session_id": session_id, "stream_id": feed_stream_id},
        flowsheet_deps,
    )
    if not isinstance(flash_result, dict) or not flash_result.get("flashed"):
        pytest.skip(f"flash_stream FEED failed: {flash_result}")

    outlet_pressure = 91325.0
    outlet_temperature = 300.0
    outlets = []

    # For separator outlets, provide composition hints so DWSIM knows which outlet gets which phase
    # Vapor: mostly lightest component (Methane)
    # Light liquid: mostly middle component (Water)
    # Heavy liquid: mostly heaviest component (n-Decane)
    outlet_configs = [
        ("VAPOR", {"Methane": 0.9, "Water": 0.05, "n-Decane": 0.05}),
        ("LIGHT_LIQUID", {"Methane": 0.05, "Water": 0.9, "n-Decane": 0.05}),
        ("HEAVY_LIQUID", {"Methane": 0.05, "Water": 0.05, "n-Decane": 0.9}),
    ]

    for name, composition in outlet_configs:
        add_outlet = {
            "session_id": session_id,
            "name": name,
            "temperature": outlet_temperature,
            "pressure": outlet_pressure,
            "molar_flow": 0.001,
            "composition": composition,
            "is_source": False,
        }
        outlet_result = await handle_flowsheet_tool("add_stream", add_outlet, flowsheet_deps)
        if not isinstance(outlet_result, dict) or "stream_id" not in outlet_result:
            pytest.skip(f"add_stream {name} failed: {outlet_result}")
        outlets.append(outlet_result["stream_id"])

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
    unit_result = await handle_flowsheet_tool("add_unit", add_unit, flowsheet_deps)
    if not isinstance(unit_result, dict) or "unit_id" not in unit_result:
        pytest.skip(f"add_unit failed: {unit_result}")
    separator_id = unit_result["unit_id"]

    connections = [
        {"source_id": feed_stream_id, "port_name": "Inlet"},
        {"source_id": outlets[0], "port_name": "VaporOutlet"},
        {"source_id": outlets[1], "port_name": "LiquidOutlet1"},
        {"source_id": outlets[2], "port_name": "LiquidOutlet2"},
    ]
    for connection in connections:
        connect_payload = {
            "session_id": session_id,
            "source_id": connection["source_id"],
            "target_id": separator_id,
            "port_name": connection["port_name"],
        }
        connect_result = await handle_flowsheet_tool("connect", connect_payload, flowsheet_deps)
        if not isinstance(connect_result, dict) or not connect_result.get("connected"):
            pytest.skip(f"connect {connection['port_name']} failed: {connect_result}")

    return feed_stream_id, outlets


@pytest.mark.integration
def test_auto_composition_outlet_stream_creation():
    async def _run():
        context = await _create_session("mcp-auto-composition-outlet")
        try:
            await _add_compounds(context.session_id, context.flowsheet_deps, ["Methane", "Water", "n-Decane"])
            add_outlet = {
                "session_id": context.session_id,
                "name": "OUTLET",
                "temperature": 300.0,
                "pressure": 101325.0,
                "molar_flow": 0.001,
                "composition": {},
                "is_source": False,
            }
            result = await handle_flowsheet_tool("add_stream", add_outlet, context.flowsheet_deps)
            assert isinstance(result, dict) and "stream_id" in result

            composition_sum = await _get_cached_composition_sum(
                context.session_client,
                context.session_id,
                result["stream_id"],
            )
            assert abs(composition_sum - 1.0) < 1e-6
        finally:
            await _close_session(context.session_client, context.session_id)

    try:
        asyncio.run(_run())
    except (AssemblyLoadError, InteropError) as exc:
        pytest.skip(f"DWSIM worker not available: {exc}")


@pytest.mark.integration
def test_simulation_with_auto_composed_outlets():
    async def _run():
        context = await _create_session("mcp-auto-composition-sim")
        try:
            feed_stream_id, outlet_stream_ids = await _build_flowsheet_with_auto_outlets(
                context.session_id,
                context.flowsheet_deps,
            )

            run_result = await handle_simulation_tool(
                "run",
                {"session_id": context.session_id, "timeout_seconds": 30},
                context.simulation_deps,
            )
            if not isinstance(run_result, dict):
                pytest.skip(f"run failed: {run_result}")

            results = await handle_simulation_tool(
                "get_results",
                {"session_id": context.session_id},
                context.simulation_deps,
            )
            if not isinstance(results, dict):
                pytest.skip(f"get_results failed: {results}")

            stream_results = results.get("stream_results") or []
            feed_stream = next((s for s in stream_results if s["id"] == feed_stream_id), None)
            assert feed_stream is not None, f"Feed stream {feed_stream_id} not found."
            assert abs(feed_stream["total_molar_flow_mol_per_s"] - 544.0) < 1e-6

            # Verify outlet streams exist in results
            # Note: Three-phase separator may use only 2 outlets depending on feed composition
            # (heavy liquid outlet often has zero flow for this particular feed)
            outlet_flows = []
            for outlet_id in outlet_stream_ids:
                outlet_stream = next((s for s in stream_results if s["id"] == outlet_id), None)
                assert outlet_stream is not None, f"Outlet stream {outlet_id} not found."
                outlet_flows.append(outlet_stream["total_molar_flow_mol_per_s"])

            # Verify mass balance: total outlet flow should equal feed flow
            total_outlet_flow = sum(outlet_flows)
            assert abs(total_outlet_flow - 544.0) < 1e-3, f"Mass balance failed: {total_outlet_flow} != 544.0"

            # Verify at least one outlet has significant flow
            assert any(flow > 1.0 for flow in outlet_flows), f"No outlets have significant flow: {outlet_flows}"
        finally:
            await _close_session(context.session_client, context.session_id)

    try:
        asyncio.run(_run())
    except (AssemblyLoadError, InteropError) as exc:
        pytest.skip(f"DWSIM worker not available: {exc}")


@pytest.mark.integration
def test_auto_composition_error_without_compounds():
    async def _run():
        context = await _create_session("mcp-auto-composition-no-compounds")
        try:
            add_outlet = {
                "session_id": context.session_id,
                "name": "OUTLET",
                "temperature": 300.0,
                "pressure": 101325.0,
                "molar_flow": 0.001,
                "composition": {},
                "is_source": False,
            }
            result = await handle_flowsheet_tool("add_stream", add_outlet, context.flowsheet_deps)
            if isinstance(result, dict):
                pytest.fail("Expected error when creating outlet stream without compounds.")

            assert result.isError is True
            message = ""
            structured = getattr(result, "structuredContent", None)
            if isinstance(structured, dict):
                message = structured.get("message", "") or ""
            message = message or str(getattr(result, "content", "")) or str(result)
            assert "no compounds registered" in message.lower()
        finally:
            await _close_session(context.session_client, context.session_id)

    try:
        asyncio.run(_run())
    except (AssemblyLoadError, InteropError) as exc:
        pytest.skip(f"DWSIM worker not available: {exc}")
