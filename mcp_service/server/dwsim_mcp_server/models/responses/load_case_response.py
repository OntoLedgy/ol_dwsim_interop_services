# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""LoadCaseResponse DTO for MCP load_case tool."""

from pydantic import BaseModel, ConfigDict, Field


class LoadCaseResponse(BaseModel):
    """Response from loading a DWSIM case."""

    session_id: str = Field(..., description="Session identifier")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
            }
        }
    )
