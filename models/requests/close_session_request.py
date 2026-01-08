"""CloseSessionRequest DTO for MCP close_session tool."""

from pydantic import BaseModel, ConfigDict, Field


class CloseSessionRequest(BaseModel):
    """Request to close an existing DWSIM simulation session."""

    session_id: str = Field(
        ...,
        description="Session identifier to close",
        min_length=1
    )

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
            }
        }
    )
