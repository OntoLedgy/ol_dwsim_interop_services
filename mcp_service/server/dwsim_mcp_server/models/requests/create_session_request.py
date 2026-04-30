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
