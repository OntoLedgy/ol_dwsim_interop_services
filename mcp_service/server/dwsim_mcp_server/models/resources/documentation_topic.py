# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""DocumentationTopic model for documentation resources."""

from typing import List
from pydantic import BaseModel, ConfigDict, Field, field_validator


class DocumentationTopic(BaseModel):
    """
    Metadata for a documentation topic.

    Used to describe available documentation that agents can access.
    """

    topic: str = Field(
        ...,
        description="Topic identifier (e.g., unit-operations, property-packages)",
    )
    title: str = Field(
        ...,
        description="Human-readable title for the documentation",
    )
    description: str = Field(
        ...,
        description="Brief summary of what the documentation covers",
    )
    sections: List[str] = Field(
        default_factory=list,
        description="List of section headings in the documentation",
    )

    @field_validator("topic")
    @classmethod
    def validate_topic(cls, v: str) -> str:
        """Ensure topic is a valid identifier."""
        if not v or not v.strip():
            raise ValueError("Topic cannot be empty")
        return v.strip().lower().replace(" ", "-")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "topic": "unit-operations",
                "title": "Unit Operations Guide",
                "description": "Reference documentation for DWSIM unit operations",
                "sections": [
                    "Overview",
                    "Separators",
                    "Heat Exchangers",
                    "Pumps and Compressors",
                    "Reactors",
                ],
            }
        }
    )
