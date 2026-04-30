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

"""UnitOperation model implementing CAPE-OPEN unit operation metadata."""

from typing import Dict, Optional
import math
from pydantic import BaseModel, ConfigDict, Field, field_validator


class UnitOperation(BaseModel):
    """Represents a unit operation in a process flowsheet."""

    id: str = Field(..., description="Unit operation unique identifier", min_length=1)
    name: str = Field(..., description="Unit operation name", min_length=1)
    unit_type: str = Field(..., description="Unit operation type", min_length=1)
    parameters: Dict[str, float] = Field(
        default_factory=dict,
        description="Unit parameters {parameter_name: value}"
    )
    connections: Dict[str, str] = Field(
        default_factory=dict,
        description="Port connections {port_name: object_id}"
    )
    description: Optional[str] = Field(None, description="Optional description")

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

    @field_validator("connections")
    @classmethod
    def validate_connections(cls, value: Dict[str, str]) -> Dict[str, str]:
        """Ensure connection keys and values are non-empty strings."""
        for key, target in value.items():
            if not key:
                raise ValueError("connection keys must be non-empty")
            if not target:
                raise ValueError(f"connection '{key}' must reference a non-empty object id")
        return value

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "id": "sep-001",
                "name": "Three-Phase Separator",
                "unit_type": "separator",
                "parameters": {
                    "pressure_drop": 5000.0,
                },
                "connections": {
                    "inlet": "stream-feed",
                    "vapor_out": "stream-vapor",
                    "liquid_out": "stream-liquid",
                },
            }
        }
    )
