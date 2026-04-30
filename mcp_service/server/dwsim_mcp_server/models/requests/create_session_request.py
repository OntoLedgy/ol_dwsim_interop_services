# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""CreateSessionRequest DTO for MCP create_session tool."""

from typing import Optional
from pydantic import BaseModel, ConfigDict, Field


class CreateSessionRequest(BaseModel):
    """Request to create a new DWSIM simulation session."""

    name: Optional[str] = Field(
        None,
        description="Optional session name for identification"
    )

    temp_dir: Optional[str] = Field(
        None,
        description="Optional temporary directory path for session files"
    )

    timeout: Optional[int] = Field(
        3600,
        description="Session timeout in seconds (default: 3600)",
        ge=60,
        le=86400
    )

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "name": "Distillation Column Design",
                "timeout": 7200,
            }
        }
    )
