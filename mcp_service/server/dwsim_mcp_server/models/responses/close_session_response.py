# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""CloseSessionResponse DTO for MCP close_session tool."""

from pydantic import BaseModel, ConfigDict, Field


class CloseSessionResponse(BaseModel):
    """Response from closing a DWSIM simulation session."""

    success: bool = Field(..., description="Whether the session was closed")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "success": True,
            }
        }
    )
