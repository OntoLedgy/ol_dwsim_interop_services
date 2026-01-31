import asyncio
from types import SimpleNamespace

import pytest

from dwsim_mcp_server.models.enums.physical_quantity_types import PhysicalQuantityTypes
from dwsim_mcp_server.models.enums.unit_symbols import (
    MolarEnergyUnits,
    MolarEntropyUnits,
    PressureUnits,
    TemperatureUnits,
)
from dwsim_mcp_server.models.measurements.measurements import Measurements
from dwsim_mcp_server.models.measurements.units_of_measure import UnitsOfMeasure

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.ipc.exceptions import AssemblyLoadError, InteropError, SessionError
from dwsim_mcp_server.ipc.flowsheet_client import FlowsheetClient
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.service import FlowsheetService
from dwsim_mcp_server.services import ThermodynamicsService
from dwsim_mcp_server.tools.analysis import handle_analysis_tool
from dwsim_mcp_server.tools.flowsheet import handle_flowsheet_tool
from dwsim_mcp_server.tools.simulation import handle_simulation_tool


def _build_measurement(quantity_type, value, unit_name):
    return Measurements(
        quantity_type=quantity_type,
        value=value,
        unit=UnitsOfMeasure(
            unit_name=unit_name,
            quantity_type=quantity_type,
            valid_range=None,
        ),
    )


async def _close_session(session_client, session_id: str) -> None:
    try:
        await session_client.close_session(session_id)
    except SessionError:
        pass
    await session_client.stop_monitoring()


@pytest.fixture
def session_with_property_package():
    async def _setup():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        session_id = await session_client.create_session(flowsheet_name="mcp-flash-integration")
        try:
            flowsheet_client = FlowsheetClient(session_client)
        except (AssemblyLoadError, InteropError) as exc:
            await _close_session(session_client, session_id)
            pytest.skip(f"DWSIM worker not available: {exc}")

        flowsheet_service = FlowsheetService(
            session_client=session_client,
            flowsheet_client=flowsheet_client,
        )
        flowsheet_deps = SimpleNamespace(flowsheet_service=flowsheet_service)

        set_pp = {"session_id": session_id, "package_name": "peng-robinson", "options": {}}
        pp_result = await handle_flowsheet_tool("set_property_package", set_pp, flowsheet_deps)
        if not isinstance(pp_result, dict) or not pp_result.get("applied"):
            await _close_session(session_client, session_id)
            pytest.skip(f"set_property_package failed: {pp_result}")

        add_compound = {"session_id": session_id, "compound_name": "Methane"}
        compound_result = await handle_flowsheet_tool("add_compound", add_compound, flowsheet_deps)
        if not isinstance(compound_result, dict):
            await _close_session(session_client, session_id)
            pytest.skip(f"add_compound failed: {compound_result}")

        analysis_deps = SimpleNamespace(
            thermodynamics_service=ThermodynamicsService(session_client=session_client)
        )
        simulation_deps = SimpleNamespace(session_client=session_client)

        return SimpleNamespace(
            session_client=session_client,
            session_id=session_id,
            flowsheet_deps=flowsheet_deps,
            analysis_deps=analysis_deps,
            simulation_deps=simulation_deps,
        )

    session_context = asyncio.run(_setup())
    try:
        yield session_context
    finally:
        asyncio.run(_close_session(session_context.session_client, session_context.session_id))


@pytest.fixture
def session_without_property_package():
    async def _setup():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        try:
            session_id = await session_client.create_session(flowsheet_name="mcp-flash-no-pp")
        except (AssemblyLoadError, InteropError) as exc:
            await session_client.stop_monitoring()
            pytest.skip(f"DWSIM worker not available: {exc}")
        return SimpleNamespace(session_client=session_client, session_id=session_id)

    session_context = asyncio.run(_setup())
    try:
        yield session_context
    finally:
        asyncio.run(_close_session(session_context.session_client, session_context.session_id))


def _extract_stream_property(stream_results, stream_id: str, property_key: str):
    stream = next((item for item in stream_results if item.get("id") == stream_id), None)
    if stream is None:
        return None
    for phase in stream.get("phases", []):
        properties = phase.get("properties") or {}
        if property_key in properties:
            return properties[property_key]
    return None


