"""SessionResultResource model for detailed simulation results."""

from typing import Any, Dict, Literal, Optional
from pydantic import BaseModel, ConfigDict, Field


class SessionResultResource(BaseModel):
    """
    Detailed simulation results for a session object.

    Used to serve large result sets via MCP resources that would be too large
    for tool responses. Properties use CAPE-OPEN standard names and SI units.
    """

    session_id: str = Field(
        ...,
        description="Session identifier",
    )
    object_id: Optional[str] = Field(
        None,
        description="Specific object identifier (stream or unit), None for all objects",
    )
    object_type: Literal["stream", "unit", "flowsheet"] = Field(
        ...,
        description="Type of object: stream, unit, or flowsheet (for overall results)",
    )
    object_name: Optional[str] = Field(
        None,
        description="Human-readable object name",
    )
    properties: Dict[str, Any] = Field(
        default_factory=dict,
        description="Property values using CAPE-OPEN standard names",
    )
    units: Dict[str, str] = Field(
        default_factory=dict,
        description="SI unit labels for each property (e.g., {'temperature': 'K', 'pressure': 'Pa'})",
    )
    phase_properties: Optional[Dict[str, Dict[str, Any]]] = Field(
        None,
        description="Per-phase properties for multi-phase streams",
    )

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                "object_id": "stream-001",
                "object_type": "stream",
                "object_name": "Feed Stream",
                "properties": {
                    "temperature": 298.15,
                    "pressure": 101325.0,
                    "molarFlow": 100.0,
                    "vaporFraction": 0.5,
                },
                "units": {
                    "temperature": "K",
                    "pressure": "Pa",
                    "molarFlow": "mol/s",
                    "vaporFraction": "-",
                },
                "phase_properties": {
                    "Vapor": {
                        "moleFraction": 0.5,
                        "temperature": 298.15,
                    },
                    "Liquid": {
                        "moleFraction": 0.5,
                        "temperature": 298.15,
                    },
                },
            }
        }
    )
