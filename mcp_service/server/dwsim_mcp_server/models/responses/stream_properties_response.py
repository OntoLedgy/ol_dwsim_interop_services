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

"""StreamPropertiesResponse DTO for MCP simulation results."""

from typing import Any, Dict, List
from pydantic import BaseModel, ConfigDict, Field


class StreamPropertiesResponse(BaseModel):
    """CAPE-OPEN stream properties in SI units."""

    id: str = Field(..., description="Stream identifier")
    name: str = Field(..., description="Stream name")
    temperature_k: float = Field(..., description="Temperature in Kelvin")
    pressure_pa: float = Field(..., description="Pressure in Pascals")
    total_molar_flow_mol_per_s: float = Field(..., description="Total molar flow in mol/s")
    phases: List[Dict[str, Any]] = Field(
        default_factory=list,
        description="Phase data entries matching CAPE-OPEN phase DTOs",
    )

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "id": "stream-001",
                "name": "Vapor Outlet",
                "temperature_k": 350.0,
                "pressure_pa": 101325.0,
                "total_molar_flow_mol_per_s": 10.5,
                "phases": [
                    {
                        "phase_label": "Vapor",
                        "phase_fraction": 1.0,
                        "composition": [
                            {"compound": "methane", "mole_fraction": 0.7},
                            {"compound": "ethane", "mole_fraction": 0.3},
                        ],
                    }
                ],
            }
        }
    )
