"""Diagnostics Pydantic models."""

from __future__ import annotations

from datetime import datetime
from typing import Any, Dict, Optional

from pydantic import BaseModel, ConfigDict, Field, field_validator


class DiagnosticsBaseModel(BaseModel):
    model_config = ConfigDict(from_attributes=True, extra="forbid")


class MemorySnapshot(DiagnosticsBaseModel):
    rss_bytes: int = Field(..., ge=0, description="Resident set size in bytes.")
    limit_bytes: int = Field(..., ge=0, description="Configured memory limit in bytes.")
    recovery_threshold_bytes: int = Field(
        ...,
        ge=0,
        description="Memory recovery threshold in bytes.",
    )
    breached: bool = Field(..., description="Whether the memory limit is breached.")


class ServerDiagnostics(DiagnosticsBaseModel):
    uptime_seconds: float = Field(..., description="Server uptime in seconds.")
    active_sessions: int = Field(..., ge=0, description="Number of active sessions.")
    memory: MemorySnapshot = Field(..., description="Current memory snapshot.")
    error_count: int = Field(..., ge=0, description="Number of recorded errors.")
    metrics_text: Optional[str] = Field(
        default=None,
        description="Prometheus metrics exposition text, if available.",
    )

    @field_validator("uptime_seconds")
    @classmethod
    def _validate_uptime_seconds(cls, value: float) -> float:
        if value < 0:
            raise ValueError("uptime_seconds must be non-negative")
        return value


class SessionDiagnostics(DiagnosticsBaseModel):
    session_id: str = Field(..., min_length=1, description="Session identifier.")
    state: str = Field(..., description="Session state (active or expired).")
    remaining_lifetime_seconds: Optional[float] = Field(
        default=None,
        description="Remaining session lifetime in seconds, if available.",
    )
    memory: MemorySnapshot = Field(..., description="Current memory snapshot.")

    @field_validator("state")
    @classmethod
    def _validate_state(cls, value: str) -> str:
        allowed = {"active", "expired"}
        if value not in allowed:
            raise ValueError(f"state must be one of {sorted(allowed)}")
        return value

    @field_validator("remaining_lifetime_seconds")
    @classmethod
    def _validate_remaining_lifetime(cls, value: Optional[float]) -> Optional[float]:
        if value is None:
            return value
        if value < 0:
            raise ValueError("remaining_lifetime_seconds must be non-negative")
        return value


class DiagnosticBundle(DiagnosticsBaseModel):
    bundle_id: str = Field(..., min_length=1, description="Diagnostic bundle identifier.")
    session_id: Optional[str] = Field(
        default=None,
        description="Associated session identifier, if any.",
    )
    created_at: datetime = Field(..., description="Bundle creation time in UTC.")
    error_type: str = Field(..., min_length=1, description="Error class name.")
    error_message: str = Field(..., description="Error message.")
    stack_trace: Optional[str] = Field(
        default=None,
        description="Captured stack trace, if available.",
    )
    context: Dict[str, Any] = Field(
        default_factory=dict,
        description="Additional diagnostic context.",
    )
    server_uptime_seconds: float = Field(
        default=0.0,
        description="Server uptime in seconds at capture time.",
    )
    memory: Optional[MemorySnapshot] = Field(
        default=None,
        description="Memory snapshot at capture time, if available.",
    )
    metrics_text: Optional[str] = Field(
        default=None,
        description="Prometheus metrics exposition text, if available.",
    )

    @field_validator("server_uptime_seconds")
    @classmethod
    def _validate_server_uptime_seconds(cls, value: float) -> float:
        if value < 0:
            raise ValueError("server_uptime_seconds must be non-negative")
        return value


class ErrorSummary(DiagnosticsBaseModel):
    bundle_id: str = Field(..., min_length=1, description="Diagnostic bundle identifier.")
    session_id: Optional[str] = Field(
        default=None,
        description="Associated session identifier, if any.",
    )
    created_at: datetime = Field(..., description="Bundle creation time in UTC.")
    error_type: str = Field(..., min_length=1, description="Error class name.")
    error_message: str = Field(..., description="Error message.")
