"""Clerk OAuth token verification."""

from __future__ import annotations

import time
from typing import Any, Optional

import jwt
from jwt import ExpiredSignatureError, InvalidTokenError, PyJWKClient, PyJWKClientError
from mcp.server.auth.provider import AccessToken, TokenVerifier

from dwsim_mcp_server.auth.settings import AuthConfig
from dwsim_mcp_server.observability.logging import get_logger

_JWKS_CACHE_SECONDS = 60 * 60


class ClerkTokenVerifier(TokenVerifier):
    """Verify Clerk-issued JWTs via JWKS with caching and scope checks."""

    def __init__(self, config: AuthConfig) -> None:
        self._config = config
        self._logger = get_logger(__name__)
        self._jwks_client: Optional[PyJWKClient] = None
        self._jwks_last_refresh = 0.0

    @property
    def required_scopes(self) -> list[str]:
        """Return required scopes for RemoteAuthProvider compatibility."""
        return self._config.required_scopes or []

    async def verify_token(self, token: str) -> AccessToken | None:
        if not token:
            self._logger.warning("auth_token_missing")
            return None
        payload = self._decode_payload(token)
        if payload is None:
            return None
        scopes = self._extract_scopes(payload)
        if not self._has_required_scopes(scopes):
            self._logger.warning(
                "auth_token_missing_scopes",
                required=self._config.required_scopes,
                present=scopes,
            )
            return None
        return AccessToken(
            token=token,
            client_id=self._extract_client_id(payload),
            scopes=scopes,
            expires_at=payload.get("exp"),
            resource=payload.get("resource"),
        )

    def _decode_payload(self, token: str) -> Optional[dict[str, Any]]:
        try:
            signing_key = self._get_jwks_client().get_signing_key_from_jwt(token).key
            return jwt.decode(
                token,
                signing_key,
                algorithms=["RS256"],
                **self._decode_kwargs(),
            )
        except ExpiredSignatureError:
            self._logger.warning("auth_token_expired")
        except (InvalidTokenError, PyJWKClientError) as exc:
            self._logger.warning("auth_token_invalid", error=str(exc))
        except Exception as exc:  # noqa: BLE001 - log unknown failures
            self._logger.warning("auth_token_verification_failed", error=str(exc))
        return None

    def _decode_kwargs(self) -> dict[str, Any]:
        options = {"verify_aud": self._config.audience is not None}
        kwargs: dict[str, Any] = {"options": options}
        if self._config.audience is not None:
            kwargs["audience"] = self._config.audience
        if self._config.issuer_url is not None:
            kwargs["issuer"] = str(self._config.issuer_url).rstrip("/")
        return kwargs

    def _get_jwks_client(self) -> PyJWKClient:
        now = time.time()
        if self._needs_jwks_refresh(now):
            self._jwks_client = PyJWKClient(self._config.effective_jwks_url)
            self._jwks_last_refresh = now
        return self._jwks_client

    def _needs_jwks_refresh(self, now: float) -> bool:
        if self._jwks_client is None:
            return True
        return (now - self._jwks_last_refresh) >= _JWKS_CACHE_SECONDS

    def _extract_scopes(self, payload: dict[str, Any]) -> list[str]:
        if isinstance(payload.get("scope"), str):
            return [scope for scope in payload["scope"].split() if scope]
        if isinstance(payload.get("scopes"), list):
            return [str(scope) for scope in payload["scopes"] if scope]
        if isinstance(payload.get("scp"), list):
            return [str(scope) for scope in payload["scp"] if scope]
        return []

    def _has_required_scopes(self, scopes: list[str]) -> bool:
        required = set(self._config.required_scopes or [])
        if not required:
            return True
        return required.issubset(set(scopes))

    def _extract_client_id(self, payload: dict[str, Any]) -> str:
        if isinstance(payload.get("azp"), str):
            return payload["azp"]
        if isinstance(payload.get("client_id"), str):
            return payload["client_id"]
        aud = payload.get("aud")
        if isinstance(aud, str):
            return aud
        if isinstance(aud, list) and aud:
            return str(aud[0])
        return "unknown"
