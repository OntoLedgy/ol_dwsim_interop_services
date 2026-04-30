# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Enumeration of flash calculation types."""

from enum import Enum


class FlashCalculationTypes(str, Enum):
    """Types of thermodynamic flash calculations."""

    TEMPERATURE_PRESSURE = "TP"
    PRESSURE_ENTHALPY = "PH"
    PRESSURE_ENTROPY = "PS"
    TEMPERATURE_VAPOR_FRACTION = "TVF"
    PRESSURE_VAPOR_FRACTION = "PVF"
