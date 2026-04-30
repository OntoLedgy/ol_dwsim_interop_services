# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""CloseSessionRequest DTO for MCP close_session tool."""

from pydantic import BaseModel, ConfigDict, Field


class CloseSessionRequest(BaseModel):
    """Request to close an existing DWSIM simulation session."""

    session_id: str = Field(
        ...,
        description="Session identifier to close",
        min_length=1
    )

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
            }
        }
    )
