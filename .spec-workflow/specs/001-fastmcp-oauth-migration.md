# FastMCP Migration with Clerk OAuth Integration

## Overview

This specification details the migration from the current low-level `Server` + `StreamableHTTPSessionManager` architecture to FastMCP with Clerk OAuth integration.

**Goal:** Enable secure remote access to the DWSIM MCP server via `mcp-remote` with Clerk as the OAuth provider.

**Estimated Effort:** 8-10 hours

**Relevant Paths:**
- MCP Server: `mcp_service/server/dwsim_mcp_server/`
- Tools: `mcp_service/server/dwsim_mcp_server/tools/`
- Config: `mcp_service/server/dwsim_mcp_server/config/`

---

## Current Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    server.py                            │
├─────────────────────────────────────────────────────────┤
│  Server("dwsim-mcp-server")                             │
│       │                                                 │
│       ├── register_tools(server, dependencies)          │
│       │       └── @server.list_tools() / @server.call_tool()
│       │                                                 │
│       └── register_resources(server, dependencies)      │
│                                                         │
│  StreamableHTTPSessionManager(app=server)               │
│       │                                                 │
│       └── Starlette Mount("/mcp", ...)                  │
└─────────────────────────────────────────────────────────┘
```

**Key Components:**
- `ServerDependencies` - Dependency injection container
- `tools/registry.py` - Central dispatcher with `@server.list_tools()` and `@server.call_tool()`
- 8 tool modules with `build_*_tools()` + `handle_*_tool()` pattern
- 33 total tools

---

## Target Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    server.py                            │
├─────────────────────────────────────────────────────────┤
│  FastMCP("dwsim-mcp-server",                            │
│          lifespan=app_lifespan,                         │
│          token_verifier=ClerkTokenVerifier(),           │
│          auth=AuthSettings(...))                        │
│       │                                                 │
│       ├── @mcp.tool() decorators (per tool)             │
│       │                                                 │
│       └── @mcp.resource() decorators                    │
│                                                         │
│  mcp.run(transport="streamable-http")                   │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│                    Clerk (External)                     │
├─────────────────────────────────────────────────────────┤
│  Authorization Server                                   │
│  - User authentication                                  │
│  - JWT token issuance                                   │
│  - JWKS endpoint for token verification                 │
└─────────────────────────────────────────────────────────┘
```

---

## Phase 1: Add Clerk Token Verifier (2 hours)

### 1.1 Install Dependencies

Add to `mcp_service/server/pyproject.toml`:

```toml
dependencies = [
    # ... existing deps ...
    "pyjwt[crypto]>=2.8.0",  # JWT verification
    "httpx>=0.27.0",          # Already present, for JWKS fetch
]
```

### 1.2 Create Auth Module

Create `mcp_service/server/dwsim_mcp_server/auth/__init__.py`:

```python
"""Authentication module for DWSIM MCP server."""

from dwsim_mcp_server.auth.clerk_verifier import ClerkTokenVerifier
from dwsim_mcp_server.auth.settings import AuthConfig

__all__ = ["ClerkTokenVerifier", "AuthConfig"]
```

Create `mcp_service/server/dwsim_mcp_server/auth/settings.py`:

```python
"""Auth configuration settings."""

from pydantic import Field, AnyHttpUrl
from pydantic_settings import BaseSettings, SettingsConfigDict


class AuthConfig(BaseSettings):
    """Clerk OAuth configuration."""

    model_config = SettingsConfigDict(env_prefix="CLERK_", case_sensitive=False)

    enabled: bool = Field(
        False,
        validation_alias="DWSIM_AUTH_ENABLED",
        description="Enable OAuth authentication.",
    )
    issuer_url: AnyHttpUrl = Field(
        "https://clerk.example.com",
        description="Clerk issuer URL (your Clerk frontend API URL).",
    )
    jwks_url: str | None = Field(
        None,
        description="JWKS endpoint URL. Defaults to {issuer_url}/.well-known/jwks.json",
    )
    audience: str | None = Field(
        None,
        description="Expected JWT audience claim.",
    )
    required_scopes: list[str] = Field(
        default_factory=lambda: ["user"],
        description="Required OAuth scopes for access.",
    )

    @property
    def effective_jwks_url(self) -> str:
        if self.jwks_url:
            return self.jwks_url
        return f"{self.issuer_url}/.well-known/jwks.json"
```

