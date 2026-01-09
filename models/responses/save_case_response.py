"""SaveCaseResponse DTO for MCP save_case tool."""

from pydantic import BaseModel, ConfigDict, Field


class SaveCaseResponse(BaseModel):
    """Response from saving a DWSIM case."""

    success: bool = Field(..., description="Whether the case was saved")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "success": True,
            }
        }
    )
