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

"""MaterialStream model implementing CAPE-OPEN ICapeThermoMaterialObject interface."""

from typing import Dict, List, Optional
from pydantic import BaseModel, ConfigDict, Field, field_validator


class MaterialStream(BaseModel):
    """
    Represents a material stream in a process flowsheet.

    Based on CAPE-OPEN ICapeThermoMaterialObject interface for cross-simulator
    interoperability.
    """

    name: str = Field(..., description="Stream name")
    temperature: Optional[float] = Field(
        None,
        description="Temperature in K",
        ge=0.0
    )
    pressure: Optional[float] = Field(
        None,
        description="Pressure in Pa",
        ge=0.0
    )
    molar_flow: Optional[float] = Field(
        None,
        description="Total molar flow in mol/s",
        ge=0.0
    )
    mass_flow: Optional[float] = Field(
        None,
        description="Total mass flow in kg/s",
        ge=0.0
    )
    vapor_fraction: Optional[float] = Field(
        None,
        description="Vapor mole fraction (0-1)",
        ge=0.0,
        le=1.0
    )

    composition: Dict[str, float] = Field(
        default_factory=dict,
        description="Component mole fractions {compound_id: fraction}"
    )

    phases: List[str] = Field(
        default_factory=list,
        description="Present phases: Vapor, Liquid, Solid"
    )

    properties: Dict[str, float] = Field(
        default_factory=dict,
        description="Additional stream properties {property_name: value}"
    )

    @field_validator("composition")
    @classmethod
    def validate_composition(cls, value: Dict[str, float]) -> Dict[str, float]:
        """Ensure composition fractions are valid and non-negative."""
        total = 0.0
        for key, fraction in value.items():
            if not key:
                raise ValueError("composition keys must be non-empty")
            if fraction < 0.0:
                raise ValueError(f"composition fraction for '{key}' must be >= 0")
            if fraction > 1.0:
                raise ValueError(f"composition fraction for '{key}' must be <= 1")
            total += fraction
        if total > 1.0 + 1e-6:
            raise ValueError("composition fractions must sum to <= 1")
        return value

    @field_validator("phases")
    @classmethod
    def validate_phases(cls, value: List[str]) -> List[str]:
        """Ensure phases are non-empty strings."""
        for phase in value:
            if not phase:
                raise ValueError("phase values must be non-empty strings")
        return value

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "name": "Feed Stream",
                "temperature": 298.15,
                "pressure": 101325.0,
                "molar_flow": 100.0,
                "vapor_fraction": 0.0,
                "composition": {
                    "water": 0.5,
                    "ethanol": 0.5,
                },
                "phases": ["Liquid"],
            }
        }
    )
