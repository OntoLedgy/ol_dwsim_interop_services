# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# This file is part of the OntoLedgy Thermodynamics Architecture and is
# dual-licensed:
#
#   1. Open source under the GNU Affero General Public License v3.0 or
#      later (AGPL-3.0-or-later). See the LICENSE file in the repository
#      root for the full licence text and NOTICE for attribution.
#   2. Commercial under a separate proprietary licence offered by
#      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Pydantic models for compound validation MCP tool inputs and outputs."""

from typing import List, Optional

from pydantic import BaseModel, Field, field_validator


class CompoundInfo(BaseModel):
    """Compound metadata for listing results."""

    name: str = Field(..., description="Canonical compound name", min_length=1)
    category: str = Field(..., description="Compound category (e.g., Hydrocarbon)", min_length=1)
    aliases: List[str] = Field(
        default_factory=list, description="Known aliases for the compound"
    )
    formula: Optional[str] = Field(
        None, description="Optional chemical formula, if available"
    )


class CompoundValidationResult(BaseModel):
    """Validation details for a requested compound name."""

    input_name: str = Field(..., description="Original input name provided", min_length=1)
    valid: bool = Field(..., description="True when the compound is recognized")
    canonical_name: Optional[str] = Field(
        None, description="Resolved canonical compound name when valid"
    )
    alias_used: bool = Field(
        ..., description="True when the input matched via an alias"
    )
    suggestions: List[str] = Field(
        default_factory=list, description="Suggested canonical names for near matches"
    )


class ValidateCompoundsInput(BaseModel):
    """Input for validate_compounds MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    compound_names: List[str] = Field(
        ..., description="Compound names to validate"
    )

    @field_validator("compound_names")
    @classmethod
    def validate_compound_names(cls, value: List[str]) -> List[str]:
        """Ensure at least one compound name is provided."""
        if len(value) < 1:
            raise ValueError("compound_names must contain at least 1 item")
        return value


class ValidateCompoundsOutput(BaseModel):
    """Output for validate_compounds MCP tool."""

    results: List[CompoundValidationResult] = Field(
        default_factory=list, description="Validation results per input name"
    )


class ListCompoundsInput(BaseModel):
    """Input for list_available_compounds MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    pattern: Optional[str] = Field(
        None, description="Optional name pattern filter"
    )
    category: Optional[str] = Field(
        None, description="Optional category filter"
    )
    limit: int = Field(50, description="Maximum number of compounds to return")
    offset: int = Field(0, description="Offset into the compound list for pagination")

    @field_validator("limit")
    @classmethod
    def validate_limit(cls, value: int) -> int:
        """Ensure limit is between 1 and 100."""
        if value < 1 or value > 100:
            raise ValueError("limit must be between 1 and 100")
        return value

    @field_validator("offset")
    @classmethod
    def validate_offset(cls, value: int) -> int:
        """Ensure offset is non-negative."""
        if value < 0:
            raise ValueError("offset must be >= 0")
        return value


class ListCompoundsOutput(BaseModel):
    """Output for list_available_compounds MCP tool."""

    compounds: List[CompoundInfo] = Field(
        default_factory=list, description="Compound entries matching the filters"
    )
    total_count: int = Field(
        ..., description="Total number of compounds matching the filters", ge=0
    )
    has_more: bool = Field(..., description="True when more results are available")
