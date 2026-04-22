"""Translate between canonical DTOs and DWSIM-specific payloads."""

from __future__ import annotations

from typing import Mapping, TypedDict

from ol_simulator_interop_services.domain.errors import AdapterCapabilityError, ComponentNotFoundError
from ol_simulator_interop_services.domain.models import (
    CanonicalComponent,
    Composition,
    CompositionEntry,
    FlashCalculationType,
    FlashResult,
    Phase,
    PhaseKind,
    PropertyBundle,
    PropertyValue,
    Provenance,
)

from dwsim_mcp_server.adapter.alias_seeder import (
    SEEDED_DWSIM_COMPONENTS_BY_BACKEND_NAME,
    SEEDED_DWSIM_COMPONENTS_BY_CAS_NUMBER,
)


class DwsimCompositionEntry(TypedDict):
    """DWSIM compound fraction payload."""

    compound: str
    mole_fraction: float


class DwsimPhaseDict(TypedDict, total=False):
    """Minimal DWSIM phase payload used by the canonical adapter."""

    phase_label: str
    phase_fraction: float
    composition: list[DwsimCompositionEntry]
    properties: dict[str, float]


class DwsimFlashResultDict(TypedDict, total=False):
    """Minimal DWSIM flash payload used by the canonical adapter."""

    calculation_type: str
    temperature_k: float
    pressure_pa: float
    converged: bool
    phases: list[DwsimPhaseDict]
    overall_properties: dict[str, float]
    message: str
    iteration_count: int
    elapsed_ms: float


class DwsimPhaseEnvelopePointDict(TypedDict):
    """DWSIM phase-envelope point payload."""

    temperature_kelvin: float
    pressure_pascal: float
    phase_labels: list[str]


_PROPERTY_MAP: dict[str, tuple[str, str | None, float]] = {
    "temperature_k": ("temperature", "K", 1.0),
    "pressure_pa": ("pressure", "Pa", 1.0),
    "total_molar_flow_mol_per_s": ("total_molar_flow", "mol/s", 1.0),
    "molar_flow_mol_per_s": ("molar_flow", "mol/s", 1.0),
    "mass_flow_kg_per_s": ("mass_flow", "kg/s", 1.0),
    "volumetric_flow_m3_per_s": ("volumetric_flow", "m3/s", 1.0),
    "mass_fraction": ("mass_fraction", None, 1.0),
    "volumetric_fraction": ("volumetric_fraction", None, 1.0),
    "density_kg_per_m3": ("density", "kg/m3", 1.0),
    "viscosity_pa_s": ("viscosity", "Pa*s", 1.0),
    "molecular_weight_kg_per_kmol": ("molecular_weight", "kg/kmol", 1.0),
    "enthalpy_kj_per_kg": ("specific_enthalpy", "kJ/kg", 1.0),
    "molar_enthalpy_kj_per_kmol": ("molar_enthalpy", "J/mol", 1.0),
    "entropy_kj_per_kg_k": ("specific_entropy", "kJ/(kg*K)", 1.0),
    "molar_entropy_kj_per_kmol_k": ("molar_entropy", "J/(mol*K)", 1.0),
    "gibbs_free_energy": ("gibbs_free_energy", None, 1.0),
    "helmholtz_energy": ("helmholtz_energy", None, 1.0),
    "internal_energy": ("internal_energy", None, 1.0),
    "k_value": ("k_value", None, 1.0),
    "fugacity": ("fugacity", None, 1.0),
    "activity_coefficient": ("activity_coefficient", None, 1.0),
}


def dwsim_result_to_flash_result(
    raw_result: DwsimFlashResultDict,
    *,
    provenance: Provenance,
    fallback_composition: Composition,
) -> FlashResult:
    """Translate a DWSIM flash payload into the canonical flash result."""

    phases = tuple(
        dwsim_phase_to_canonical(phase_payload)
        for phase_payload in raw_result.get("phases", [])
    )
    if not phases:
        phases = (_build_fallback_phase(raw_result, fallback_composition),)

    overall_payload: dict[str, float] = {}
    overall_payload["temperature_k"] = raw_result.get("temperature_k", 0.0)
    overall_payload["pressure_pa"] = raw_result.get("pressure_pa", 0.0)
    overall_payload.update(raw_result.get("overall_properties", {}))

    return FlashResult(
        phases=phases,
        converged=raw_result.get("converged", False),
        provenance=provenance,
        message=raw_result.get("message"),
        iteration_count=raw_result.get("iteration_count"),
        overall_properties=dwsim_properties_to_property_bundle(overall_payload),
    )


def composition_to_dwsim(composition: Composition) -> tuple[list[str], list[float]]:
    """Translate canonical composition into parallel DWSIM compound lists."""

    compound_names: list[str] = []
    mole_fractions: list[float] = []
    for entry in composition.entries:
        seeded_component = SEEDED_DWSIM_COMPONENTS_BY_CAS_NUMBER.get(entry.component.cas_number)
        if seeded_component is None:
            raise ComponentNotFoundError(
                f"no seeded DWSIM alias exists for CAS {entry.component.cas_number}"
            )
        compound_names.append(seeded_component.backend_name)
        mole_fractions.append(entry.mole_fraction)
    return compound_names, mole_fractions


