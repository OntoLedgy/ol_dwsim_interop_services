"""Observability settings for the DWSIM MCP server."""

from __future__ import annotations

import logging
from typing import Literal, Optional

from pydantic import Field, field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

ExporterType = Literal["jaeger", "zipkin", "otlp", "console", "none"]


class ObservabilitySettings(BaseSettings):
    """Settings sourced from DWSIM_* environment variables for observability."""

    model_config = SettingsConfigDict(env_prefix="DWSIM_", case_sensitive=False)

    log_level: str = Field(
        "INFO",
        description="Logging level for the MCP server (e.g., DEBUG, INFO, WARNING).",
    )
    log_file: Optional[str] = Field(
        None,
        description="Optional log file path for rotating file logging.",
    )
    log_file_max_bytes: int = Field(
        10 * 1024 * 1024,
        description="Maximum log file size in bytes before rotation.",
    )
    log_file_backup_count: int = Field(
        5,
        description="Number of rotated log files to keep.",
    )

    seq_url: Optional[str] = Field(
        None,
        description="Seq server URL for structured log shipping.",
    )
    seq_api_key: Optional[str] = Field(
        None,
        description="Seq API key for authenticated log shipping.",
    )

    tracing_enabled: bool = Field(
        False,
        description="Enable OpenTelemetry tracing.",
    )
    tracing_exporter: ExporterType = Field(
        "none",
        description="Tracing exporter type.",
    )
    tracing_endpoint: Optional[str] = Field(
        None,
        description="Tracing exporter endpoint URL.",
    )
    tracing_sample_rate: float = Field(
        1.0,
        description="Tracing sampling rate from 0.0 to 1.0.",
    )

    metrics_enabled: bool = Field(
        True,
        description="Enable Prometheus metrics collection.",
    )
    metrics_port: int = Field(
        9090,
        description="Metrics HTTP server port.",
    )

    @field_validator("log_level")
    @classmethod
    def _validate_log_level(cls, value: str) -> str:
        level = str(value).upper()
        if level not in logging._nameToLevel:
            raise ValueError(f"Invalid log level: {value}")
        return level

    @field_validator("tracing_sample_rate")
    @classmethod
    def _validate_sample_rate(cls, value: float) -> float:
        if not 0.0 <= value <= 1.0:
            raise ValueError("tracing_sample_rate must be between 0.0 and 1.0")
        return value

    @field_validator("metrics_port")
    @classmethod
    def _validate_metrics_port(cls, value: int) -> int:
        if not 1 <= value <= 65535:
            raise ValueError("metrics_port must be between 1 and 65535")
        return value

    @classmethod
    def from_env(cls) -> "ObservabilitySettings":
        """Instantiate settings from environment variables."""
        return cls()
