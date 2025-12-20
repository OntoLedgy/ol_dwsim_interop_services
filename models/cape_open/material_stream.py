"""MaterialStream model implementing CAPE-OPEN ICapeThermoMaterialObject interface."""

from typing import Dict, List, Optional
from pydantic import BaseModel, Field


class MaterialStream(BaseModel):
    """
    Represents a material stream in a process flowsheet.

    Based on CAPE-OPEN ICapeThermoMaterialObject interface for cross-simulator
    interoperability.
    """

    name: str = Field(..., description="Stream name")
    temperature: Optional[float] = Field(None, description="Temperature in K")
    pressure: Optional[float] = Field(None, description="Pressure in Pa")
    molar_flow: Optional[float] = Field(None, description="Total molar flow in mol/s")
    mass_flow: Optional[float] = Field(None, description="Total mass flow in kg/s")
    vapor_fraction: Optional[float] = Field(None, description="Vapor mole fraction (0-1)")

    composition: Dict[str, float] = Field(
        default_factory=dict,
        description="Component mole fractions {compound_id: fraction}"
    )

    phases: List[str] = Field(
        default_factory=list,
        description="Present phases: Vapor, Liquid, Solid"
    )

    properties: Dict[str, float] = Field(
        default_factory=dict,
        description="Additional stream properties {property_name: value}"
    )

    class Config:
        """Pydantic configuration."""
        json_schema_extra = {
            "example": {
                "name": "Feed Stream",
                "temperature": 298.15,
                "pressure": 101325.0,
                "molar_flow": 100.0,
                "vapor_fraction": 0.0,
                "composition": {
                    "water": 0.5,
                    "ethanol": 0.5
                },
                "phases": ["Liquid"]
            }
        }