def dwsim_phase_to_canonical(phase_payload: DwsimPhaseDict) -> Phase:
    """Translate a DWSIM phase payload into a canonical phase."""

    phase_kind = dwsim_phase_label_to_kind(phase_payload.get("phase_label", ""))
    phase_properties = dwsim_properties_to_property_bundle(
        phase_payload.get("properties", {}),
        basis=phase_kind.value,
    )
    density = _require_density(phase_payload.get("properties", {}))
    return Phase(
        phase_kind=phase_kind,
        fraction=phase_payload.get("phase_fraction", 0.0),
        composition=_dwsim_composition_to_canonical(phase_payload.get("composition", [])),
        density_kg_per_m3=density,
        properties=phase_properties,
    )


def flash_type_to_dwsim(flash_calculation_type: FlashCalculationType) -> str:
    """Translate canonical flash identifiers to DWSIM strings."""

    mapping = {
        FlashCalculationType.TEMPERATURE_PRESSURE: "TP",
        FlashCalculationType.PRESSURE_ENTHALPY: "PH",
        FlashCalculationType.PRESSURE_ENTROPY: "PS",
        FlashCalculationType.TEMPERATURE_PRESSURE_VAPOR_FRACTION: "TPQ",
        FlashCalculationType.PRESSURE_VAPOR_FRACTION: "PQ",
    }
    return mapping[flash_calculation_type]


def dwsim_phase_label_to_kind(phase_label: str) -> PhaseKind:
    """Map DWSIM phase labels onto canonical phase kinds."""

    normalized_label = phase_label.strip().lower()
    if normalized_label in {"vapor", "vapour", "gas"}:
        return PhaseKind.VAPOR
    if normalized_label in {"liquid", "liquid1", "liq", "l1"}:
        return PhaseKind.LIQUID
    if normalized_label in {"liquid2", "liquid_secondary", "liq2", "l2"}:
        return PhaseKind.LIQUID_SECONDARY
    if normalized_label in {"aqueous", "water", "aq"}:
        return PhaseKind.AQUEOUS
    if normalized_label in {"solid", "sol", "s"}:
        return PhaseKind.SOLID
    if normalized_label in {"overall", "mixture", "bulk", ""}:
        return PhaseKind.OVERALL
    raise AdapterCapabilityError(f"unsupported DWSIM phase label: {phase_label}")


def dwsim_properties_to_property_bundle(
    raw_properties: Mapping[str, float],
    *,
    basis: str | None = None,
) -> PropertyBundle:
    """Translate DWSIM property mappings into a canonical property bundle."""

    properties: list[PropertyValue] = []
    seen_property_ids: set[str] = set()
    for raw_property_id, raw_value in raw_properties.items():
        mapped_property = _PROPERTY_MAP.get(raw_property_id)
        if mapped_property is None:
            continue
        property_id, unit, factor = mapped_property
        if property_id in seen_property_ids:
            continue
        properties.append(
            PropertyValue(
                property_id=property_id,
                value=raw_value * factor,
                unit=unit,
                basis=basis,
            )
        )
        seen_property_ids.add(property_id)
    return PropertyBundle(properties=tuple(properties))


def _dwsim_composition_to_canonical(
    composition_entries: list[DwsimCompositionEntry],
) -> Composition:
    entries = tuple(
        CompositionEntry(
            component=_seeded_component_to_canonical(entry["compound"]).reference(),
            mole_fraction=entry["mole_fraction"],
        )
        for entry in composition_entries
    )
    return Composition(entries=entries)


def _seeded_component_to_canonical(backend_name: str) -> CanonicalComponent:
    seeded_component = SEEDED_DWSIM_COMPONENTS_BY_BACKEND_NAME.get(backend_name)
    if seeded_component is None:
        raise ComponentNotFoundError(f"no seeded canonical component exists for {backend_name}")
    return CanonicalComponent(
        cas_number=seeded_component.cas_number,
        molecular_formula=seeded_component.molecular_formula,
        common_name=seeded_component.common_name,
    )


def _require_density(raw_properties: Mapping[str, float]) -> float:
    density = raw_properties.get("density_kg_per_m3")
    if density is None or density <= 0.0:
        raise AdapterCapabilityError("DWSIM phase payload does not include a positive density")
    return density


def _build_fallback_phase(
    raw_result: DwsimFlashResultDict,
    fallback_composition: Composition,
) -> Phase:
    overall_properties = raw_result.get("overall_properties", {})
    fallback_density = overall_properties.get("density_kg_per_m3", 1.0)
    if fallback_density <= 0.0:
        fallback_density = 1.0
    return Phase(
        phase_kind=PhaseKind.OVERALL,
        fraction=1.0,
        composition=fallback_composition,
        density_kg_per_m3=fallback_density,
        properties=dwsim_properties_to_property_bundle(overall_properties, basis=PhaseKind.OVERALL.value),
    )
