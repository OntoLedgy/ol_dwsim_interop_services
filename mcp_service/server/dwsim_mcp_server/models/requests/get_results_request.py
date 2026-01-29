"""GetResultsRequest DTO for MCP get_results tool."""

from typing import Optional
from pydantic import BaseModel, ConfigDict, Field, field_validator


class GetResultsRequest(BaseModel):
    """Request to retrieve simulation results."""

    session_id: str = Field(
        ...,
        description="Session identifier (letters, numbers, '.', '-', '_')",
        min_length=1,
    )
    object_id: Optional[str] = Field(
        None,
        description="Optional object identifier for targeted results",
        min_length=1
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

    @field_validator("object_id")
    @classmethod
    def validate_object_id(cls, value: Optional[str]) -> Optional[str]:
        """Normalize object_id when provided."""
        if value is None:
            return None
        cleaned = value.strip()
        if not cleaned:
            raise ValueError("object_id must not be empty")
        return cleaned

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
                "object_id": "stream-vapor",
            }
        }
    )
