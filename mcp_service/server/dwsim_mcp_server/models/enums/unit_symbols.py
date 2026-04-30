# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Enumeration of unit symbols for physical quantities."""

from enum import Enum


class TemperatureUnits(str, Enum):
    """Temperature unit symbols."""

    KELVIN = "K"
    CELSIUS = "C"
    FAHRENHEIT = "F"
    RANKINE = "R"


class PressureUnits(str, Enum):
    """Pressure unit symbols."""

    PASCAL = "Pa"
    KILOPASCAL = "kPa"
    MEGAPASCAL = "MPa"
    BAR = "bar"
    ATMOSPHERE = "atm"
    PSI = "psi"


class MolarEnergyUnits(str, Enum):
    """Molar energy unit symbols (enthalpy, Gibbs energy)."""

    JOULE_PER_MOLE = "J/mol"
    KILOJOULE_PER_MOLE = "kJ/mol"
    BTU_PER_LBMOL = "BTU/lbmol"


class MolarEntropyUnits(str, Enum):
    """Molar entropy unit symbols."""

    JOULE_PER_MOLE_KELVIN = "J/(mol*K)"
    KILOJOULE_PER_MOLE_KELVIN = "kJ/(mol*K)"


class DensityUnits(str, Enum):
    """Density unit symbols."""

    KG_PER_CUBIC_METER = "kg/m3"
    G_PER_CUBIC_CM = "g/cm3"
    LB_PER_CUBIC_FT = "lb/ft3"


class ViscosityUnits(str, Enum):
    """Dynamic viscosity unit symbols."""

    PASCAL_SECOND = "Pa*s"
    CENTIPOISE = "cP"
    POISE = "P"
