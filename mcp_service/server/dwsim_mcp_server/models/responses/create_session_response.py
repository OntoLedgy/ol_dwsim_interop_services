"""CreateSessionResponse DTO for MCP create_session tool."""

from pydantic import BaseModel, ConfigDict, Field


class CreateSessionResponse(BaseModel):
    """Response from creating a new DWSIM simulation session."""

    session_id: str = Field(..., description="Unique session identifier")
    name: str = Field(..., description="Session name")
    temp_dir: str = Field(..., description="Session temporary directory path")
    created_at: str = Field(..., description="Session creation timestamp (ISO 8601)")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "550e8400-e29b-41d4-a716-446655440000",
                "name": "Distillation Column Design",
                "temp_dir": "/tmp/dwsim_sessions/550e8400",
                "created_at": "2024-12-19T20:30:00Z",
            }
        }
    )
