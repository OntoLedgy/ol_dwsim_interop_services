# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Input models for sensitivity analysis MCP tools."""

from typing import List, Literal, Optional
import math

from pydantic import BaseModel, Field, field_validator, model_validator


class VariableSpec(BaseModel):
    """Specification for a simulation variable."""

    object_id: str = Field(..., description="Target object identifier", min_length=1)
    property_name: str = Field(..., description="Property name on the object", min_length=1)


class RangeSpec(BaseModel):
    """Specification for a numeric value range."""

    min_value: float = Field(..., description="Minimum value for the range")
    max_value: float = Field(..., description="Maximum value for the range")

    @model_validator(mode="after")
    def validate_range(self) -> "RangeSpec":
        """Ensure the range bounds are ordered and finite."""
        if not math.isfinite(self.min_value) or not math.isfinite(self.max_value):
            raise ValueError("range bounds must be finite")
        if self.min_value >= self.max_value:
            raise ValueError("range min_value must be less than max_value")
        return self


class OutputSpec(BaseModel):
    """Specification for an output to collect."""

    object_id: str = Field(..., description="Target object identifier", min_length=1)
    property_name: str = Field(..., description="Property name on the object", min_length=1)


class SweepVariableSpec(VariableSpec):
    """Variable specification for parameter sweep studies."""

    range: RangeSpec = Field(..., description="Range for the variable")
    steps: int = Field(..., description="Number of steps for this variable", ge=2, le=100)

    @field_validator("steps")
    @classmethod
    def validate_steps(cls, value: int) -> int:
        """Ensure steps are within supported bounds."""
        if value < 2 or value > 100:
            raise ValueError("steps must be between 2 and 100")
        return value


class SensitivityAnalysisRequest(BaseModel):
    """Input for single-variable sensitivity analysis."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    variable: VariableSpec = Field(..., description="Variable to vary")
    range: RangeSpec = Field(..., description="Range for the variable values")
    steps: int = Field(..., description="Number of steps to evaluate", ge=2, le=100)
    outputs: List[OutputSpec] = Field(..., description="Outputs to collect", min_length=1)

    @field_validator("steps")
    @classmethod
    def validate_steps(cls, value: int) -> int:
        """Ensure steps are within supported bounds."""
        if value < 2 or value > 100:
            raise ValueError("steps must be between 2 and 100")
        return value


class ParameterSweepRequest(BaseModel):
    """Input for multi-variable parameter sweep."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    variables: List[SweepVariableSpec] = Field(..., description="Variables to sweep", min_length=1)
    max_combinations: int = Field(
        1000,
        description="Maximum total combinations allowed for the sweep",
        ge=1,
    )
    outputs: List[OutputSpec] = Field(..., description="Outputs to collect", min_length=1)


class ObjectiveSpec(BaseModel):
    """Specification for an optimization objective."""

    object_id: str = Field(..., description="Target object identifier", min_length=1)
    property_name: str = Field(..., description="Property name on the object", min_length=1)
    direction: Literal["minimize", "maximize"] = Field(
        ...,
        description="Optimization direction for the objective",
    )


class VariableWithBounds(BaseModel):
    """Specification for an optimization variable with bounds."""

    object_id: str = Field(..., description="Target object identifier", min_length=1)
    property_name: str = Field(..., description="Property name on the object", min_length=1)
    lower: float = Field(..., description="Lower bound for the variable")
    upper: float = Field(..., description="Upper bound for the variable")
    initial: Optional[float] = Field(
        None,
        description="Optional initial value for the optimizer",
    )

    @model_validator(mode="after")
    def validate_bounds(self) -> "VariableWithBounds":
        """Ensure bounds are finite and ordered."""
        if not math.isfinite(self.lower) or not math.isfinite(self.upper):
            raise ValueError("bounds must be finite")
        if self.lower >= self.upper:
            raise ValueError("lower must be less than upper")
        return self


class ConstraintSpec(BaseModel):
    """Specification for an optimization constraint."""

    object_id: str = Field(..., description="Target object identifier", min_length=1)
    property_name: str = Field(..., description="Property name on the object", min_length=1)
    operator: Literal["<=", ">=", "=="] = Field(
        ...,
        description="Constraint operator",
    )
    threshold: float = Field(..., description="Constraint threshold value")


class OptimizationRequest(BaseModel):
    """Input for optimization requests."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    objective: ObjectiveSpec = Field(..., description="Objective to optimize")
    variables: List[VariableWithBounds] = Field(
        ...,
        description="Optimization variables with bounds",
        min_length=1,
    )
    constraints: Optional[List[ConstraintSpec]] = Field(
        None,
        description="Optional constraints to enforce",
    )
    max_iterations: int = Field(
        100,
        description="Maximum optimization iterations",
        ge=1,
    )
