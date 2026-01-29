"""RunSimulationRequest DTO for MCP run tool."""

from pydantic import BaseModel, ConfigDict, Field, field_validator


class RunSimulationRequest(BaseModel):
    """Request to run a simulation for a session."""

    session_id: str = Field(
        ...,
        description="Session identifier (letters, numbers, '.', '-', '_')",
        min_length=1,
    )
    timeout_seconds: int = Field(
        120,
        description="Maximum calculation time in seconds",
        ge=1,
    )

    @field_validator("session_id")
    @classmethod
    def validate_session_id(cls, value: str) -> str:
        """Ensure session_id is non-empty and contains only safe characters."""
        cleaned = value.strip()
        if not cleaned:
            raise ValueError("session_id must not be empty")
        for char in cleaned:
            if not (char.isalnum() or char in {".", "-", "_"}):
                raise ValueError("session_id contains invalid characters")
        return cleaned

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
                "timeout_seconds": 120,
            }
        }
    )
