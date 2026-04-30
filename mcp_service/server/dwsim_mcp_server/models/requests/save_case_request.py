# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""SaveCaseRequest DTO for MCP save_case tool."""

from pydantic import BaseModel, ConfigDict, Field


class SaveCaseRequest(BaseModel):
    """Request to save a DWSIM flowsheet case to disk."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    file_path: str = Field(..., description="Destination file path for the case", min_length=1)

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
                "file_path": "C:/dwsim/cases/separator.dwxml",
            }
        }
    )
