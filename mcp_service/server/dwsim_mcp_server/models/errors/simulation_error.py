# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""SimulationError model for simulation-related failures."""

from typing import List, Optional
from pydantic import BaseModel, ConfigDict, Field


class SimulationError(BaseModel):
    """Error related to simulation execution and result retrieval."""

    code: str = Field(
        ...,
        description=(
            "Error code (e.g., CONVERGENCE_FAILURE, SIMULATION_TIMEOUT, "
            "INVALID_FLOWSHEET_STATE, NO_RESULTS_AVAILABLE)"
        ),
    )
    message: str = Field(..., description="Human-readable error message")
    session_id: Optional[str] = Field(None, description="Session ID if applicable")
    solver_messages: Optional[List[str]] = Field(
        None,
        description="Solver diagnostic messages, if available",
    )
    failed_unit_id: Optional[str] = Field(
        None,
        description="Unit operation ID that failed, if known",
    )
    convergence_state: Optional[str] = Field(
        None,
        description="Convergence state when failure occurred",
    )
    details: Optional[dict] = Field(None, description="Additional error details")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "code": "CONVERGENCE_FAILURE",
                "message": "Calculation did not converge for separator SEP-101.",
                "session_id": "session-1234",
                "solver_messages": [
                    "SEP-101: Flash calculation failed to converge.",
                ],
                "failed_unit_id": "SEP-101",
                "convergence_state": "NotConverged",
            }
        }
    )
