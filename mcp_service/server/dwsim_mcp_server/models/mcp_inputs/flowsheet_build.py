# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Pydantic models for flowsheet-building MCP tool inputs and outputs."""

from typing import Any, Dict, List, Optional
import math
from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

COMPOSITION_TOLERANCE = 1e-6
SUPPORTED_PROPERTY_PACKAGES = (
    "peng-robinson",
    "srk",
    "nrtl",
    "psrk",
    "unifac",
)
SUPPORTED_UNIT_TYPES = (
    "separator",
    "mixer",
    "heater",
    "pump",
    "valve",
)


class AddCompoundInput(BaseModel):
    """Input for add_compound MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    compound_name: str = Field(..., description="Compound name in DWSIM databank", min_length=1)


class AddCompoundOutput(BaseModel):
    """Output for add_compound MCP tool."""

    compound_name: str = Field(..., description="Compound name that was requested")
    added: bool = Field(..., description="True if newly added, False if already present")


class SetPropertyPackageInput(BaseModel):
    """Input for set_property_package MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    package_name: str = Field(..., description="Supported property package name", min_length=1)
    options: Dict[str, str] = Field(default_factory=dict, description="Optional package options")

    @field_validator("package_name")
    @classmethod
    def validate_package(cls, value: str) -> str:
        """Ensure the property package is supported (case-insensitive)."""
        normalized = value.lower()
        if normalized not in SUPPORTED_PROPERTY_PACKAGES:
            raise ValueError(
                f"Unsupported property package '{value}'. Supported: {', '.join(SUPPORTED_PROPERTY_PACKAGES)}"
            )
        return normalized

    @field_validator("options")
    @classmethod
    def validate_options(cls, value: Dict[str, str]) -> Dict[str, str]:
        """Ensure option keys/values are non-empty strings."""
        for key, option_value in value.items():
            if not key:
                raise ValueError("option keys must be non-empty")
            if not option_value:
                raise ValueError(f"option '{key}' must be a non-empty string")
        return value


class SetPropertyPackageOutput(BaseModel):
    """Output for set_property_package MCP tool."""

    package_name: str = Field(..., description="Normalized property package name")
    applied: bool = Field(..., description="True when the package was applied to the session")


class AddStreamInput(BaseModel):
    """Input for add_stream MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    name: str = Field(..., description="Stream name", min_length=1)
    temperature: Optional[float] = Field(None, description="Temperature in K", gt=0.0)
    pressure: Optional[float] = Field(None, description="Pressure in Pa", gt=0.0)
    molar_flow: Optional[float] = Field(None, description="Molar flow in mol/s", gt=0.0)
    mass_flow: Optional[float] = Field(None, description="Mass flow in kg/s", gt=0.0)
    composition: Dict[str, float] = Field(
        default_factory=dict,
        description="Component mole fractions {compound_id: fraction}",
    )
    phase_hint: Optional[str] = Field(None, description="Optional phase hint: Vapor, Liquid, or Solid")
    is_source: bool = Field(
        default=False,
        description="True for feed streams (known conditions), False for outlet streams (calculated by DWSIM)",
    )

    @field_validator("composition")
    @classmethod
    def validate_composition(cls, value: Dict[str, float]) -> Dict[str, float]:
        """Ensure composition fractions are valid and sum within tolerance."""
        total = 0.0
        for key, fraction in value.items():
            if not key:
                raise ValueError("composition keys must be non-empty")
            if not math.isfinite(fraction):
                raise ValueError(f"composition fraction for '{key}' must be finite")
            if fraction < 0.0:
                raise ValueError(f"composition fraction for '{key}' must be >= 0")
            if fraction > 1.0:
                raise ValueError(f"composition fraction for '{key}' must be <= 1")
            total += fraction
        if total > 1.0 + COMPOSITION_TOLERANCE:
            raise ValueError("composition fractions must sum to <= 1 within tolerance")
        return value

    @model_validator(mode="after")
    def validate_flows(self) -> "AddStreamInput":
        """Require at least one of molar_flow or mass_flow when setting a source stream."""
        if self.is_source and self.molar_flow is None and self.mass_flow is None:
            raise ValueError("either molar_flow or mass_flow must be provided for source streams")
        return self


class AddStreamOutput(BaseModel):
    """Output for add_stream MCP tool."""

    stream_id: str = Field(..., description="Identifier of the created stream")
    name: str = Field(..., description="Stream name")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "stream_id": "stream-feed",
                "name": "feed",
            }
        }
    )


class AddUnitInput(BaseModel):
    """Input for add_unit MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    unit_type: str = Field(..., description="Unit operation type", min_length=1)
    name: str = Field(..., description="Unit name", min_length=1)
    parameters: Dict[str, Any] = Field(default_factory=dict, description="Unit parameters")

    @field_validator("unit_type")
    @classmethod
    def validate_unit_type(cls, value: str) -> str:
        """Ensure unit type is supported."""
        normalized = value.lower()
        if normalized not in SUPPORTED_UNIT_TYPES:
            raise ValueError(
                f"Unsupported unit_type '{value}'. Supported: {', '.join(SUPPORTED_UNIT_TYPES)}"
            )
        return normalized

    @field_validator("parameters")
    @classmethod
    def validate_parameters(cls, value: Dict[str, Any]) -> Dict[str, Any]:
        """Ensure parameter keys are non-empty and values are finite or accepted types."""
        for key, parameter_value in value.items():
            if not key:
                raise ValueError("parameter keys must be non-empty")
            if isinstance(parameter_value, (int, float)):
                if not math.isfinite(parameter_value):
                    raise ValueError(f"parameter '{key}' must be finite")
            elif not isinstance(parameter_value, (str, bool)):
                raise ValueError(
                    f"parameter '{key}' must be a number, string, or bool (got {type(parameter_value).__name__})"
                )
        return value