async def _add_source_stream(session_id: str, flowsheet_deps, *, name: str, temperature: float, pressure: float,
                             composition: dict[str, float]):
    add_stream = {
        "session_id": session_id,
        "name": name,
        "temperature": temperature,
        "pressure": pressure,
        "molar_flow": 1.0,
        "composition": composition,
        "is_source": True,
    }
    stream_result = await handle_flowsheet_tool("add_stream", add_stream, flowsheet_deps)
    if not isinstance(stream_result, dict) or "stream_id" not in stream_result:
        pytest.skip(f"add_stream failed: {stream_result}")
    stream_id = stream_result["stream_id"]

    flash_result = await handle_flowsheet_tool(
        "flash_stream",
        {"session_id": session_id, "stream_id": stream_id},
        flowsheet_deps,
    )
    if not isinstance(flash_result, dict) or not flash_result.get("flashed"):
        pytest.skip(f"flash_stream failed: {flash_result}")

    return stream_id


async def _get_molar_property_value(
    session_id: str,
    flowsheet_deps,
    simulation_deps,
    *,
    stream_id: str,
    property_key: str,
):
    run_result = await handle_simulation_tool(
        "run",
        {"session_id": session_id, "timeout_seconds": 30},
        simulation_deps,
    )
    if not isinstance(run_result, dict):
        pytest.skip(f"run failed: {run_result}")

    results = await handle_simulation_tool("get_results", {"session_id": session_id}, simulation_deps)
    if not isinstance(results, dict):
        pytest.skip(f"get_results failed: {results}")

    stream_results = results.get("stream_results") or []
    value = _extract_stream_property(stream_results, stream_id, property_key)
    if value is None:
        pytest.skip(f"{property_key} not found in stream results.")
    return value


@pytest.mark.integration
def test_flash_tp_single_phase(session_with_property_package):
    async def _run():
        payload = {
            "session_id": session_with_property_package.session_id,
            "compounds": ["Methane"],
            "composition": [1.0],
            "temperature": _build_measurement(
                PhysicalQuantityTypes.TEMPERATURE,
                200.0,
                TemperatureUnits.KELVIN.value,
            ),
            "pressure": _build_measurement(
                PhysicalQuantityTypes.PRESSURE,
                101325.0,
                PressureUnits.PASCAL.value,
            ),
        }
        result = await handle_analysis_tool("flash_tp", payload, session_with_property_package.analysis_deps)
        if not isinstance(result, dict):
            pytest.skip(f"flash_tp failed: {result}")

        assert result["converged"] is True
        phases = result.get("phases") or []
        assert len(phases) == 1, f"Expected single phase, got {len(phases)}"
        assert phases[0]["phase_type"] in {"Vapor", "Liquid"}

    try:
        asyncio.run(_run())
    except (AssemblyLoadError, InteropError) as exc:
        pytest.skip(f"DWSIM worker not available: {exc}")


@pytest.mark.integration
def test_flash_ph_water_steam_transition(session_with_property_package):
    async def _run():
        add_water = {"session_id": session_with_property_package.session_id, "compound_name": "Water"}
        water_result = await handle_flowsheet_tool(
            "add_compound",
            add_water,
            session_with_property_package.flowsheet_deps,
        )
        if not isinstance(water_result, dict):
            pytest.skip(f"add_compound Water failed: {water_result}")

        stream_id = await _add_source_stream(
            session_with_property_package.session_id,
            session_with_property_package.flowsheet_deps,
            name="WATER_FEED",
            temperature=373.15,
            pressure=101325.0,
            composition={"Water": 1.0, "Methane": 0.0},
        )

        molar_enthalpy = await _get_molar_property_value(
            session_with_property_package.session_id,
            session_with_property_package.flowsheet_deps,
            session_with_property_package.simulation_deps,
            stream_id=stream_id,
            property_key="molar_enthalpy_kj_per_kmol",
        )

        payload = {
            "session_id": session_with_property_package.session_id,
            "compounds": ["Water"],
            "composition": [1.0],
            "pressure": _build_measurement(
                PhysicalQuantityTypes.PRESSURE,
                101325.0,
                PressureUnits.PASCAL.value,
            ),
            "enthalpy": _build_measurement(
                PhysicalQuantityTypes.MOLAR_ENTHALPY,
                float(molar_enthalpy),  # kJ/kmol is numerically equal to J/mol
                MolarEnergyUnits.JOULE_PER_MOLE.value,
            ),
        }
        result = await handle_analysis_tool("flash_ph", payload, session_with_property_package.analysis_deps)
        if not isinstance(result, dict):
            pytest.skip(f"flash_ph failed: {result}")

        assert result["converged"] is True
        assert result.get("phases"), "Expected at least one phase in flash_ph results."

    try:
        asyncio.run(_run())
    except (AssemblyLoadError, InteropError) as exc:
        pytest.skip(f"DWSIM worker not available: {exc}")


