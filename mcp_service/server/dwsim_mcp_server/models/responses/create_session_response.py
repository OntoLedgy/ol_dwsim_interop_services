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

"""CreateSessionResponse DTO for MCP create_session tool."""

from pydantic import BaseModel, ConfigDict, Field


class CreateSessionResponse(BaseModel):
    """Response from creating a new DWSIM simulation session."""

    session_id: str = Field(..., description="Unique session identifier")
    name: str = Field(..., description="Session name")
    temp_dir: str = Field(..., description="Session temporary directory path")
    created_at: str = Field(..., description="Session creation timestamp (ISO 8601)")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "550e8400-e29b-41d4-a716-446655440000",
                "name": "Distillation Column Design",
                "temp_dir": "/tmp/dwsim_sessions/550e8400",
                "created_at": "2024-12-19T20:30:00Z",
            }
        }
    )
