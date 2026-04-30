# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# This file is part of the OntoLedgy Thermodynamics Architecture and is
# dual-licensed:
#
#   1. Open source under the GNU Affero General Public License v3.0 or
#      later (AGPL-3.0-or-later). See the LICENSE file in the repository
#      root for the full licence text and NOTICE for attribution.
#   2. Commercial under a separate proprietary licence offered by
#      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

import json

import pytest

from dwsim_mcp_server.models.enums.flash_calculation_types import FlashCalculationTypes
from dwsim_mcp_server.models.enums.phase_types import PhaseTypes
from dwsim_mcp_server.models.enums.physical_quantity_types import PhysicalQuantityTypes
from dwsim_mcp_server.models.enums.unit_symbols import (
    DensityUnits,
    MolarEnergyUnits,
    MolarEntropyUnits,
    PressureUnits,
    TemperatureUnits,
    ViscosityUnits,
)


@pytest.mark.parametrize(
    ("enum_member", "expected_value"),
    [
        (FlashCalculationTypes.TEMPERATURE_PRESSURE, "TP"),
        (FlashCalculationTypes.PRESSURE_ENTHALPY, "PH"),
        (FlashCalculationTypes.PRESSURE_ENTROPY, "PS"),
        (FlashCalculationTypes.TEMPERATURE_VAPOR_FRACTION, "TVF"),
        (FlashCalculationTypes.PRESSURE_VAPOR_FRACTION, "PVF"),
    ],
)
def test_flash_calculation_types_values(enum_member, expected_value):
    assert enum_member.value == expected_value
    assert enum_member == expected_value
    assert json.dumps(enum_member) == f"\"{expected_value}\""


@pytest.mark.parametrize(
    ("enum_member", "expected_value"),
    [
        (PhaseTypes.VAPOR, "Vapor"),
        (PhaseTypes.LIQUID, "Liquid"),
        (PhaseTypes.LIQUID2, "Liquid2"),
        (PhaseTypes.AQUEOUS, "Aqueous"),
        (PhaseTypes.SOLID, "Solid"),
    ],
)
def test_phase_types_values(enum_member, expected_value):
    assert enum_member.value == expected_value
    assert enum_member == expected_value
    assert json.dumps(enum_member) == f"\"{expected_value}\""


@pytest.mark.parametrize(
    ("enum_member", "expected_value"),
    [
        (PhysicalQuantityTypes.TEMPERATURE, "Temperature"),
        (PhysicalQuantityTypes.PRESSURE, "Pressure"),
        (PhysicalQuantityTypes.MOLAR_ENTHALPY, "MolarEnthalpy"),
        (PhysicalQuantityTypes.MOLAR_ENTROPY, "MolarEntropy"),
        (PhysicalQuantityTypes.DENSITY, "Density"),
        (PhysicalQuantityTypes.DYNAMIC_VISCOSITY, "DynamicViscosity"),
        (PhysicalQuantityTypes.THERMAL_CONDUCTIVITY, "ThermalConductivity"),
        (PhysicalQuantityTypes.MOLAR_HEAT_CAPACITY_CP, "MolarHeatCapacityCp"),
        (PhysicalQuantityTypes.MOLAR_HEAT_CAPACITY_CV, "MolarHeatCapacityCv"),
        (PhysicalQuantityTypes.MOLECULAR_WEIGHT, "MolecularWeight"),
        (PhysicalQuantityTypes.COMPRESSIBILITY_FACTOR, "CompressibilityFactor"),
        (PhysicalQuantityTypes.GIBBS_ENERGY, "GibbsEnergy"),
        (PhysicalQuantityTypes.SURFACE_TENSION, "SurfaceTension"),
        (PhysicalQuantityTypes.MOLAR_FLOW_RATE, "MolarFlowRate"),
        (PhysicalQuantityTypes.MASS_FLOW_RATE, "MassFlowRate"),
        (PhysicalQuantityTypes.VOLUMETRIC_FLOW_RATE, "VolumetricFlowRate"),
    ],
)
def test_physical_quantity_types_values(enum_member, expected_value):
    assert enum_member.value == expected_value
    assert enum_member == expected_value
    assert json.dumps(enum_member) == f"\"{expected_value}\""


@pytest.mark.parametrize(
    ("enum_class", "expected_values"),
    [
        (TemperatureUnits, ["K", "C", "F", "R"]),
        (PressureUnits, ["Pa", "kPa", "MPa", "bar", "atm", "psi"]),
        (MolarEnergyUnits, ["J/mol", "kJ/mol", "BTU/lbmol"]),
        (MolarEntropyUnits, ["J/(mol*K)", "kJ/(mol*K)"]),
        (DensityUnits, ["kg/m3", "g/cm3", "lb/ft3"]),
        (ViscosityUnits, ["Pa*s", "cP", "P"]),
    ],
)
def test_unit_enums_values_and_serialization(enum_class, expected_values):
    assert [member.value for member in enum_class] == expected_values
    for expected in expected_values:
        member = enum_class(expected)
        assert member == expected
        assert json.dumps(member) == f"\"{expected}\""
