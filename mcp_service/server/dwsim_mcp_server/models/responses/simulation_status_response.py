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

"""SimulationStatusResponse DTO for MCP simulation status."""

from typing import Optional
from pydantic import BaseModel, ConfigDict, Field


class SimulationStatusResponse(BaseModel):
    """Response containing current simulation status."""

    status: str = Field(
        ...,
        description="Status: idle, running, converged, failed, timeout",
    )
    is_running: bool = Field(..., description="Whether a calculation is currently running")
    last_run_timestamp: Optional[str] = Field(
        None,
        description="Last calculation timestamp (ISO 8601)",
    )
    elapsed_ms: Optional[float] = Field(
        None,
        description="Elapsed time in milliseconds if running",
    )

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "status": "running",
                "is_running": True,
                "last_run_timestamp": "2024-12-19T20:30:00Z",
                "elapsed_ms": 450.0,
            }
        }
    )
