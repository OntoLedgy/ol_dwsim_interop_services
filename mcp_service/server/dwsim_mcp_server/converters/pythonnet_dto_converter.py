"""Conversion helpers between Pydantic models and pythonnet C# DTOs."""

from __future__ import annotations

from typing import Any, Dict, Iterable, Optional, Tuple, Type

from models.cape_open.material_stream import MaterialStream
from models.cape_open.thermo_property_package import ThermoPropertyPackage
from models.cape_open.unit_operation import UnitOperation


def to_csharp_material_stream(source: MaterialStream | Dict[str, Any]):
    """Convert a MaterialStream model or dict to a C# MaterialStreamDto."""
    model = _ensure_model(MaterialStream, source)
    dto_types = _load_csharp_dto_types()
    material_dto_type = dto_types["MaterialStreamDto"]
    phase_dto_type = dto_types["PhaseDto"]
    compound_dto_type = dto_types["CompoundFractionDto"]

    if not model.composition:
        raise ValueError("MaterialStream composition is required for C# DTO conversion.")

    phase_label = model.phases[0] if model.phases else "overall"
    phase_fraction = model.vapor_fraction if model.vapor_fraction is not None else 1.0

    phase_dto = phase_dto_type()
    phase_dto.PhaseLabel = phase_label
    phase_dto.PhaseFraction = float(phase_fraction)
    phase_dto.Composition = [
        _create_compound_fraction(compound_dto_type, compound, fraction)
        for compound, fraction in model.composition.items()
    ]

    dto = material_dto_type()
    dto.Id = None
    dto.Name = model.name
    dto.TemperatureK = float(model.temperature or 0.0)
    dto.PressurePa = float(model.pressure or 0.0)
    dto.TotalMolarFlowMolPerS = float(model.molar_flow or 0.0)
    dto.Phases = [phase_dto]
    return dto


def from_csharp_material_stream(dto) -> MaterialStream:
    """Convert a C# MaterialStreamDto to a MaterialStream model."""
    phases = getattr(dto, "Phases", None) or []
    composition = _first_phase_composition(phases)
    phase_labels = [getattr(phase, "PhaseLabel", "") for phase in phases if phase is not None]

    return MaterialStream(
        name=getattr(dto, "Name", ""),
        temperature=_get_attr(dto, "TemperatureK"),
        pressure=_get_attr(dto, "PressurePa"),
        molar_flow=_get_attr(dto, "TotalMolarFlowMolPerS"),
        composition=composition,
        phases=phase_labels,
    )


def to_csharp_property_package(source: ThermoPropertyPackage | Dict[str, Any]):
    """Convert a ThermoPropertyPackage model or dict to a C# PropertyPackageDto."""
    model = _ensure_model(ThermoPropertyPackage, source)
    dto_types = _load_csharp_dto_types()
    property_dto_type = dto_types["PropertyPackageDto"]

    package_type = _get_required_option(model.options, "package_type")
    dto = property_dto_type()
    dto.Id = None
    dto.Name = model.name
    dto.PackageType = package_type
    dto.Parameters = {key: str(value) for key, value in model.parameters.items()}
    return dto


def from_csharp_property_package(dto) -> ThermoPropertyPackage:
    """Convert a C# PropertyPackageDto to a ThermoPropertyPackage model."""
    raw_params = getattr(dto, "Parameters", None) or {}
    parameters = {key: _parse_float(value, key) for key, value in raw_params.items()}

    return ThermoPropertyPackage(
        name=getattr(dto, "Name", ""),
        parameters=parameters,
        options={"package_type": getattr(dto, "PackageType", "")},
    )


def to_csharp_unit_operation(source: UnitOperation | Dict[str, Any]):
    """Convert a UnitOperation model or dict to a C# UnitOperationDto."""
    model = _ensure_model(UnitOperation, source)
    dto_types = _load_csharp_dto_types()
    unit_dto_type = dto_types["UnitOperationDto"]

    dto = unit_dto_type()
    dto.Id = model.id
    dto.Name = model.name
    dto.UnitType = model.unit_type
    dto.Parameters = {key: float(value) for key, value in model.parameters.items()}
    return dto


def from_csharp_unit_operation(dto) -> UnitOperation:
    """Convert a C# UnitOperationDto to a UnitOperation model."""
    parameters = getattr(dto, "Parameters", None) or {}
    return UnitOperation(
        id=getattr(dto, "Id", ""),
        name=getattr(dto, "Name", ""),
        unit_type=getattr(dto, "UnitType", ""),
        parameters={key: float(value) for key, value in parameters.items()},
    )


def _ensure_model(model_type: Type[Any], source: Any):
    if isinstance(source, model_type):
        return source
    if isinstance(source, dict):
        return model_type.model_validate(source)
    raise TypeError(f"Unsupported source type for {model_type.__name__}: {type(source)}")


def _load_csharp_dto_types() -> Dict[str, Type[Any]]:
    """Load pythonnet C# DTO types on demand."""
    try:
        from DwsimWorker.Contracts.CapeOpen import (  # type: ignore
            CompoundFractionDto,
            MaterialStreamDto,
            PhaseDto,
            PropertyPackageDto,
            UnitOperationDto,
        )
    except Exception as exc:
        raise RuntimeError("Failed to load C# DTO types via pythonnet.") from exc

    return {
        "CompoundFractionDto": CompoundFractionDto,
        "MaterialStreamDto": MaterialStreamDto,
        "PhaseDto": PhaseDto,
        "PropertyPackageDto": PropertyPackageDto,
        "UnitOperationDto": UnitOperationDto,
    }


def _create_compound_fraction(
    dto_type: Type[Any],
    compound: str,
    fraction: float,
):
    dto = dto_type()
    dto.Compound = compound
    dto.MoleFraction = float(fraction)
    return dto


def _get_required_option(options: Dict[str, str], key: str) -> str:
    value = options.get(key) or options.get(key.replace("_", ""))
    if not value:
        raise ValueError(f"Property package option '{key}' is required for conversion.")
    return value


def _parse_float(value: Any, key: str) -> float:
    try:
        return float(value)
    except (TypeError, ValueError) as exc:
        raise ValueError(f"Property package parameter '{key}' must be numeric.") from exc


def _first_phase_composition(phases: Iterable[Any]) -> Dict[str, float]:
    for phase in phases:
        if phase is None:
            continue
        composition = getattr(phase, "Composition", None) or []
        return {
            getattr(item, "Compound", ""): float(getattr(item, "MoleFraction", 0.0))
            for item in composition
            if getattr(item, "Compound", None)
        }
    return {}


def _get_attr(dto: Any, name: str) -> Optional[float]:
    value = getattr(dto, name, None)
    if value is None:
        return None
    return float(value)
