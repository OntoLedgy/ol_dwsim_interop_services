import json

import pytest

from models.enums.flash_calculation_types import FlashCalculationTypes
from models.enums.phase_types import PhaseTypes
from models.enums.physical_quantity_types import PhysicalQuantityTypes
from models.enums.unit_symbols import MolarEnergyUnits, MolarEntropyUnits, PressureUnits, TemperatureUnits
from models.measurements.measurements import Measurements
from models.measurements.physical_properties import PhysicalProperties
from models.measurements.units_of_measure import UnitsOfMeasure
from models.mcp_inputs.flash_inputs import FlashPHInputs, FlashPSInputs, FlashTPInputs
from models.responses.flash_results import FlashResults, PhaseResults


def build_measurement(quantity_type, value, unit_name):
    return Measurements(
        quantity_type=quantity_type,
        value=value,
        unit=UnitsOfMeasure(
            unit_name=unit_name,
            quantity_type=quantity_type,
            valid_range=None,
        ),
    )


def test_flash_tp_inputs_accepts_valid_payload():
    payload = FlashTPInputs(
        session_id="s1",
        compounds=["water", "ethanol"],
        composition=[0.5, 0.5],
        temperature=build_measurement(
            PhysicalQuantityTypes.TEMPERATURE,
            298.15,
            TemperatureUnits.KELVIN.value,
        ),
        pressure=build_measurement(
            PhysicalQuantityTypes.PRESSURE,
            101325.0,
            PressureUnits.PASCAL.value,
        ),
    )
    assert payload.composition == [0.5, 0.5]


@pytest.mark.parametrize(
    ("model_cls", "kwargs"),
    [
        (
            FlashTPInputs,
            dict(
                session_id="s1",
                compounds=["water", "ethanol"],
                composition=[0.6, 0.6],
                temperature=build_measurement(
                    PhysicalQuantityTypes.TEMPERATURE,
                    298.15,
                    TemperatureUnits.KELVIN.value,
                ),
                pressure=build_measurement(
                    PhysicalQuantityTypes.PRESSURE,
                    101325.0,
                    PressureUnits.PASCAL.value,
                ),
            ),
        ),
        (
            FlashPHInputs,
            dict(
                session_id="s1",
                compounds=["water", "ethanol"],
                composition=[0.6, 0.6],
                pressure=build_measurement(
                    PhysicalQuantityTypes.PRESSURE,
                    101325.0,
                    PressureUnits.PASCAL.value,
                ),
                enthalpy=build_measurement(
                    PhysicalQuantityTypes.MOLAR_ENTHALPY,
                    1000.0,
                    MolarEnergyUnits.JOULE_PER_MOLE.value,
                ),
            ),
        ),
        (
            FlashPSInputs,
            dict(
                session_id="s1",
                compounds=["water", "ethanol"],
                composition=[0.6, 0.6],
                pressure=build_measurement(
                    PhysicalQuantityTypes.PRESSURE,
                    101325.0,
                    PressureUnits.PASCAL.value,
                ),
                entropy=build_measurement(
                    PhysicalQuantityTypes.MOLAR_ENTROPY,
                    10.0,
                    MolarEntropyUnits.JOULE_PER_MOLE_KELVIN.value,
                ),
            ),
        ),
    ],
)
def test_flash_inputs_reject_invalid_composition_sum(model_cls, kwargs):
    with pytest.raises(ValueError):
        model_cls(**kwargs)


@pytest.mark.parametrize(
    ("model_cls", "kwargs"),
    [
        (
            FlashTPInputs,
            dict(
                session_id="s1",
                compounds=["water", "ethanol"],
                composition=[1.0],
                temperature=build_measurement(
                    PhysicalQuantityTypes.TEMPERATURE,
                    298.15,
                    TemperatureUnits.KELVIN.value,
                ),
                pressure=build_measurement(
                    PhysicalQuantityTypes.PRESSURE,
                    101325.0,
                    PressureUnits.PASCAL.value,
                ),
            ),
        ),
        (
            FlashPHInputs,
            dict(
                session_id="s1",
                compounds=["water", "ethanol"],
                composition=[1.0],
                pressure=build_measurement(
                    PhysicalQuantityTypes.PRESSURE,
                    101325.0,
                    PressureUnits.PASCAL.value,
                ),
                enthalpy=build_measurement(
                    PhysicalQuantityTypes.MOLAR_ENTHALPY,
                    1000.0,
                    MolarEnergyUnits.JOULE_PER_MOLE.value,
                ),
            ),
        ),
        (
            FlashPSInputs,
            dict(
                session_id="s1",
                compounds=["water", "ethanol"],
                composition=[1.0],
                pressure=build_measurement(
                    PhysicalQuantityTypes.PRESSURE,
                    101325.0,
                    PressureUnits.PASCAL.value,
                ),
                entropy=build_measurement(
                    PhysicalQuantityTypes.MOLAR_ENTROPY,
                    10.0,
                    MolarEntropyUnits.JOULE_PER_MOLE_KELVIN.value,
                ),
            ),
        ),
    ],
)
def test_flash_inputs_reject_mismatched_compounds_and_composition(model_cls, kwargs):
    with pytest.raises(ValueError):
        model_cls(**kwargs)


def test_flash_results_serialization():
    temperature = PhysicalProperties(
        name="Temperature",
        measurement=build_measurement(
            PhysicalQuantityTypes.TEMPERATURE,
            298.15,
            TemperatureUnits.KELVIN.value,
        ),
    )
    pressure = PhysicalProperties(
        name="Pressure",
        measurement=build_measurement(
            PhysicalQuantityTypes.PRESSURE,
            101325.0,
            PressureUnits.PASCAL.value,
        ),
    )
    phase = PhaseResults(
        phase_type=PhaseTypes.VAPOR,
        fraction=1.0,
        composition={"water": 1.0},
        properties=[],
    )
    result = FlashResults(
        calculation_type=FlashCalculationTypes.TEMPERATURE_PRESSURE,
        temperature=temperature,
        pressure=pressure,
        converged=True,
        phases=[phase],
        message=None,
        iteration_count=3,
    )

    data = result.model_dump()
    assert data["calculation_type"] == "TP"
    assert data["phases"][0]["phase_type"] == "Vapor"

    serialized = json.loads(result.model_dump_json())
    assert serialized["calculation_type"] == "TP"
    assert serialized["phases"][0]["phase_type"] == "Vapor"
