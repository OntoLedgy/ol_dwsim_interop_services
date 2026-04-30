# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
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