Create `mcp_service/server/dwsim_mcp_server/auth/clerk_verifier.py`:

```python
"""Clerk JWT token verifier for MCP OAuth."""

from __future__ import annotations

import time
from typing import Any

import httpx
import jwt
from jwt import PyJWKClient, PyJWKClientError

from mcp.server.auth.provider import AccessToken, TokenVerifier

from dwsim_mcp_server.auth.settings import AuthConfig
from dwsim_mcp_server.observability import get_logger


class ClerkTokenVerifier(TokenVerifier):
    """Verify Clerk-issued JWT tokens."""

    def __init__(self, config: AuthConfig) -> None:
        self.config = config
        self.logger = get_logger(__name__)
        self._jwks_client: PyJWKClient | None = None
        self._jwks_cache_time: float = 0
        self._jwks_cache_ttl: float = 3600  # 1 hour

    def _get_jwks_client(self) -> PyJWKClient:
        """Get or refresh JWKS client."""
        now = time.time()
        if self._jwks_client is None or (now - self._jwks_cache_time) > self._jwks_cache_ttl:
            self._jwks_client = PyJWKClient(
                self.config.effective_jwks_url,
                cache_keys=True,
                lifespan=self._jwks_cache_ttl,
            )
            self._jwks_cache_time = now
        return self._jwks_client

    async def verify_token(self, token: str) -> AccessToken | None:
        """Verify a Bearer token and return AccessToken if valid."""
        try:
            # Get signing key from JWKS
            jwks_client = self._get_jwks_client()
            signing_key = jwks_client.get_signing_key_from_jwt(token)

            # Decode and verify JWT
            decode_options: dict[str, Any] = {
                "verify_signature": True,
                "verify_exp": True,
                "verify_iat": True,
                "require": ["exp", "iat", "sub"],
            }

            if self.config.audience:
                decode_options["verify_aud"] = True

            payload = jwt.decode(
                token,
                signing_key.key,
                algorithms=["RS256"],
                audience=self.config.audience,
                issuer=str(self.config.issuer_url),
                options=decode_options,
            )

            # Extract scopes (Clerk uses 'scope' claim as space-separated string)
            scope_claim = payload.get("scope", "")
            scopes = scope_claim.split() if isinstance(scope_claim, str) else []

            # Check required scopes
            required = set(self.config.required_scopes)
            if required and not required.issubset(set(scopes)):
                self.logger.warning(
                    "token_missing_scopes",
                    required=list(required),
                    present=scopes,
                )
                return None

            # Extract expiration
            exp = payload.get("exp")
            expires_at = exp if isinstance(exp, int) else None

            self.logger.debug(
                "token_verified",
                sub=payload.get("sub"),
                scopes=scopes,
            )

            return AccessToken(
                token=token,
                scopes=scopes,
                expires_at=expires_at,
            )

        except PyJWKClientError as e:
            self.logger.warning("jwks_fetch_failed", error=str(e))
            return None
        except jwt.ExpiredSignatureError:
            self.logger.warning("token_expired")
            return None
        except jwt.InvalidTokenError as e:
            self.logger.warning("token_invalid", error=str(e))
            return None
        except Exception as e:
            self.logger.exception("token_verification_failed", error=str(e))
            return None
```

---

## Phase 2: Migrate to FastMCP (4-5 hours)

### 2.1 Create Lifespan Context

Create `mcp_service/server/dwsim_mcp_server/context.py`:

