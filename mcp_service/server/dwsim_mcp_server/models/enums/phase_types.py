"""Enumeration of thermodynamic phase types."""

from enum import Enum


class PhaseTypes(str, Enum):
    """Types of thermodynamic phases in equilibrium."""

    VAPOR = "Vapor"
    LIQUID = "Liquid"
    LIQUID2 = "Liquid2"
    AQUEOUS = "Aqueous"
    SOLID = "Solid"
