"""Server bootstrap settings for the DWSIM MCP server."""

from __future__ import annotations

from typing import Optional

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

from dwsim_mcp_server.config.resource_limit_settings import ResourceLimitSettings


class ServerSettings(BaseSettings):
    """Configuration values for MCP server bootstrap."""

    model_config = SettingsConfigDict(env_prefix="", case_sensitive=False)

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
