"""Authentication settings for Clerk OAuth."""

from __future__ import annotations

from typing import Optional

from pydantic import AnyHttpUrl, Field, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class AuthConfig(BaseSettings):
    """Configuration values for Clerk OAuth authentication."""

    model_config = SettingsConfigDict(env_prefix="", case_sensitive=False)

    enabled: bool = Field(
        False,
        validation_alias="DWSIM_AUTH_ENABLED",
        description="Enable OAuth authentication for HTTP transport.",
    )
    issuer_url: Optional[AnyHttpUrl] = Field(
        None,
        validation_alias="CLERK_ISSUER_URL",
        description="Clerk issuer URL for token verification.",
    )
    jwks_url: Optional[str] = Field(
        None,
        validation_alias="CLERK_JWKS_URL",
        description="Optional override for the Clerk JWKS URL.",
    )
    audience: Optional[str] = Field(
        None,
        validation_alias="CLERK_AUDIENCE",
        description="Optional JWT audience to enforce during verification.",
    )
    required_scopes: list[str] = Field(
        default_factory=lambda: ["user"],
        validation_alias="CLERK_REQUIRED_SCOPES",
        description="Required OAuth scopes for access tokens.",
    )

    @property
    def effective_jwks_url(self) -> str:
        """Return explicit JWKS URL or derive from issuer URL."""
        if self.jwks_url:
            return self.jwks_url
        if self.issuer_url:
            issuer = str(self.issuer_url).rstrip("/")
            return f"{issuer}/.well-known/jwks.json"
        raise ValueError("JWKS URL cannot be resolved without issuer_url or jwks_url.")

    @model_validator(mode="after")
    def _validate_enabled_requirements(self) -> "AuthConfig":
        if not self.enabled:
            return self
        if self.issuer_url is None and self.jwks_url is None:
            raise ValueError("Issuer URL or JWKS URL is required when auth is enabled.")
        return self
