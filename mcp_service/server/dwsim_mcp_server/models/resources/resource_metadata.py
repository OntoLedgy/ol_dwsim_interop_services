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

"""ResourceMetadata model for MCP resource discovery."""

from pydantic import BaseModel, ConfigDict, Field


class ResourceMetadata(BaseModel):
    """
    Metadata describing an MCP resource.

    Used for resource listing and discovery by LLM agents.
    """

    uri: str = Field(
        ...,
        description="Unique resource URI (e.g., resource://docs/unit-operations)",
    )
    name: str = Field(
        ...,
        description="Human-readable resource name",
    )
    description: str = Field(
        ...,
        description="Brief description of the resource content",
    )
    mime_type: str = Field(
        default="application/json",
        description="Content MIME type (application/json or text/markdown)",
    )

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "uri": "resource://docs/unit-operations",
                "name": "Unit Operations Documentation",
                "description": "Reference guide for DWSIM unit operations and their parameters",
                "mime_type": "text/markdown",
            }
        }
    )
