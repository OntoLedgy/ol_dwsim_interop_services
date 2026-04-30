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

"""ResourceLimitError model for limit violations."""

from typing import Optional
from pydantic import BaseModel, ConfigDict, Field


class ResourceLimitError(BaseModel):
    """Error related to resource limit enforcement."""

    code: str = Field(..., description="Error code (e.g., RESOURCE_LIMIT_EXCEEDED, TIMEOUT)")
    message: str = Field(..., description="Human-readable error message")
    session_id: Optional[str] = Field(None, description="Session ID if applicable")
    details: Optional[dict] = Field(None, description="Additional error details")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "code": "TIMEOUT",
                "message": "Operation timed out after 300 seconds.",
                "session_id": "550e8400-e29b-41d4-a716-446655440000",
                "details": {"timeout_seconds": 300, "elapsed_seconds": 305.2},
            }
        }
    )
