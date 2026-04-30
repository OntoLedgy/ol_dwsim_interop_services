# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Enumeration of thermodynamic phase types."""

from enum import Enum


class PhaseTypes(str, Enum):
    """Types of thermodynamic phases in equilibrium."""

    VAPOR = "Vapor"
    LIQUID = "Liquid"
    LIQUID2 = "Liquid2"
    AQUEOUS = "Aqueous"
    SOLID = "Solid"
