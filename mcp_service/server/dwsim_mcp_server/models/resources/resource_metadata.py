"""ResourceMetadata model for MCP resource discovery."""

from pydantic import BaseModel, ConfigDict, Field


class ResourceMetadata(BaseModel):
    """
    Metadata describing an MCP resource.

    Used for resource listing and discovery by LLM agents.
    """

    uri: str = Field(
        ...,
        description="Unique resource URI (e.g., resource://docs/unit-operations)",
    )
    name: str = Field(
        ...,
        description="Human-readable resource name",
    )
    description: str = Field(
        ...,
        description="Brief description of the resource content",
    )
    mime_type: str = Field(
        default="application/json",
        description="Content MIME type (application/json or text/markdown)",
    )

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "uri": "resource://docs/unit-operations",
                "name": "Unit Operations Documentation",
                "description": "Reference guide for DWSIM unit operations and their parameters",
                "mime_type": "text/markdown",
            }
        }
    )
