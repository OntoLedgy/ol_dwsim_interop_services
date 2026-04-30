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

"""ThermoPropertyPackage model implementing CAPE-OPEN property package metadata."""

from typing import Dict, Optional
import math
from pydantic import BaseModel, ConfigDict, Field, field_validator


class ThermoPropertyPackage(BaseModel):
    """Represents a thermodynamic property package configuration."""

    name: str = Field(..., description="Property package name", min_length=1)
    description: Optional[str] = Field(None, description="Optional description")
    parameters: Dict[str, float] = Field(
        default_factory=dict,
        description="Numeric parameters {parameter_name: value}"
    )
    options: Dict[str, str] = Field(
        default_factory=dict,
        description="String options {option_name: value}"
    )

    @field_validator("parameters")
    @classmethod
    def validate_parameters(cls, value: Dict[str, float]) -> Dict[str, float]:
        """Ensure parameter keys are non-empty and values are finite."""
        for key, number in value.items():
            if not key:
                raise ValueError("parameter keys must be non-empty")
            if not math.isfinite(number):
                raise ValueError(f"parameter '{key}' must be a finite number")
        return value

    @field_validator("options")
    @classmethod
    def validate_options(cls, value: Dict[str, str]) -> Dict[str, str]:
        """Ensure option keys and values are non-empty strings."""
        for key, option_value in value.items():
            if not key:
                raise ValueError("option keys must be non-empty")
            if not option_value:
                raise ValueError(f"option '{key}' must be a non-empty string")
        return value

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "name": "Peng-Robinson",
                "description": "Peng-Robinson EOS property package",
                "parameters": {
                    "binary_interaction_kij": 0.0,
                },
                "options": {
                    "mixing_rule": "van_der_waals",
                },
            }
        }
    )
