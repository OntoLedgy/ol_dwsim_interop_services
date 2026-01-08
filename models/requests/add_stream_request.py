"""AddStreamRequest DTO for MCP add_stream tool."""

from typing import Dict, Optional
from pydantic import BaseModel, ConfigDict, Field, field_validator


class AddStreamRequest(BaseModel):
    """Request to add a material stream to a flowsheet."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    name: str = Field(..., description="Stream name", min_length=1)
    temperature: Optional[float] = Field(None, description="Temperature in K", ge=0.0)
    pressure: Optional[float] = Field(None, description="Pressure in Pa", ge=0.0)
    molar_flow: Optional[float] = Field(None, description="Total molar flow in mol/s", ge=0.0)
    mass_flow: Optional[float] = Field(None, description="Total mass flow in kg/s", ge=0.0)
    composition: Dict[str, float] = Field(
        default_factory=dict,
        description="Component mole fractions {compound_id: fraction}"
    )

    @field_validator("composition")
    @classmethod
    def validate_composition(cls, value: Dict[str, float]) -> Dict[str, float]:
        """Ensure composition fractions are non-negative and sum to <= 1."""
        total = 0.0
        for key, fraction in value.items():
            if not key:
                raise ValueError("composition keys must be non-empty")
            if fraction < 0.0:
                raise ValueError(f"composition fraction for '{key}' must be >= 0")
            if fraction > 1.0:
                raise ValueError(f"composition fraction for '{key}' must be <= 1")
            total += fraction
        if total > 1.0 + 1e-6:
            raise ValueError("composition fractions must sum to <= 1")
        return value

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "session_id": "session-1234",
                "name": "Feed Stream",
                "temperature": 298.15,
                "pressure": 101325.0,
                "molar_flow": 100.0,
                "composition": {
                    "water": 0.5,
                    "ethanol": 0.5,
                },
            }
        }
    )
