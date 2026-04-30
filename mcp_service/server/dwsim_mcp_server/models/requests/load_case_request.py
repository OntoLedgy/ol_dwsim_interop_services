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

"""LoadCaseRequest DTO for MCP load_case tool."""

from pydantic import BaseModel, ConfigDict, Field


class LoadCaseRequest(BaseModel):
    """Request to load a DWSIM flowsheet case from disk."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    file_path: str = Field(..., description="Source file path for the case", min_length=1)

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
                "file_path": "C:/dwsim/cases/separator.dwxml",
            }
        }
    )