class AddUnitOutput(BaseModel):
    """Output for add_unit MCP tool."""

    unit_id: str = Field(..., description="Identifier of the created unit")
    name: str = Field(..., description="Unit name")
    unit_type: str = Field(..., description="Unit operation type")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "unit_id": "unit-sep-01",
                "name": "sep-01",
                "unit_type": "separator",
            }
        }
    )


class ConnectInput(BaseModel):
    """Input for connect MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    source_id: str = Field(..., description="Source object identifier", min_length=1)
    target_id: str = Field(..., description="Target object identifier", min_length=1)
    port_name: str = Field(..., description="Port name on the target object", min_length=1)


class ConnectOutput(BaseModel):
    """Output for connect MCP tool."""

    source_id: str = Field(..., description="Source object identifier")
    target_id: str = Field(..., description="Target object identifier")
    port_name: str = Field(..., description="Port used for the connection")
    connected: bool = Field(..., description="True when the connection was created")


class ListObjectsInput(BaseModel):
    """Input for list_objects MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)


class GetStreamPropertiesInput(BaseModel):
    """Input for get_stream_properties MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    stream_id: str = Field(..., description="Stream identifier", min_length=1)


class StreamSummary(BaseModel):
    """Summary of a stream object."""

    id: str = Field(..., description="Stream identifier", min_length=1)
    name: str = Field(..., description="Stream name", min_length=1)


class UnitSummary(BaseModel):
    """Summary of a unit operation."""

    id: str = Field(..., description="Unit identifier", min_length=1)
    name: str = Field(..., description="Unit name", min_length=1)
    unit_type: str = Field(..., description="Unit operation type", min_length=1)


class ConnectionSummary(BaseModel):
    """Summary of a connection between objects."""

    source_id: str = Field(..., description="Source object identifier", min_length=1)
    target_id: str = Field(..., description="Target object identifier", min_length=1)
    port_name: str = Field(..., description="Port used on the target", min_length=1)


class ListObjectsOutput(BaseModel):
    """Output for list_objects MCP tool."""

    streams: List[StreamSummary] = Field(default_factory=list, description="All streams in the session")
    units: List[UnitSummary] = Field(default_factory=list, description="All units in the session")
    connections: List[ConnectionSummary] = Field(
        default_factory=list, description="All connections in the session"
    )


class SetObjectParameterInput(BaseModel):
    """Input for set_object_parameter MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    object_id: str = Field(..., description="Target object identifier", min_length=1)
    parameter_name: str = Field(..., description="Parameter name", min_length=1)
    value: Any = Field(..., description="Value to set (type validated by downstream schema)")

    @field_validator("value")
    @classmethod
    def validate_value(cls, value: Any) -> Any:
        """Ensure value is JSON-serializable-friendly."""
        if isinstance(value, (int, float, str, bool)) or value is None:
            if isinstance(value, (int, float)) and not math.isfinite(value):
                raise ValueError("parameter value must be finite when numeric")
            return value
        raise ValueError("parameter value must be a number, string, bool, or null")


class SetObjectParameterOutput(BaseModel):
    """Output for set_object_parameter MCP tool."""

    object_id: str = Field(..., description="Target object identifier")
    parameter_name: str = Field(..., description="Parameter name")
    value: Any = Field(..., description="Applied value")
    previous_value: Optional[Any] = Field(
        None, description="Previous value if available, omitted when unavailable"
    )


class DeleteObjectInput(BaseModel):
    """Input for delete_object MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    object_id: str = Field(..., description="Object identifier to delete", min_length=1)


class DeleteObjectOutput(BaseModel):
    """Output for delete_object MCP tool."""

    object_id: str = Field(..., description="Deleted object identifier")
    deleted: bool = Field(..., description="True when object was deleted")
    removed_connections: List[ConnectionSummary] = Field(
        default_factory=list, description="Connections removed as part of deletion"
    )


class FlashStreamInput(BaseModel):
    """Input for flash_stream MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    stream_id: str = Field(..., description="Stream identifier to flash", min_length=1)


class FlashStreamOutput(BaseModel):
    """Output for flash_stream MCP tool."""

    stream_id: str = Field(..., description="Stream identifier that was flashed")
    flashed: bool = Field(..., description="True when flash calculation succeeded")


class SetBinaryInteractionParameterInput(BaseModel):
    """Input for set_binary_interaction_parameter MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    compound1: str = Field(..., description="First compound name", min_length=1)
    compound2: str = Field(..., description="Second compound name", min_length=1)
    value: float = Field(..., description="Binary interaction parameter value")

    @field_validator("value")
    @classmethod
    def validate_value(cls, value: float) -> float:
        """Ensure BIP value is finite."""
        if not math.isfinite(value):
            raise ValueError("BIP value must be finite")
        return value


class SetBinaryInteractionParameterOutput(BaseModel):
    """Output for set_binary_interaction_parameter MCP tool."""

    compound1: str = Field(..., description="First compound name")
    compound2: str = Field(..., description="Second compound name")
    value: float = Field(..., description="Applied BIP value")
    applied: bool = Field(..., description="True when BIP was applied")