```python
"""Application context and lifespan management."""

from __future__ import annotations

from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from dataclasses import dataclass

from mcp.server.fastmcp import FastMCP

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.config.resource_limit_settings import ResourceLimitSettings
from dwsim_mcp_server.ipc.flowsheet_client import FlowsheetClient
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.service import FlowsheetService
from dwsim_mcp_server.service.diagnostics_service import DiagnosticsService
from dwsim_mcp_server.services import ThermodynamicsService
from dwsim_mcp_server.services.sensitivity_service import SensitivityService
from dwsim_mcp_server.observability import get_logger


@dataclass
class AppContext:
    """Application context with typed dependencies."""

    settings: ServerSettings
    session_client: LimitedSessionClient
    flowsheet_client: FlowsheetClient
    flowsheet_service: FlowsheetService
    thermodynamics_service: ThermodynamicsService
    sensitivity_service: SensitivityService
    diagnostics_service: DiagnosticsService


@asynccontextmanager
async def app_lifespan(server: FastMCP) -> AsyncIterator[AppContext]:
    """Manage application lifecycle with type-safe context."""
    logger = get_logger(__name__)
    settings = ServerSettings()

    # Initialize dependencies
    session_client = LimitedSessionClient(settings.resource_limits)
    flowsheet_client = FlowsheetClient(session_client)

    flowsheet_service = FlowsheetService(
        session_client=session_client,
        flowsheet_client=flowsheet_client,
    )
    thermodynamics_service = ThermodynamicsService(session_client=session_client)
    sensitivity_service = SensitivityService(
        session_client=session_client,
        flowsheet_client=flowsheet_client,
        allowed_export_roots=settings.case_storage_roots,
    )
    diagnostics_service = DiagnosticsService(session_client=session_client)

    # Start monitoring
    session_client.start_monitoring()
    logger.info("dependencies_initialized")

    try:
        yield AppContext(
            settings=settings,
            session_client=session_client,
            flowsheet_client=flowsheet_client,
            flowsheet_service=flowsheet_service,
            thermodynamics_service=thermodynamics_service,
            sensitivity_service=sensitivity_service,
            diagnostics_service=diagnostics_service,
        )
    finally:
        # Cleanup
        await session_client.stop_monitoring()
        session_client.dispose()
        logger.info("dependencies_disposed")
```

### 2.2 Convert Tools to FastMCP Decorators

**Strategy:** Create a new `tools_fastmcp/` directory with converted tools, then swap.

Example conversion for `session.py`:

```python
"""Session management tools (FastMCP version)."""

from __future__ import annotations

from mcp.server.fastmcp import Context, FastMCP
from mcp.server.session import ServerSession

from dwsim_mcp_server.context import AppContext
from dwsim_mcp_server.models.requests import (
    CreateSessionRequest,
    CloseSessionRequest,
    LoadCaseRequest,
    SaveCaseRequest,
)
from dwsim_mcp_server.observability import get_logger
from dwsim_mcp_server.utils import resolve_case_path


def register_session_tools(mcp: FastMCP) -> None:
    """Register session management tools with FastMCP."""
    logger = get_logger(__name__)

    @mcp.tool(
        description=(
            "Create a new DWSIM session for flowsheet work. "
            "This MUST be called first before any other DWSIM tools. "
            "Returns a session_id used in all subsequent calls."
        )
    )
    async def create_session(
        name: str | None = None,
        timeout: int = 3600,
        temp_dir: str | None = None,
        ctx: Context[ServerSession, AppContext] = None,
    ) -> dict:
        """Create a new DWSIM simulation session."""
        app = ctx.request_context.lifespan_context
        session_id = await app.session_client.create_session(
            flowsheet_name=name,
            timeout_seconds=timeout,
        )
        logger.info("session_created", session_id=session_id, name=name)
        return {"session_id": session_id}

    @mcp.tool(
        description=(
            "Close an existing DWSIM session and release resources. "
            "Always call this when done to free memory."
        )
    )
    async def close_session(
        session_id: str,
        ctx: Context[ServerSession, AppContext] = None,
    ) -> dict:
        """Close an existing DWSIM session."""
        app = ctx.request_context.lifespan_context
        result = await app.session_client.close_session(session_id)
        logger.info("session_closed", session_id=session_id, success=result)
        return {"success": result}

    @mcp.tool(
        description="Save the flowsheet to a DWSIM case file for later reload. "
                    "Provide file_path ending in .dwxmz (compressed) or .dwxml (uncompressed)."
    )
    async def save_case(
        session_id: str,
        file_path: str,
        ctx: Context[ServerSession, AppContext] = None,
    ) -> dict:
        """Save the current flowsheet case to a DWSIM file."""
        app = ctx.request_context.lifespan_context
        resolved_path = resolve_case_path(file_path, app.settings.case_storage_roots)
        result = await app.session_client.save_case(session_id, str(resolved_path))
        logger.info("case_saved", session_id=session_id, path=str(resolved_path))
        return {"success": result}

    @mcp.tool(
        description="Load a flowsheet case from a DWSIM file (.dwxmz) into a session."
    )
    async def load_case(
        session_id: str,
        file_path: str,
        ctx: Context[ServerSession, AppContext] = None,
    ) -> dict:
        """Load a flowsheet case from a DWSIM file."""
        app = ctx.request_context.lifespan_context
        resolved_path = resolve_case_path(file_path, app.settings.case_storage_roots)
        result = await app.session_client.load_case(session_id, str(resolved_path))
        logger.info("case_loaded", session_id=session_id, path=str(resolved_path))
        return {"session_id": session_id}
```

