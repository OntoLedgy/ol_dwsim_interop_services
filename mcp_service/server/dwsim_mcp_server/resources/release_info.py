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
    "this running dwsim-mcp-server instance."
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
