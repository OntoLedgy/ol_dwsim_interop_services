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

"""Release metadata MCP resource helpers."""

from __future__ import annotations

import json

from mcp import types
from pydantic import AnyUrl

from dwsim_mcp_server.release_info import get_release_info

RELEASE_INFO_URI = "release://info"
RELEASE_INFO_NAME = "Release Information"
RELEASE_INFO_DESCRIPTION = (
    "Get the package, version, commit SHA, source URL, and license for "
    "this running ol-dwsim-mcp-server instance."
)
RELEASE_INFO_MIME_TYPE = "application/json"


def register_release_info_resource(server) -> None:
    @server.resource(
        RELEASE_INFO_URI,
        name=RELEASE_INFO_NAME,
        description=RELEASE_INFO_DESCRIPTION,
        mime_type=RELEASE_INFO_MIME_TYPE,
    )
    async def release_information() -> str:
        return json.dumps(get_release_info(), indent=2)


def get_release_info_resource() -> types.Resource:
    return types.Resource(
        uri=AnyUrl(RELEASE_INFO_URI),
        name=RELEASE_INFO_NAME,
        description=RELEASE_INFO_DESCRIPTION,
        mimeType=RELEASE_INFO_MIME_TYPE,
    )


def get_release_info_result() -> types.ReadResourceResult:
    return types.ReadResourceResult(
        contents=[
            types.TextResourceContents(
                uri=AnyUrl(RELEASE_INFO_URI),
                text=json.dumps(get_release_info(), indent=2),
                mimeType=RELEASE_INFO_MIME_TYPE,
            )
        ]
    )
