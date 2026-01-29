import pytest

from dwsim_mcp_server.models.cape_open.material_stream import MaterialStream
from dwsim_mcp_server.models.cape_open.thermo_property_package import ThermoPropertyPackage
from dwsim_mcp_server.models.cape_open.unit_operation import UnitOperation


def test_material_stream_valid_composition():
    stream = MaterialStream(
        name="Feed",
        temperature=298.15,
        pressure=101325.0,
        molar_flow=10.0,
        composition={"water": 0.5, "ethanol": 0.5},
        phases=["Liquid"],
    )
    assert stream.composition["water"] == pytest.approx(0.5)


def test_material_stream_invalid_composition_sum():
    with pytest.raises(ValueError):
        MaterialStream(
            name="Feed",
            composition={"water": 0.6, "ethanol": 0.6},
        )


def test_property_package_requires_finite_parameters():
    with pytest.raises(ValueError):
        ThermoPropertyPackage(
            name="PR",
            parameters={"kij": float("nan")},
        )


def test_unit_operation_rejects_invalid_connections():
    with pytest.raises(ValueError):
        UnitOperation(
            id="unit-1",
            name="Separator",
            unit_type="separator",
            connections={"inlet": ""},
        )
