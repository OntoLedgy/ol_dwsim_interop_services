# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

import pytest

from dwsim_mcp_server.converters import pythonnet_dto_converter as converter
from dwsim_mcp_server.models.cape_open.material_stream import MaterialStream
from dwsim_mcp_server.models.cape_open.thermo_property_package import ThermoPropertyPackage
from dwsim_mcp_server.models.cape_open.unit_operation import UnitOperation


class FakeCompoundFractionDto:
    def __init__(self):
        self.Compound = None
        self.MoleFraction = None


class FakePhaseDto:
    def __init__(self):
        self.PhaseLabel = None
        self.PhaseFraction = None
        self.Composition = []


class FakeMaterialStreamDto:
    def __init__(self):
        self.Id = None
        self.Name = None
        self.TemperatureK = None
        self.PressurePa = None
        self.TotalMolarFlowMolPerS = None
        self.Phases = []


class FakePropertyPackageDto:
    def __init__(self):
        self.Id = None
        self.Name = None
        self.PackageType = None
        self.Parameters = {}


class FakeUnitOperationDto:
    def __init__(self):
        self.Id = None
        self.Name = None
        self.UnitType = None
        self.Parameters = {}


def _fake_dto_types():
    return {
        "CompoundFractionDto": FakeCompoundFractionDto,
        "MaterialStreamDto": FakeMaterialStreamDto,
        "PhaseDto": FakePhaseDto,
        "PropertyPackageDto": FakePropertyPackageDto,
        "UnitOperationDto": FakeUnitOperationDto,
    }


def test_to_csharp_material_stream_requires_composition(monkeypatch):
    monkeypatch.setattr(converter, "_load_csharp_dto_types", _fake_dto_types)
    stream = MaterialStream(name="Feed")
    with pytest.raises(ValueError):
        converter.to_csharp_material_stream(stream)


def test_to_csharp_material_stream_maps_fields(monkeypatch):
    monkeypatch.setattr(converter, "_load_csharp_dto_types", _fake_dto_types)
    stream = MaterialStream(
        name="Feed",
        temperature=300.0,
        pressure=101325.0,
        molar_flow=10.0,
        composition={"water": 1.0},
        phases=["Liquid"],
    )
    dto = converter.to_csharp_material_stream(stream)
    assert dto.Name == "Feed"
    assert dto.TemperatureK == pytest.approx(300.0)
    assert dto.Phases[0].Composition[0].Compound == "water"


def test_from_csharp_material_stream_maps_first_phase():
    phase = FakePhaseDto()
    compound = FakeCompoundFractionDto()
    compound.Compound = "ethanol"
    compound.MoleFraction = 0.3
    phase.PhaseLabel = "Liquid"
    phase.Composition = [compound]

    dto = FakeMaterialStreamDto()
    dto.Name = "Stream-1"
    dto.TemperatureK = 298.15
    dto.PressurePa = 101325.0
    dto.TotalMolarFlowMolPerS = 5.0
    dto.Phases = [phase]

    model = converter.from_csharp_material_stream(dto)
    assert model.name == "Stream-1"
    assert model.composition["ethanol"] == pytest.approx(0.3)


def test_to_csharp_property_package_requires_package_type(monkeypatch):
    monkeypatch.setattr(converter, "_load_csharp_dto_types", _fake_dto_types)
    model = ThermoPropertyPackage(name="PR", options={})
    with pytest.raises(ValueError):
        converter.to_csharp_property_package(model)


def test_to_csharp_property_package_maps_parameters(monkeypatch):
    monkeypatch.setattr(converter, "_load_csharp_dto_types", _fake_dto_types)
    model = ThermoPropertyPackage(
        name="PR",
        options={"package_type": "PengRobinson"},
        parameters={"kij": 0.0},
    )
    dto = converter.to_csharp_property_package(model)
    assert dto.PackageType == "PengRobinson"
    assert dto.Parameters["kij"] == "0.0"


def test_unit_operation_round_trip(monkeypatch):
    monkeypatch.setattr(converter, "_load_csharp_dto_types", _fake_dto_types)
    model = UnitOperation(
        id="unit-1",
        name="Separator",
        unit_type="separator",
        parameters={"pressure_drop": 100.0},
    )
    dto = converter.to_csharp_unit_operation(model)
    dto.Parameters["pressure_drop"] = 200.0
    new_model = converter.from_csharp_unit_operation(dto)
    assert new_model.parameters["pressure_drop"] == pytest.approx(200.0)