### 2.3 Tool Conversion Checklist

Each tool module needs conversion:

| Module | Tools | Priority |
|--------|-------|----------|
| `session.py` | 4 | High - core functionality |
| `flowsheet.py` | 10 | High - core functionality |
| `simulation.py` | 3 | High - core functionality |
| `compound.py` | 2 | Medium |
| `analysis.py` | 3 | Medium |
| `sensitivity.py` | 5 | Medium |
| `export.py` | 3 | Medium |
| `diagnostics.py` | 3 | Low |

**Conversion pattern for each tool:**

1. Remove from `build_*_tools()` list
2. Add `@mcp.tool()` decorator with description
3. Change function signature:
   - Named parameters instead of `arguments: dict`
   - Add `ctx: Context[ServerSession, AppContext]` parameter
4. Access dependencies via `ctx.request_context.lifespan_context`
5. Return dict directly (no `model_dump()` needed for simple types)

---

## Phase 3: Update Server Entry Point (1 hour)

### 3.1 New server.py

Replace `mcp_service/server/dwsim_mcp_server/server.py`:

```python
"""MCP server bootstrap entry point (FastMCP version)."""

from __future__ import annotations

from pydantic import AnyHttpUrl

from mcp.server.auth.settings import AuthSettings
from mcp.server.fastmcp import FastMCP

from dwsim_mcp_server.auth import AuthConfig, ClerkTokenVerifier
from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.config.server_settings import TransportMode
from dwsim_mcp_server.context import app_lifespan
from dwsim_mcp_server.observability import configure_logging, get_logger
from dwsim_mcp_server.observability.settings import ObservabilitySettings
from dwsim_mcp_server.observability.tracing import configure_tracing

# Import tool registration functions
from dwsim_mcp_server.tools_fastmcp.session import register_session_tools
from dwsim_mcp_server.tools_fastmcp.flowsheet import register_flowsheet_tools
from dwsim_mcp_server.tools_fastmcp.simulation import register_simulation_tools
from dwsim_mcp_server.tools_fastmcp.compound import register_compound_tools
from dwsim_mcp_server.tools_fastmcp.analysis import register_analysis_tools
from dwsim_mcp_server.tools_fastmcp.sensitivity import register_sensitivity_tools
from dwsim_mcp_server.tools_fastmcp.export import register_export_tools
from dwsim_mcp_server.tools_fastmcp.diagnostics import register_diagnostics_tools


def create_mcp_server() -> FastMCP:
    """Create and configure the FastMCP server instance."""
    settings = ServerSettings()
    auth_config = AuthConfig()
    obs_settings = ObservabilitySettings.from_env()

    configure_logging(obs_settings.log_level)
    logger = get_logger(__name__)

    configure_tracing(
        exporter=obs_settings.tracing_exporter,
        endpoint=obs_settings.tracing_endpoint,
        sample_rate=obs_settings.tracing_sample_rate,
    )

    # Build FastMCP kwargs
    mcp_kwargs = {
        "name": "dwsim-mcp-server",
        "lifespan": app_lifespan,
    }

    # Add OAuth if enabled
    if auth_config.enabled:
        logger.info(
            "oauth_enabled",
            issuer=str(auth_config.issuer_url),
            scopes=auth_config.required_scopes,
        )
        mcp_kwargs["token_verifier"] = ClerkTokenVerifier(auth_config)
        mcp_kwargs["auth"] = AuthSettings(
            issuer_url=auth_config.issuer_url,
            resource_server_url=AnyHttpUrl(
                f"http://{settings.http_host}:{settings.http_port}"
            ),
            required_scopes=auth_config.required_scopes,
        )
    else:
        logger.info("oauth_disabled")

    mcp = FastMCP(**mcp_kwargs)

    # Register all tools
    register_session_tools(mcp)
    register_flowsheet_tools(mcp)
    register_simulation_tools(mcp)
    register_compound_tools(mcp)
    register_analysis_tools(mcp)
    register_sensitivity_tools(mcp)
    register_export_tools(mcp)
    register_diagnostics_tools(mcp)

    logger.info("server_configured", tool_count=len(mcp._tool_manager._tools))

    return mcp


def run() -> None:
    """Synchronous entry point for CLI usage."""
    settings = ServerSettings()
    mcp = create_mcp_server()

    if settings.transport_mode == TransportMode.STREAMABLE_HTTP:
        mcp.run(
            transport="streamable-http",
            host=settings.http_host,
            port=settings.http_port,
        )
    else:
        mcp.run(transport="stdio")


if __name__ == "__main__":
    run()
```

