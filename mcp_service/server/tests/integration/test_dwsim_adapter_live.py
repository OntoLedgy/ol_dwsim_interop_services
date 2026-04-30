# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

from __future__ import annotations

import math
from pathlib import Path

import pytest
import pytest_asyncio
from ol_simulator_interop_services.domain.errors import AdapterCapabilityError
from ol_simulator_interop_services.domain.models import (
    CanonicalComponent,
    Composition,
    CompositionEntry,
    FlashProblem,
    TemperaturePressureCondition,
)

from dwsim_mcp_server.adapter import DwsimAdapterConfig, build_dwsim_adapter

pytestmark = pytest.mark.live_dwsim

ETHANOL_CAS_NUMBER = "64-17-5"
ETHANOL_FORMULA = "C2H6O"
WATER_CAS_NUMBER = "7732-18-5"
WATER_FORMULA = "H2O"
EQUIMOLAR_FRACTION = 0.5
FLASH_TEMPERATURE_KELVIN = 350.0
FLASH_PRESSURE_PASCAL = 101325.0
PHASE_COMPOSITION_TOLERANCE = 1e-4
COMPONENT_BALANCE_TOLERANCE = 1e-4  # DIS-34: DWSIM PR flash precision for ethanol/water bubble region is ~4e-6
PHASE_ENVELOPE_SAMPLE_SIZE = 5


@pytest_asyncio.fixture(scope="session", loop_scope="session")
async def live_adapter(request: pytest.FixtureRequest):
    dwsim_worker_dll_path = request.getfixturevalue("dwsim_worker_dll_path")
    assert isinstance(dwsim_worker_dll_path, Path)

    adapter = build_dwsim_adapter(
        DwsimAdapterConfig(dwsim_worker_dll_path=str(dwsim_worker_dll_path))
    )
    yield adapter


def _build_equimolar_ethanol_water_composition() -> Composition:
    ethanol = CanonicalComponent(
        cas_number=ETHANOL_CAS_NUMBER,
        molecular_formula=ETHANOL_FORMULA,
        common_name="Ethanol",
    )
    water = CanonicalComponent(
        cas_number=WATER_CAS_NUMBER,
        molecular_formula=WATER_FORMULA,
        common_name="Water",
    )
    return Composition(
        entries=(
            CompositionEntry(
                component=ethanol.reference(),
                mole_fraction=EQUIMOLAR_FRACTION,
            ),
            CompositionEntry(
                component=water.reference(),
                mole_fraction=EQUIMOLAR_FRACTION,
            ),
        )
    )


def _build_tp_condition() -> TemperaturePressureCondition:
    return TemperaturePressureCondition(
        temperature_kelvin=FLASH_TEMPERATURE_KELVIN,
        pressure_pascal=FLASH_PRESSURE_PASCAL,
    )


async def _get_peng_robinson_spec(adapter):
    packages = await adapter.list_property_packages()
    for package in packages:
        if package.display_name == "Peng-Robinson":
            return package.spec
    pytest.fail("Peng-Robinson property package was not returned by the DWSIM adapter.")


def _component_fraction_by_cas(flash_result) -> dict[str, float]:
    fractions_by_cas: dict[str, float] = {}
    for phase in flash_result.phases:
        for entry in phase.composition.entries:
            cas_number = entry.component.cas_number
            weighted_fraction = phase.fraction * entry.mole_fraction
            fractions_by_cas[cas_number] = fractions_by_cas.get(cas_number, 0.0) + weighted_fraction
    return fractions_by_cas


def _is_monotone_non_decreasing(values: list[float]) -> bool:
    return all(current <= following for current, following in zip(values, values[1:], strict=False))


async def test_flash_ethanol_water_peng_robinson_tp(live_adapter) -> None:
    composition = _build_equimolar_ethanol_water_composition()
    condition = _build_tp_condition()
    package_spec = await _get_peng_robinson_spec(live_adapter)

    result = await live_adapter.flash(
        FlashProblem(
            composition=composition,
            stream_condition=condition,
            property_package_spec=package_spec,
        )
    )

    assert result.phases
    for phase in result.phases:
        phase_total = sum(entry.mole_fraction for entry in phase.composition.entries)
        assert abs(phase_total - 1.0) <= PHASE_COMPOSITION_TOLERANCE

    component_balance = _component_fraction_by_cas(result)
    assert abs(component_balance[ETHANOL_CAS_NUMBER] - EQUIMOLAR_FRACTION) <= COMPONENT_BALANCE_TOLERANCE
    assert abs(component_balance[WATER_CAS_NUMBER] - EQUIMOLAR_FRACTION) <= COMPONENT_BALANCE_TOLERANCE
    assert result.provenance.backend_id == "dwsim"


async def test_get_properties_density_and_enthalpy_for_same_condition(live_adapter) -> None:
    composition = _build_equimolar_ethanol_water_composition()
    condition = _build_tp_condition()
    package_spec = await _get_peng_robinson_spec(live_adapter)

    bundle = await live_adapter.get_properties(
        composition=composition,
        condition=condition,
        property_package_spec=package_spec,
        property_ids=("density", "molar_enthalpy"),
    )

    properties_by_id = {property_value.property_id: property_value for property_value in bundle.properties}
    assert "density" in properties_by_id
    assert "molar_enthalpy" in properties_by_id
    assert math.isfinite(properties_by_id["density"].value)
    assert math.isfinite(properties_by_id["molar_enthalpy"].value)


async def test_phase_envelope_monotonicity_5_points(live_adapter) -> None:
    composition = _build_equimolar_ethanol_water_composition()
    package_spec = await _get_peng_robinson_spec(live_adapter)

    try:
        envelope = await live_adapter.phase_envelope(
            composition=composition,
            property_package_spec=package_spec,
        )
    except AdapterCapabilityError as exc:
        pytest.skip(f"phase_envelope is not exposed by the current DWSIM bridge: {exc}")

    if len(envelope.points) < PHASE_ENVELOPE_SAMPLE_SIZE:
        pytest.skip(
            f"phase envelope returned {len(envelope.points)} points; "
            f"{PHASE_ENVELOPE_SAMPLE_SIZE} are required for the monotonicity check"
        )

    sample_points = list(envelope.points[:PHASE_ENVELOPE_SAMPLE_SIZE])
    temperatures = [point.temperature_kelvin for point in sample_points]
    pressures = [point.pressure_pascal for point in sample_points]

    assert _is_monotone_non_decreasing(temperatures) or _is_monotone_non_decreasing(pressures)


async def test_list_property_packages_contains_peng_robinson(live_adapter) -> None:
    packages = await live_adapter.list_property_packages()

    display_names = {package.display_name for package in packages}
    assert packages
    assert "Peng-Robinson" in display_names


async def test_list_components_contains_water(live_adapter) -> None:
    components = await live_adapter.list_components()

    assert components
    assert any(component.cas_number == WATER_CAS_NUMBER for component in components)
