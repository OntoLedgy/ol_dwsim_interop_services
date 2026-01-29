"""SimulationResultResponse DTO for MCP simulation results."""

from typing import List, Optional
from pydantic import BaseModel, ConfigDict, Field

from .stream_properties_response import StreamPropertiesResponse


class SimulationResultResponse(BaseModel):
    """Response containing simulation results."""

    status: str = Field(
        ...,
        description="Simulation status: converged, failed, timeout, not_run",
    )
    convergence_state: str = Field(
        ...,
        description="Convergence state: Converged, NotConverged, Error",
    )
    elapsed_ms: float = Field(..., description="Calculation duration in milliseconds")
    stream_results: List[StreamPropertiesResponse] = Field(
        default_factory=list,
        description="Calculated stream properties",
    )
    messages: List[str] = Field(
        default_factory=list,
        description="Solver diagnostic messages",
    )
    mass_balance_valid: Optional[bool] = Field(
        None,
        description="Mass balance validation result",
    )
    mass_balance_error_percent: Optional[float] = Field(
        None,
        description="Mass balance error percentage",
    )

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "status": "converged",
                "convergence_state": "Converged",
                "elapsed_ms": 1250.0,
                "stream_results": [
                    {
                        "id": "stream-001",
                        "name": "Vapor Outlet",
                        "temperature_k": 350.0,
                        "pressure_pa": 101325.0,
                        "total_molar_flow_mol_per_s": 10.5,
                        "phases": [],
                    }
                ],
                "messages": ["Calculation converged successfully."],
                "mass_balance_valid": True,
                "mass_balance_error_percent": 0.4,
            }
        }
    )
