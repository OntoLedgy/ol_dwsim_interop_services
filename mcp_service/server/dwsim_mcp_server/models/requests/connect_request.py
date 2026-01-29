"""ConnectRequest DTO for MCP connect tool."""

from pydantic import BaseModel, ConfigDict, Field


class ConnectRequest(BaseModel):
    """Request to connect two objects in a flowsheet."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    source_id: str = Field(..., description="Source object identifier", min_length=1)
    target_id: str = Field(..., description="Target object identifier", min_length=1)
    port_name: str = Field(..., description="Port name to connect", min_length=1)

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
                "source_id": "stream-feed",
                "target_id": "unit-sep-001",
                "port_name": "inlet",
            }
        }
    )
