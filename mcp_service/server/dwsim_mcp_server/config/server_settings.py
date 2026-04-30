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

"""Server bootstrap settings for the DWSIM MCP server."""

from __future__ import annotations

from enum import Enum
from typing import Literal, Optional

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

from dwsim_mcp_server.config.resource_limit_settings import ResourceLimitSettings


class TransportMode(str, Enum):
    """Available MCP transport modes."""

    STDIO = "stdio"
    STREAMABLE_HTTP = "streamable-http"


class ServerSettings(BaseSettings):
    """Configuration values for MCP server bootstrap."""

    model_config = SettingsConfigDict(env_prefix="", case_sensitive=False)

    transport_mode: TransportMode = Field(
        TransportMode.STDIO,
        validation_alias="DWSIM_TRANSPORT_MODE",
        description="Transport mode: 'stdio' for CLI/direct connection, 'streamable-http' for Docker/HTTP deployment.",
    )
    http_host: str = Field(
        "0.0.0.0",
        validation_alias="DWSIM_HTTP_HOST",
        description="Host address for HTTP transport (only used when transport_mode='streamable-http').",
    )
    http_port: int = Field(
        8000,
        validation_alias="DWSIM_HTTP_PORT",
        description="Port for HTTP transport (only used when transport_mode='streamable-http').",
    )
    public_base_url: Optional[str] = Field(
        None,
        validation_alias="DWSIM_PUBLIC_BASE_URL",
        description="Full public MCP URL when behind a reverse proxy (e.g., https://example.com/dwsim/mcp). "
        "Used for OAuth discovery. If not set, uses http://{http_host}:{http_port}/mcp.",
    )
    log_level: str = Field(
        "INFO",
        validation_alias="DWSIM_LOG_LEVEL",
        description="Logging level for the MCP server (e.g., DEBUG, INFO, WARNING).",
    )
    enable_pythonnet: bool = Field(
        True,
        validation_alias="DWSIM_ENABLE_PYTHONNET",
        description="Enable pythonnet bridge for in-process worker calls.",
    )
    worker_assembly_path: Optional[str] = Field(
        None,
        validation_alias="DWSIM_WORKER_ASSEMBLY_PATH",
        description="Optional path to DwsimWorker.dll if not using default discovery.",
    )
    case_storage_roots: list[str] = Field(
        default_factory=lambda: ["./cases"],
        validation_alias="DWSIM_CASE_STORAGE_ROOTS",
        description="Allowed base directories for case save/load operations.",
    )
    resource_limits: ResourceLimitSettings = Field(
        default_factory=ResourceLimitSettings,
        description="Nested resource limit settings for sessions and operations.",
    )

    # Resource provider settings
    docs_path: str = Field(
        "./docs/resources",
        validation_alias="DWSIM_DOCS_PATH",
        description="Path to documentation resources directory.",
    )
    sample_cases_path: str = Field(
        "./cases/samples",
        validation_alias="DWSIM_SAMPLE_CASES_PATH",
        description="Path to sample cases metadata directory.",
    )
    apps_path: str = Field(
        "./apps/templates",
        validation_alias="DWSIM_APPS_PATH",
        description="Path to MCP UI app templates directory.",
    )
    apps_cache_enabled: bool = Field(
        True,
        validation_alias="DWSIM_APPS_CACHE_ENABLED",
        description="Enable caching for app templates and app.json metadata.",
    )
    apps_cache_ttl_seconds: int = Field(
        300,
        validation_alias="DWSIM_APPS_CACHE_TTL_SECONDS",
        description="Time-to-live for app cache entries in seconds.",
    )
    apps_default_csp_connect_domains: list[str] = Field(
        default_factory=list,
        validation_alias="DWSIM_APPS_DEFAULT_CSP_CONNECT_DOMAINS",
        description="Default connect-src CSP domains for apps.",
    )
    apps_default_csp_resource_domains: list[str] = Field(
        default_factory=list,
        validation_alias="DWSIM_APPS_DEFAULT_CSP_RESOURCE_DOMAINS",
        description="Default script/style/img CSP domains for apps.",
    )
    apps_default_csp_frame_domains: list[str] = Field(
        default_factory=list,
        validation_alias="DWSIM_APPS_DEFAULT_CSP_FRAME_DOMAINS",
        description="Default frame-src CSP domains for apps.",
    )
    max_resource_size_kb: int = Field(
        1024,
        validation_alias="DWSIM_MAX_RESOURCE_SIZE_KB",
        description="Maximum size in KB for resource content responses.",
    )
