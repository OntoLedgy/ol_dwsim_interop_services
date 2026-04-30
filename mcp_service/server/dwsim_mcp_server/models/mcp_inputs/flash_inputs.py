# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Input models for flash calculation MCP tools."""

from typing import List
import math

from pydantic import BaseModel, Field, field_validator, model_validator

from dwsim_mcp_server.models.measurements.measurements import Measurements

COMPOSITION_TOLERANCE = 0.001


class FlashTPInputs(BaseModel):
    """Input for temperature-pressure flash calculation."""

    session_id: str = Field(..., description="Session with configured property package", min_length=1)
    compounds: List[str] = Field(..., description="Compound names", min_length=1)
    composition: List[float] = Field(
        ..., description="Component mole fractions (must sum to 1.0)", min_length=1
    )
    temperature: Measurements = Field(..., description="Temperature measurement")
    pressure: Measurements = Field(..., description="Pressure measurement")

    @field_validator("composition")
    @classmethod
    def validate_composition_sum(cls, value: List[float]) -> List[float]:
        """Ensure composition values are finite and sum to 1.0 within tolerance."""
        total = 0.0
        for fraction in value:
            if not math.isfinite(fraction):
                raise ValueError("composition fractions must be finite")
            if fraction < 0.0:
                raise ValueError("composition fractions must be >= 0.0")
            if fraction > 1.0:
                raise ValueError("composition fractions must be <= 1.0")
            total += fraction
        if abs(total - 1.0) > COMPOSITION_TOLERANCE:
            raise ValueError(f"composition must sum to 1.0 within {COMPOSITION_TOLERANCE}")
        return value

    @model_validator(mode="after")
    def validate_composition_length(self) -> "FlashTPInputs":
        """Ensure composition length matches compounds length."""
        if len(self.compounds) != len(self.composition):
            raise ValueError(
                f"composition length ({len(self.composition)}) must match compounds length ({len(self.compounds)})"
            )
        return self


class FlashPHInputs(BaseModel):
    """Input for pressure-enthalpy flash calculation."""

    session_id: str = Field(..., description="Session with configured property package", min_length=1)
    compounds: List[str] = Field(..., description="Compound names", min_length=1)
    composition: List[float] = Field(
        ..., description="Component mole fractions (must sum to 1.0)", min_length=1
    )
    pressure: Measurements = Field(..., description="Pressure measurement")
    enthalpy: Measurements = Field(..., description="Molar enthalpy measurement")

    @field_validator("composition")
    @classmethod
    def validate_composition_sum(cls, value: List[float]) -> List[float]:
        """Ensure composition values are finite and sum to 1.0 within tolerance."""
        total = 0.0
        for fraction in value:
            if not math.isfinite(fraction):
                raise ValueError("composition fractions must be finite")
            if fraction < 0.0:
                raise ValueError("composition fractions must be >= 0.0")
            if fraction > 1.0:
                raise ValueError("composition fractions must be <= 1.0")
            total += fraction
        if abs(total - 1.0) > COMPOSITION_TOLERANCE:
            raise ValueError(f"composition must sum to 1.0 within {COMPOSITION_TOLERANCE}")
        return value

    @model_validator(mode="after")
    def validate_composition_length(self) -> "FlashPHInputs":
        """Ensure composition length matches compounds length."""
        if len(self.compounds) != len(self.composition):
            raise ValueError(
                f"composition length ({len(self.composition)}) must match compounds length ({len(self.compounds)})"
            )
        return self


class FlashPSInputs(BaseModel):
    """Input for pressure-entropy flash calculation."""

    session_id: str = Field(..., description="Session with configured property package", min_length=1)
    compounds: List[str] = Field(..., description="Compound names", min_length=1)
    composition: List[float] = Field(
        ..., description="Component mole fractions (must sum to 1.0)", min_length=1
    )
    pressure: Measurements = Field(..., description="Pressure measurement")
    entropy: Measurements = Field(..., description="Molar entropy measurement")

    @field_validator("composition")
    @classmethod
    def validate_composition_sum(cls, value: List[float]) -> List[float]:
        """Ensure composition values are finite and sum to 1.0 within tolerance."""
        total = 0.0
        for fraction in value:
            if not math.isfinite(fraction):
                raise ValueError("composition fractions must be finite")
            if fraction < 0.0:
                raise ValueError("composition fractions must be >= 0.0")
            if fraction > 1.0:
                raise ValueError("composition fractions must be <= 1.0")
            total += fraction
        if abs(total - 1.0) > COMPOSITION_TOLERANCE:
            raise ValueError(f"composition must sum to 1.0 within {COMPOSITION_TOLERANCE}")
        return value

    @model_validator(mode="after")
    def validate_composition_length(self) -> "FlashPSInputs":
        """Ensure composition length matches compounds length."""
        if len(self.compounds) != len(self.composition):
            raise ValueError(
                f"composition length ({len(self.composition)}) must match compounds length ({len(self.compounds)})"
            )
        return self
