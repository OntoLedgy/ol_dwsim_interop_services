"""RunSimulationRequest DTO for MCP run tool."""

from pydantic import BaseModel, ConfigDict, Field


class RunSimulationRequest(BaseModel):
    """Request to run a simulation for a session."""

    session_id: str = Field(..., description="Session identifier", min_length=1)

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
            }
        }
    )
