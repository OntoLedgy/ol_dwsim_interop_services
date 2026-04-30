# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Enumeration of physical quantity types."""

from enum import Enum


class PhysicalQuantityTypes(str, Enum):
    """Types of physical quantities that can be measured."""

    TEMPERATURE = "Temperature"
    PRESSURE = "Pressure"
    MOLAR_ENTHALPY = "MolarEnthalpy"
    MOLAR_ENTROPY = "MolarEntropy"
    DENSITY = "Density"
    DYNAMIC_VISCOSITY = "DynamicViscosity"
    THERMAL_CONDUCTIVITY = "ThermalConductivity"
    MOLAR_HEAT_CAPACITY_CP = "MolarHeatCapacityCp"
    MOLAR_HEAT_CAPACITY_CV = "MolarHeatCapacityCv"
    MOLECULAR_WEIGHT = "MolecularWeight"
    COMPRESSIBILITY_FACTOR = "CompressibilityFactor"
    GIBBS_ENERGY = "GibbsEnergy"
    SURFACE_TENSION = "SurfaceTension"
    MOLAR_FLOW_RATE = "MolarFlowRate"
    MASS_FLOW_RATE = "MassFlowRate"
    VOLUMETRIC_FLOW_RATE = "VolumetricFlowRate"