---

## Phase 4: Clerk Configuration (1 hour)

### 4.1 Clerk Dashboard Setup

1. **Create OAuth Application in Clerk:**
   - Go to Clerk Dashboard > Configure > OAuth Applications
   - Create new application for "DWSIM MCP Server"
   - Note the Client ID

2. **Configure Allowed Redirect URIs:**
   ```
   http://localhost:3000/callback  (for mcp-remote local dev)
   https://your-domain.com/callback  (for production)
   ```

3. **Get Clerk JWKS URL:**
   - Your JWKS URL is: `https://<your-clerk-frontend-api>/.well-known/jwks.json`
   - Example: `https://clerk.your-domain.com/.well-known/jwks.json`

### 4.2 Environment Variables

Add to `.env`:

```bash
# OAuth Configuration
DWSIM_AUTH_ENABLED=true
CLERK_ISSUER_URL=https://clerk.your-domain.com
CLERK_AUDIENCE=your-clerk-client-id  # Optional, for audience validation
CLERK_REQUIRED_SCOPES=user  # Space-separated list

# HTTP Transport (required for OAuth)
DWSIM_TRANSPORT_MODE=streamable-http
DWSIM_HTTP_HOST=0.0.0.0
DWSIM_HTTP_PORT=8001
```

### 4.3 Update .env.example Template

Update `mcp_service/server/dwsim_mcp_server/templates/env.example.j2`:

```jinja2
# =============================================================================
# DWSIM MCP Server Configuration
# =============================================================================

# Transport mode: 'stdio' for CLI, 'streamable-http' for Docker/HTTP
DWSIM_TRANSPORT_MODE=streamable-http

# HTTP transport settings
DWSIM_HTTP_HOST=0.0.0.0
DWSIM_HTTP_PORT=8001

# =============================================================================
# OAuth Configuration (Clerk)
# =============================================================================

# Enable OAuth authentication
DWSIM_AUTH_ENABLED=false

# Clerk issuer URL (your Clerk Frontend API URL)
CLERK_ISSUER_URL=https://clerk.example.com

# Optional: Expected JWT audience claim
# CLERK_AUDIENCE=your-client-id

# Required OAuth scopes (space-separated)
CLERK_REQUIRED_SCOPES=user

# =============================================================================
# Existing settings...
# =============================================================================
```

