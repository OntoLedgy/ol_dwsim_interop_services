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

"""GetStatusRequest DTO for MCP get_status tool."""

from pydantic import BaseModel, ConfigDict, Field, field_validator


class GetStatusRequest(BaseModel):
    """Request to retrieve simulation status."""

    session_id: str = Field(
        ...,
        description="Session identifier (letters, numbers, '.', '-', '_')",
        min_length=1,
    )

    @field_validator("session_id")
    @classmethod
    def validate_session_id(cls, value: str) -> str:
        """Ensure session_id is non-empty and contains only safe characters."""
        cleaned = value.strip()
        if not cleaned:
            raise ValueError("session_id must not be empty")
        for char in cleaned:
            if not (char.isalnum() or char in {".", "-", "_"}):
                raise ValueError("session_id contains invalid characters")
        return cleaned

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
            }
        }
    )
