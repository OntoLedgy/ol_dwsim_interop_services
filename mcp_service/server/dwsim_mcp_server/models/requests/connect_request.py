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

"""ConnectRequest DTO for MCP connect tool."""

from pydantic import BaseModel, ConfigDict, Field


class ConnectRequest(BaseModel):
    """Request to connect two objects in a flowsheet."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    source_id: str = Field(..., description="Source object identifier", min_length=1)
    target_id: str = Field(..., description="Target object identifier", min_length=1)
    port_name: str = Field(..., description="Port name to connect", min_length=1)

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
                "source_id": "stream-feed",
                "target_id": "unit-sep-001",
                "port_name": "inlet",
            }
        }
    )
