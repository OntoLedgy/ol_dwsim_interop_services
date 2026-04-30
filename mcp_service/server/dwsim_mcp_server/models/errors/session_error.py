# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""SessionError model for session-related errors."""

from typing import Optional
from pydantic import BaseModel, ConfigDict, Field


class SessionError(BaseModel):
    """Error related to session management operations."""

    code: str = Field(..., description="Error code (e.g., SESSION_NOT_FOUND, MAX_SESSIONS_EXCEEDED)")
    message: str = Field(..., description="Human-readable error message")
    session_id: Optional[str] = Field(None, description="Session ID if applicable")
    details: Optional[dict] = Field(None, description="Additional error details")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "code": "SESSION_NOT_FOUND",
                "message": "Session with ID '550e8400-e29b-41d4-a716-446655440000' does not exist",
                "session_id": "550e8400-e29b-41d4-a716-446655440000",
            }
        }
    )
