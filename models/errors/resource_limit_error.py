"""ResourceLimitError model for limit violations."""

from typing import Optional
from pydantic import BaseModel, ConfigDict, Field


class ResourceLimitError(BaseModel):
    """Error related to resource limit enforcement."""

    code: str = Field(..., description="Error code (e.g., RESOURCE_LIMIT_EXCEEDED, TIMEOUT)")
    message: str = Field(..., description="Human-readable error message")
    session_id: Optional[str] = Field(None, description="Session ID if applicable")
    details: Optional[dict] = Field(None, description="Additional error details")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "code": "TIMEOUT",
                "message": "Operation timed out after 300 seconds.",
                "session_id": "550e8400-e29b-41d4-a716-446655440000",
                "details": {"timeout_seconds": 300, "elapsed_seconds": 305.2},
            }
        }
    )
