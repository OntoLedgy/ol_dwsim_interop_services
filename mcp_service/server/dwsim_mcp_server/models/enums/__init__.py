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

"""Enumeration models for thermodynamics and flash calculations."""

from .flash_calculation_types import FlashCalculationTypes
from .physical_quantity_types import PhysicalQuantityTypes
from .phase_types import PhaseTypes
from .unit_symbols import (
    DensityUnits,
    MolarEnergyUnits,
    MolarEntropyUnits,
    PressureUnits,
    TemperatureUnits,
    ViscosityUnits,
)

__all__ = [
    "DensityUnits",
    "FlashCalculationTypes",
    "MolarEnergyUnits",
    "MolarEntropyUnits",
    "PhaseTypes",
    "PhysicalQuantityTypes",
    "PressureUnits",
    "TemperatureUnits",
    "ViscosityUnits",
]
