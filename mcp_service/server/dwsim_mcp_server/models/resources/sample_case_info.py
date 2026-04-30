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

"""SampleCaseInfo model for sample simulation cases."""

from typing import List, Literal
from pydantic import BaseModel, ConfigDict, Field, field_validator


class SampleCaseInfo(BaseModel):
    """
    Information about a sample DWSIM simulation case.

    Used to describe available sample cases that agents can load and study.
    """

    name: str = Field(
        ...,
        description="Unique case identifier (e.g., three-phase-separator)",
    )
    description: str = Field(
        ...,
        description="Brief description of what the case demonstrates",
    )
    compounds: List[str] = Field(
        default_factory=list,
        description="List of compounds used in the simulation",
    )
    unit_operations: List[str] = Field(
        default_factory=list,
        description="List of unit operation types in the flowsheet",
    )
    complexity: Literal["simple", "moderate", "complex"] = Field(
        default="simple",
        description="Relative complexity level of the case",
    )
    file_path: str = Field(
        ...,
        description="Path to the DWSIM case file (.dwxmz)",
    )

    @field_validator("name")
    @classmethod
    def validate_name(cls, v: str) -> str:
        """Ensure name is a valid identifier."""
        if not v or not v.strip():
            raise ValueError("Case name cannot be empty")
        return v.strip().lower().replace(" ", "-")

    @field_validator("compounds")
    @classmethod
    def validate_compounds(cls, v: List[str]) -> List[str]:
        """Ensure compound names are non-empty."""
        return [c.strip() for c in v if c and c.strip()]

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "name": "three-phase-separator",
                "description": "Simple three-phase separator with hydrocarbon and water mixture",
                "compounds": ["Methane", "Ethane", "Propane", "Water"],
                "unit_operations": ["ThreePhaseSeparator"],
                "complexity": "simple",
                "file_path": "cases/samples/three-phase-separator.dwxmz",
            }
        }
    )
