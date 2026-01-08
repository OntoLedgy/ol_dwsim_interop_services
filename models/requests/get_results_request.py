"""GetResultsRequest DTO for MCP get_results tool."""

from typing import Optional
from pydantic import BaseModel, ConfigDict, Field


class GetResultsRequest(BaseModel):
    """Request to retrieve simulation results."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    object_id: Optional[str] = Field(
        None,
        description="Optional object identifier for targeted results",
        min_length=1
    )

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
                "object_id": "stream-vapor",
            }
        }
    )
