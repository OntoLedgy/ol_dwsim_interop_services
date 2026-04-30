# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

import httpx
import pytest
from mcp.server import FastMCP
from mcp.server.auth.provider import AccessToken
from mcp.server.auth.settings import AuthSettings

pytestmark = pytest.mark.integration


class DummyTokenVerifier:
    async def verify_token(self, token: str):
        if token != "good":
            return None
        return AccessToken(
            token=token,
            client_id="test-client",
            scopes=["user"],
            expires_at=None,
        )


def _auth_settings() -> AuthSettings:
    return AuthSettings(
        issuer_url="https://issuer.example",
        resource_server_url="http://testserver/mcp",
        required_scopes=["user"],
    )


def _server_with_auth() -> FastMCP:
    mcp = FastMCP(
        "test-mcp",
        auth=_auth_settings(),
        token_verifier=DummyTokenVerifier(),
    )

    @mcp.tool()
    def ping(message: str) -> dict[str, str]:
        return {"message": message}

    return mcp


@pytest.mark.asyncio
async def test_oauth_discovery_endpoint_available():
    server = _server_with_auth()
    async with httpx.AsyncClient(app=server.streamable_http_app(), base_url="http://testserver") as client:
        response = await client.get("/.well-known/oauth-protected-resource")
    assert response.status_code == 200
    assert "authorization_servers" in response.json()


@pytest.mark.asyncio
async def test_unauthenticated_request_rejected():
    server = _server_with_auth()
    async with httpx.AsyncClient(app=server.streamable_http_app(), base_url="http://testserver") as client:
        response = await client.post("/mcp", json={"jsonrpc": "2.0", "id": 1, "method": "list_tools"})
    assert response.status_code == 401


@pytest.mark.asyncio
async def test_authenticated_request_allowed():
    server = _server_with_auth()
    headers = {"Authorization": "Bearer good"}
    async with httpx.AsyncClient(app=server.streamable_http_app(), base_url="http://testserver") as client:
        response = await client.post(
            "/mcp",
            headers=headers,
            json={"jsonrpc": "2.0", "id": 1, "method": "list_tools"},
        )
    assert response.status_code != 401


def test_stdio_bypasses_auth():
    server = _server_with_auth()
    result = server.call_tool("ping", {"message": "ok"})
    assert result == {"message": "ok"}
