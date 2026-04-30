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

"""AddUnitRequest DTO for MCP add_unit tool."""

from typing import Dict, Optional
import math
from pydantic import BaseModel, ConfigDict, Field, field_validator


class AddUnitRequest(BaseModel):
    """Request to add a unit operation to a flowsheet."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    unit_type: str = Field(..., description="Unit operation type", min_length=1)
    name: str = Field(..., description="Unit operation name", min_length=1)
    parameters: Dict[str, float] = Field(
        default_factory=dict,
        description="Unit parameters {parameter_name: value}"
    )
    description: Optional[str] = Field(None, description="Optional unit description")

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

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
                "unit_type": "separator",
                "name": "Three-Phase Separator",
                "parameters": {
                    "pressure_drop": 5000.0,
                },
            }
        }
    )
