import asyncio
from types import SimpleNamespace

import pytest
from jwt import ExpiredSignatureError, InvalidTokenError

from dwsim_mcp_server.auth.clerk_verifier import ClerkTokenVerifier
from dwsim_mcp_server.auth.settings import AuthConfig


class FakePyJWKClient:
    instances: list["FakePyJWKClient"] = []

    def __init__(self, url: str) -> None:
        self.url = url
        self.instances.append(self)

    def get_signing_key_from_jwt(self, token: str):
        return SimpleNamespace(key="public-key")


def _config() -> AuthConfig:
    return AuthConfig(
        enabled=True,
        issuer_url="https://issuer.example",
        jwks_url="https://jwks.example/.well-known/jwks.json",
        audience="client-id",
        required_scopes=["user"],
    )


def _patch_time(monkeypatch, times: list[float]) -> None:
    iterator = iter(times)
    monkeypatch.setattr(
        "dwsim_mcp_server.auth.clerk_verifier.time.time",
        lambda: next(iterator),
    )


def test_verify_token_valid(monkeypatch):
    monkeypatch.setattr(
        "dwsim_mcp_server.auth.clerk_verifier.PyJWKClient",
        FakePyJWKClient,
    )
    monkeypatch.setattr(
        "dwsim_mcp_server.auth.clerk_verifier.jwt.decode",
        lambda *args, **kwargs: {"scope": "user write", "exp": 123, "aud": "client-id"},
    )

    verifier = ClerkTokenVerifier(_config())
    token = asyncio.run(verifier.verify_token("token"))

    assert token is not None
    assert token.scopes == ["user", "write"]
    assert token.expires_at == 123
    assert token.client_id == "client-id"


def test_verify_token_expired(monkeypatch):
    monkeypatch.setattr(
        "dwsim_mcp_server.auth.clerk_verifier.PyJWKClient",
        FakePyJWKClient,
    )

    def _raise_expired(*args, **kwargs):
        raise ExpiredSignatureError("expired")

    monkeypatch.setattr("dwsim_mcp_server.auth.clerk_verifier.jwt.decode", _raise_expired)
    verifier = ClerkTokenVerifier(_config())

    token = asyncio.run(verifier.verify_token("token"))
    assert token is None


def test_verify_token_invalid_signature(monkeypatch):
    monkeypatch.setattr(
        "dwsim_mcp_server.auth.clerk_verifier.PyJWKClient",
        FakePyJWKClient,
    )

    def _raise_invalid(*args, **kwargs):
        raise InvalidTokenError("invalid")

    monkeypatch.setattr("dwsim_mcp_server.auth.clerk_verifier.jwt.decode", _raise_invalid)
    verifier = ClerkTokenVerifier(_config())

    token = asyncio.run(verifier.verify_token("token"))
    assert token is None


def test_verify_token_missing_scopes(monkeypatch):
    monkeypatch.setattr(
        "dwsim_mcp_server.auth.clerk_verifier.PyJWKClient",
        FakePyJWKClient,
    )
    monkeypatch.setattr(
        "dwsim_mcp_server.auth.clerk_verifier.jwt.decode",
        lambda *args, **kwargs: {"scope": "read"},
    )

    verifier = ClerkTokenVerifier(_config())
    token = asyncio.run(verifier.verify_token("token"))

    assert token is None


def test_jwks_caching(monkeypatch):
    FakePyJWKClient.instances = []
    monkeypatch.setattr(
        "dwsim_mcp_server.auth.clerk_verifier.PyJWKClient",
        FakePyJWKClient,
    )
    monkeypatch.setattr(
        "dwsim_mcp_server.auth.clerk_verifier.jwt.decode",
        lambda *args, **kwargs: {"scope": "user"},
    )
    _patch_time(monkeypatch, [0.0, 10.0, 3601.0])

    verifier = ClerkTokenVerifier(_config())

    asyncio.run(verifier.verify_token("token"))
    asyncio.run(verifier.verify_token("token"))
    asyncio.run(verifier.verify_token("token"))

    assert len(FakePyJWKClient.instances) == 2
