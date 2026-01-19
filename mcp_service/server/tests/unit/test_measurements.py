import pytest

from models.enums.physical_quantity_types import PhysicalQuantityTypes
from models.measurements.measurements import Measurements
from models.measurements.physical_properties import PhysicalProperties
from models.measurements.ranges import Ranges
from models.measurements.units_of_measure import UnitsOfMeasure


@pytest.mark.parametrize(
    ("value", "expected"),
    [
        (0.0, True),
        (5.0, True),
        (10.0, True),
        (-1.0, False),
        (10.1, False),
    ],
)
def test_ranges_contains(value, expected):
    ranges = Ranges(min_value=0.0, max_value=10.0)
    assert ranges.contains(value) is expected


def test_units_of_measure_is_value_valid_without_range():
    unit = UnitsOfMeasure(
        unit_name="K",
        quantity_type=PhysicalQuantityTypes.TEMPERATURE,
        valid_range=None,
    )
    assert unit.is_value_valid(-10.0) is True


def test_units_of_measure_is_value_valid_with_range():
    unit = UnitsOfMeasure(
        unit_name="K",
        quantity_type=PhysicalQuantityTypes.TEMPERATURE,
        valid_range=Ranges(min_value=250.0, max_value=500.0),
    )
    assert unit.is_value_valid(300.0) is True
    assert unit.is_value_valid(100.0) is False


def test_measurements_requires_matching_unit_quantity():
    unit = UnitsOfMeasure(
        unit_name="K",
        quantity_type=PhysicalQuantityTypes.TEMPERATURE,
        valid_range=None,
    )
    measurement = Measurements(
        quantity_type=PhysicalQuantityTypes.TEMPERATURE,
        value=298.15,
        unit=unit,
    )
    assert measurement.unit.unit_name == "K"


def test_measurements_rejects_mismatched_unit_quantity():
    unit = UnitsOfMeasure(
        unit_name="Pa",
        quantity_type=PhysicalQuantityTypes.PRESSURE,
        valid_range=None,
    )
    with pytest.raises(ValueError):
        Measurements(
            quantity_type=PhysicalQuantityTypes.TEMPERATURE,
            value=298.15,
            unit=unit,
        )


def test_physical_properties_accessors():
    unit = UnitsOfMeasure(
        unit_name="Pa",
        quantity_type=PhysicalQuantityTypes.PRESSURE,
        valid_range=None,
    )
    measurement = Measurements(
        quantity_type=PhysicalQuantityTypes.PRESSURE,
        value=101325.0,
        unit=unit,
    )
    prop = PhysicalProperties(name="Pressure", measurement=measurement)
    assert prop.value == 101325.0
    assert prop.unit_name == "Pa"


def test_physical_properties_accessors_without_measurement():
    prop = PhysicalProperties(name="Empty", measurement=None)
    assert prop.value is None
    assert prop.unit_name is None