---

## Phase 5: Client Configuration (30 minutes)

### 5.1 Claude Desktop with mcp-remote

Update `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "dwsim": {
      "command": "npx",
      "args": [
        "-y",
        "mcp-remote",
        "https://your-server.com/mcp"
      ]
    }
  }
}
```

On first connection, `mcp-remote` will:
1. Fetch `/.well-known/oauth-protected-resource` from your server
2. Discover Clerk as the Authorization Server
3. Open browser for Clerk login
4. Store token for subsequent requests

### 5.2 Direct Claude Code Connection (No Auth)

For local development without OAuth:

```json
{
  "mcpServers": {
    "dwsim-local": {
      "command": "dwsim-mcp",
      "args": ["run"],
      "env": {
        "DWSIM_TRANSPORT_MODE": "stdio",
        "DWSIM_AUTH_ENABLED": "false"
      }
    }
  }
}
```

---

## Phase 6: Testing (2 hours)

### 6.1 Unit Tests for Token Verifier

Create `mcp_service/server/tests/unit/auth/test_clerk_verifier.py`:

```python
"""Tests for Clerk token verifier."""

import pytest
from unittest.mock import Mock, patch, AsyncMock

from dwsim_mcp_server.auth.clerk_verifier import ClerkTokenVerifier
from dwsim_mcp_server.auth.settings import AuthConfig


@pytest.fixture
def auth_config():
    return AuthConfig(
        enabled=True,
        issuer_url="https://clerk.example.com",
        required_scopes=["user"],
    )


@pytest.fixture
def verifier(auth_config):
    return ClerkTokenVerifier(auth_config)


class TestClerkTokenVerifier:

    async def test_verify_valid_token(self, verifier):
        # Mock JWKS client and JWT decode
        with patch.object(verifier, '_get_jwks_client') as mock_jwks:
            mock_key = Mock()
            mock_key.key = "fake-key"
            mock_jwks.return_value.get_signing_key_from_jwt.return_value = mock_key

            with patch('jwt.decode') as mock_decode:
                mock_decode.return_value = {
                    "sub": "user_123",
                    "scope": "user admin",
                    "exp": 9999999999,
                    "iat": 1000000000,
                }

                result = await verifier.verify_token("valid.jwt.token")

                assert result is not None
                assert result.scopes == ["user", "admin"]

    async def test_verify_expired_token(self, verifier):
        with patch.object(verifier, '_get_jwks_client') as mock_jwks:
            mock_key = Mock()
            mock_key.key = "fake-key"
            mock_jwks.return_value.get_signing_key_from_jwt.return_value = mock_key

            with patch('jwt.decode') as mock_decode:
                import jwt
                mock_decode.side_effect = jwt.ExpiredSignatureError()

                result = await verifier.verify_token("expired.jwt.token")

                assert result is None

    async def test_verify_missing_scopes(self, verifier):
        with patch.object(verifier, '_get_jwks_client') as mock_jwks:
            mock_key = Mock()
            mock_key.key = "fake-key"
            mock_jwks.return_value.get_signing_key_from_jwt.return_value = mock_key

            with patch('jwt.decode') as mock_decode:
                mock_decode.return_value = {
                    "sub": "user_123",
                    "scope": "read",  # Missing "user" scope
                    "exp": 9999999999,
                    "iat": 1000000000,
                }

                result = await verifier.verify_token("no-scope.jwt.token")

                assert result is None
```

### 6.2 Integration Test

Create `mcp_service/server/tests/integration/test_oauth_flow.py`:

