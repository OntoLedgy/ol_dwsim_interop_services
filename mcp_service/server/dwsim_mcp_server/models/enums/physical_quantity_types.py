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
