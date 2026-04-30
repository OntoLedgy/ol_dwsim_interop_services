# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Resource limit settings for the DWSIM MCP server."""

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class ResourceLimitSettings(BaseSettings):
    """Configuration values for timeouts and resource limits."""

    model_config = SettingsConfigDict(env_prefix="", case_sensitive=False)

    max_sessions: int = Field(
        10,
        ge=1,
        validation_alias="DWSIM_MAX_SESSIONS",
        description="Maximum number of concurrent sessions.",
    )
    session_timeout_seconds: int = Field(
        3600,
        ge=60,
        le=86400,
        validation_alias="DWSIM_SESSION_TIMEOUT",
        description="Default session lifetime in seconds.",
    )
    operation_timeout_seconds: int = Field(
        300,
        ge=1,
        validation_alias="DWSIM_OPERATION_TIMEOUT",
        description="Default per-operation timeout in seconds.",
    )
    memory_limit_mb: int = Field(
        2048,
        ge=1,
        validation_alias="DWSIM_MEMORY_LIMIT_MB",
        description="Process memory limit in MB before rejecting new operations.",
    )
    memory_poll_interval_seconds: float = Field(
        2.0,
        ge=0.1,
        validation_alias="DWSIM_MEMORY_POLL_INTERVAL_SECONDS",
        description="Interval in seconds for polling process memory usage.",
    )
    memory_recovery_ratio: float = Field(
        0.9,
        ge=0.5,
        le=1.0,
        validation_alias="DWSIM_MEMORY_RECOVERY_RATIO",
        description="Ratio of memory limit below which breach state clears.",
    )