```python
"""Integration tests for OAuth flow."""

import pytest
from httpx import AsyncClient

from dwsim_mcp_server.server import create_mcp_server


@pytest.fixture
def app():
    mcp = create_mcp_server()
    return mcp.streamable_http_app()


class TestOAuthDiscovery:

    async def test_protected_resource_metadata(self, app):
        async with AsyncClient(app=app, base_url="http://test") as client:
            response = await client.get("/.well-known/oauth-protected-resource")

            assert response.status_code == 200
            data = response.json()
            assert "resource" in data
            assert "authorization_servers" in data

    async def test_mcp_endpoint_requires_auth(self, app):
        async with AsyncClient(app=app, base_url="http://test") as client:
            response = await client.post(
                "/mcp",
                json={"jsonrpc": "2.0", "method": "initialize", "id": 1},
            )

            # Should return 401 without token
            assert response.status_code == 401

    async def test_mcp_endpoint_with_valid_token(self, app):
        # This test requires a valid Clerk token - skip in CI
        pytest.skip("Requires valid Clerk token")
```

---

## Migration Checklist

### Pre-Migration
- [ ] Backup current `server.py` and `tools/` directory
- [ ] Create feature branch: `feature/fastmcp-oauth`
- [ ] Update `pyproject.toml` with new dependencies
- [ ] Run `uv sync` or `pip install -e ".[dev]"`

### Phase 1: Auth Module
- [ ] Create `auth/` directory
- [ ] Implement `AuthConfig` settings
- [ ] Implement `ClerkTokenVerifier`
- [ ] Add unit tests for verifier

### Phase 2: Tool Migration
- [ ] Create `tools_fastmcp/` directory
- [ ] Convert `session.py` (4 tools)
- [ ] Convert `flowsheet.py` (10 tools)
- [ ] Convert `simulation.py` (3 tools)
- [ ] Convert `compound.py` (2 tools)
- [ ] Convert `analysis.py` (3 tools)
- [ ] Convert `sensitivity.py` (5 tools)
- [ ] Convert `export.py` (3 tools)
- [ ] Convert `diagnostics.py` (3 tools)

### Phase 3: Server Update
- [ ] Create `context.py` with `AppContext` and `app_lifespan`
- [ ] Update `server.py` to use FastMCP
- [ ] Update CLI entry point if needed

### Phase 4: Configuration
- [ ] Update `.env.example` template
- [ ] Document Clerk setup steps
- [ ] Create `mcp.json` template for clients

### Phase 5: Testing
- [ ] Run existing tests (should still pass)
- [ ] Add OAuth-specific tests
- [ ] Manual test with `mcp-remote`
- [ ] Test stdio mode still works

### Post-Migration
- [ ] Remove old `tools/` directory (or keep as `tools_legacy/`)
- [ ] Update README with OAuth documentation
- [ ] Create PR and review

---

## Rollback Plan

If issues arise:

1. **Immediate rollback:** Revert to backup `server.py` and `tools/`
2. **Partial rollback:** Keep FastMCP but disable OAuth via `DWSIM_AUTH_ENABLED=false`
3. **Transport fallback:** Switch to stdio mode which bypasses OAuth entirely

---

## Notes

### Clerk-Specific Considerations

1. **Token Format:** Clerk issues standard JWTs with RS256 signing
2. **JWKS Caching:** The verifier caches JWKS for 1 hour to reduce latency
3. **Scopes:** Clerk may not include scopes by default - you may need to configure custom claims
4. **Multi-tenant:** If using Clerk organizations, add organization validation to the verifier

### MCP-Remote Behavior

- `mcp-remote` expects `/.well-known/oauth-protected-resource` at the server root
- If your server is mounted at `/dwsim/mcp`, the discovery endpoint should still be at the root
- The `resource_server_url` in `AuthSettings` must match the URL clients use to connect

### Performance

- First request has ~100-200ms overhead for JWKS fetch
- Subsequent requests have minimal overhead (cached key + fast JWT verification)
- Consider Redis/memcached for JWKS caching in high-traffic scenarios