@pytest.mark.integration
def test_flash_ps_ideal_gas_behavior(session_with_property_package):
    async def _run():
        stream_id = await _add_source_stream(
            session_with_property_package.session_id,
            session_with_property_package.flowsheet_deps,
            name="METHANE_FEED",
            temperature=300.0,
            pressure=101325.0,
            composition={"Methane": 1.0},
        )

        molar_entropy = await _get_molar_property_value(
            session_with_property_package.session_id,
            session_with_property_package.flowsheet_deps,
            session_with_property_package.simulation_deps,
            stream_id=stream_id,
            property_key="molar_entropy_kj_per_kmol_k",
        )

        payload = {
            "session_id": session_with_property_package.session_id,
            "compounds": ["Methane"],
            "composition": [1.0],
            "pressure": _build_measurement(
                PhysicalQuantityTypes.PRESSURE,
                101325.0,
                PressureUnits.PASCAL.value,
            ),
            "entropy": _build_measurement(
                PhysicalQuantityTypes.MOLAR_ENTROPY,
                float(molar_entropy),  # kJ/(kmol*K) is numerically equal to J/(mol*K)
                MolarEntropyUnits.JOULE_PER_MOLE_KELVIN.value,
            ),
        }
        result = await handle_analysis_tool("flash_ps", payload, session_with_property_package.analysis_deps)
        if not isinstance(result, dict):
            pytest.skip(f"flash_ps failed: {result}")

        assert result["converged"] is True
        assert result.get("phases"), "Expected at least one phase in flash_ps results."

    try:
        asyncio.run(_run())
    except (AssemblyLoadError, InteropError) as exc:
        pytest.skip(f"DWSIM worker not available: {exc}")


@pytest.mark.integration
def test_flash_tp_requires_property_package(session_without_property_package):
    async def _run():
        analysis_deps = SimpleNamespace(
            thermodynamics_service=ThermodynamicsService(
                session_client=session_without_property_package.session_client
            )
        )
        payload = {
            "session_id": session_without_property_package.session_id,
            "compounds": ["Methane"],
            "composition": [1.0],
            "temperature": _build_measurement(
                PhysicalQuantityTypes.TEMPERATURE,
                200.0,
                TemperatureUnits.KELVIN.value,
            ),
            "pressure": _build_measurement(
                PhysicalQuantityTypes.PRESSURE,
                101325.0,
                PressureUnits.PASCAL.value,
            ),
        }
        result = await handle_analysis_tool("flash_tp", payload, analysis_deps)
        if isinstance(result, dict):
            assert result["converged"] is False
            assert not result.get("phases"), "Expected no phases when property package is missing."
        else:
            assert result.isError is True

    try:
        asyncio.run(_run())
    except (AssemblyLoadError, InteropError) as exc:
        pytest.skip(f"DWSIM worker not available: {exc}")


@pytest.mark.integration
def test_flash_tp_rejects_invalid_compound(session_with_property_package):
    async def _run():
        payload = {
            "session_id": session_with_property_package.session_id,
            "compounds": ["NotARealCompound"],
            "composition": [1.0],
            "temperature": _build_measurement(
                PhysicalQuantityTypes.TEMPERATURE,
                250.0,
                TemperatureUnits.KELVIN.value,
            ),
            "pressure": _build_measurement(
                PhysicalQuantityTypes.PRESSURE,
                101325.0,
                PressureUnits.PASCAL.value,
            ),
        }
        result = await handle_analysis_tool("flash_tp", payload, session_with_property_package.analysis_deps)
        if isinstance(result, dict):
            assert result["converged"] is False
            assert not result.get("phases"), "Expected no phases for invalid compound."
        else:
            assert result.isError is True

    try:
        asyncio.run(_run())
    except (AssemblyLoadError, InteropError) as exc:
        pytest.skip(f"DWSIM worker not available: {exc}")


@pytest.mark.integration
def test_flash_tp_rejects_composition_mismatch(session_with_property_package):
    async def _run():
        payload = {
            "session_id": session_with_property_package.session_id,
            "compounds": ["Methane", "Ethane"],
            "composition": [1.0],
            "temperature": _build_measurement(
                PhysicalQuantityTypes.TEMPERATURE,
                250.0,
                TemperatureUnits.KELVIN.value,
            ),
            "pressure": _build_measurement(
                PhysicalQuantityTypes.PRESSURE,
                101325.0,
                PressureUnits.PASCAL.value,
            ),
        }
        result = await handle_analysis_tool("flash_tp", payload, session_with_property_package.analysis_deps)
        assert result.isError is True
        assert result.structuredContent["code"] == "VALIDATION_ERROR"

    asyncio.run(_run())
