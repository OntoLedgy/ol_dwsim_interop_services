# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Response models for sensitivity studies and optimization results."""

from typing import Dict, List, Literal, Optional

from pydantic import BaseModel, Field


class ResultRow(BaseModel):
    """Single row of sensitivity or sweep results."""

    inputs: Dict[str, float] = Field(
        ..., description="Input variable values for this run"
    )
    outputs: Optional[Dict[str, Optional[float]]] = Field(
        None,
        description="Output values for this run; nulls indicate failed outputs",
    )
    converged: bool = Field(..., description="True if the solver converged")
    error: Optional[str] = Field(None, description="Error message if run failed")


class StudyStatus(BaseModel):
    """Progress status for a running sensitivity study."""

    study_id: str = Field(..., description="Unique identifier for the study")
    status: Literal["running", "completed", "cancelled", "error"] = Field(
        ..., description="Current study status"
    )
    completed_steps: int = Field(
        ..., ge=0, description="Number of completed steps"
    )
    total_steps: int = Field(..., ge=0, description="Total number of steps")
    estimated_remaining_seconds: Optional[float] = Field(
        None, ge=0.0, description="Estimated seconds remaining"
    )


class SensitivityStudyResult(BaseModel):
    """Results for a completed or partial sensitivity study."""

    study_id: str = Field(..., description="Unique identifier for the study")
    status: Literal["running", "completed", "cancelled", "error"] = Field(
        ..., description="Final or current study status"
    )
    rows: List[ResultRow] = Field(
        default_factory=list, description="Collected result rows"
    )
    completed_steps: int = Field(
        ..., ge=0, description="Number of completed steps"
    )
    total_steps: int = Field(..., ge=0, description="Total number of steps")
    elapsed_seconds: float = Field(
        ..., ge=0.0, description="Elapsed time in seconds"
    )
    cancelled: bool = Field(
        False, description="True if the study was cancelled"
    )


class OptimizationResult(BaseModel):
    """Results from an optimization run."""

    study_id: str = Field(..., description="Unique identifier for the study")
    status: Literal["converged", "max_iterations", "error"] = Field(
        ..., description="Optimization status"
    )
    optimal_values: Dict[str, float] = Field(
        ..., description="Optimal variable values"
    )
    objective_value: float = Field(
        ..., description="Objective function value at optimum"
    )
    converged: bool = Field(..., description="True if optimization converged")
    message: Optional[str] = Field(
        None, description="Optional status or error message"
    )
