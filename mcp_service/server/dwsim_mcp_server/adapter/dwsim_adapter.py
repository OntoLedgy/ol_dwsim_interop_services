"""Canonical DWSIM simulator adapter."""

from __future__ import annotations

import inspect
import re
from collections.abc import Awaitable, Sequence
from time import perf_counter
from typing import Protocol, TypeVar, TypedDict, cast, runtime_checkable

from ol_simulator_interop_services.domain.errors import AdapterCapabilityError, PropertyPackageNotFoundError
from ol_simulator_interop_services.domain.models import (
    Composition,
    FlashProblem,
    FlashResult,
    NamedValue,
    PhaseEnvelope,
    PhaseEnvelopePoint,
    PhaseKind,
    PropertyBundle,
    PropertyPackageDescriptor,
    PropertyPackageMetadata,
    PropertyPackageSpec,
    PropertyValue,
    Provenance,
    StreamCondition,
)
from ol_simulator_interop_services.domain.protocols import SimulatorAdapter
from ol_simulator_interop_services.domain.registries import (
    ComponentRegistry,
    ParameterSourceRegistry,
    PropertyPackageRegistry,
)

from dwsim_mcp_server.adapter.alias_seeder import (
    DWSIM_BACKEND_ID,
    DWSIM_PARAMETER_SOURCE_NAME,
    DWSIM_PARAMETER_SOURCE_REVISION,
    SEEDED_DWSIM_COMPONENTS_BY_BACKEND_NAME,
    SEEDED_DWSIM_PROPERTY_PACKAGES_BY_DISPLAY_NAME,
    SEEDED_DWSIM_PROPERTY_PACKAGES_BY_MODEL_ID,
    seed_dwsim_component_aliases,
    seed_dwsim_parameter_source_aliases,
    seed_dwsim_property_packages,
)
from dwsim_mcp_server.adapter.translators import (
    DwsimFlashResultDict,
    DwsimPhaseEnvelopePointDict,
    composition_to_dwsim,
    dwsim_properties_to_property_bundle,
    dwsim_result_to_flash_result,
    flash_type_to_dwsim,
)
from dwsim_mcp_server.ipc.clr_loader import load_dwsim_worker
from dwsim_mcp_server.ipc.exceptions import map_dotnet_exception
from dwsim_mcp_server.ipc.session_client import _create_logger, _parse_guid

T = TypeVar("T")


class DwsimConditionPayload(TypedDict, total=False):
    """DWSIM flash input payload."""

    temperature_kelvin: float
    pressure_pascal: float
    enthalpy_j_per_mol: float
    entropy_j_per_mol_k: float
    vapor_fraction: float


class DwsimComponentRecord(TypedDict):
    """Minimal DWSIM component descriptor."""

    name: str
    category: str
    aliases: list[str]


class DwsimPropertyPackageRecord(TypedDict, total=False):
    """Minimal DWSIM property package descriptor."""

    name: str
    description: str


class DwsimPropertyPackageDetail(TypedDict, total=False):
    """Detailed DWSIM property package payload."""

    name: str
    description: str
    package_type: str
    parameters: dict[str, str | int | float | bool]
    options: dict[str, str | int | float | bool]


@runtime_checkable
class SessionLifecycleClient(Protocol):
    """Required session lifecycle surface."""

    def create_session(self, flowsheet_name: str | None = None) -> str | Awaitable[str]:
        """Create a DWSIM session."""

    def close_session(self, session_id: str) -> bool | Awaitable[bool]:
        """Close a DWSIM session."""


@runtime_checkable
class HasSessionManager(Protocol):
    """Session-manager surface used by pythonnet fallbacks."""

    @property
    def session_manager(self) -> SessionManagerLike:
        """Return the underlying .NET SessionManager."""


class SessionManagerLike(Protocol):
    """Subset of SessionManager used by the adapter."""

    def GetSession(self, guid: object) -> object:
        """Resolve a session context from the session manager."""


@runtime_checkable
class SupportsAdapterSessionPreparation(Protocol):
    """Optional mock seam for session preparation."""

    def adapter_add_compound(
        self,
        session_id: str,
        compound_name: str,
    ) -> None | Awaitable[None]:
        """Add a compound to an adapter-owned session."""

    def adapter_set_property_package(
        self,
        session_id: str,
        package_name: str,
    ) -> None | Awaitable[None]:
        """Apply a property package to an adapter-owned session."""


@runtime_checkable
class SupportsAdapterFlash(Protocol):
    """Optional mock seam for flash calculations."""

    def adapter_flash(
        self,
        session_id: str,
        *,
        compound_names: list[str],
        mole_fractions: list[float],
        calculation_type: str,
        condition: DwsimConditionPayload,
    ) -> DwsimFlashResultDict | Awaitable[DwsimFlashResultDict]:
        """Run a DWSIM flash calculation."""


@runtime_checkable
class SupportsAdapterPropertyPackages(Protocol):
    """Optional mock seam for property-package catalog calls."""

    def adapter_list_property_packages(
        self,
    ) -> list[DwsimPropertyPackageRecord] | Awaitable[list[DwsimPropertyPackageRecord]]:
        """List DWSIM property packages."""

    def adapter_introspect_property_package(
        self,
        package_name: str,
    ) -> DwsimPropertyPackageDetail | Awaitable[DwsimPropertyPackageDetail]:
        """Inspect a DWSIM property package."""


@runtime_checkable
class SupportsAdapterComponents(Protocol):
    """Optional mock seam for component catalog calls."""

    def adapter_list_components(
        self,
    ) -> list[DwsimComponentRecord] | Awaitable[list[DwsimComponentRecord]]:
        """List DWSIM compounds."""


@runtime_checkable
class SupportsAdapterPhaseEnvelope(Protocol):
    """Optional mock seam for phase-envelope calculations."""

    def adapter_phase_envelope(
        self,
        session_id: str,
        *,
        compound_names: list[str],
        mole_fractions: list[float],
        package_name: str,
    ) -> list[DwsimPhaseEnvelopePointDict] | Awaitable[list[DwsimPhaseEnvelopePointDict]]:
        """Calculate a DWSIM phase envelope."""


class DwsimAdapter(SimulatorAdapter):
    """Canonical adapter that wraps the DWSIM SessionClient and worker adapters."""

    backend_id = DWSIM_BACKEND_ID
    adapter_id = DWSIM_BACKEND_ID

    def __init__(
        self,
        *,
        session_client: SessionLifecycleClient,
        component_registry: ComponentRegistry,
        parameter_source_registry: ParameterSourceRegistry,
        property_package_registry: PropertyPackageRegistry,
    ) -> None:
        self._session_client = session_client
        self._component_registry = component_registry
        self._parameter_source_registry = parameter_source_registry
        self._property_package_registry = property_package_registry

        seed_dwsim_component_aliases(self._component_registry)
        seed_dwsim_parameter_source_aliases(self._parameter_source_registry)
        seed_dwsim_property_packages(
            self._property_package_registry,
            self._parameter_source_registry,
        )

    async def flash(self, problem: FlashProblem) -> FlashResult:
        """Run a canonical flash calculation through DWSIM."""

        started_at = perf_counter()
        raw_result = await self._execute_flash_problem(problem)
        provenance = self._build_provenance(
            property_package_spec=problem.property_package_spec,
            stream_condition=problem.stream_condition,
            started_at=started_at,
        )
        return dwsim_result_to_flash_result(
            raw_result,
            provenance=provenance,
            fallback_composition=problem.composition,
        )

    async def get_properties(
        self,
        composition: Composition,
        condition: StreamCondition,
        property_package_spec: PropertyPackageSpec,
        property_ids: tuple[str, ...],
    ) -> PropertyBundle:
        """Calculate a canonical property bundle from a DWSIM flash/property query."""

        raw_result = await self._execute_flash_problem(
            FlashProblem(
                composition=composition,
                stream_condition=condition,
                property_package_spec=property_package_spec,
            )
        )
        requested_properties = self._select_requested_properties(raw_result, property_ids)
        return PropertyBundle(properties=tuple(requested_properties))

    async def phase_envelope(
        self,
        composition: Composition,
        property_package_spec: PropertyPackageSpec,
    ) -> PhaseEnvelope:
        """Build a canonical phase envelope through an exposed DWSIM seam."""

        package_name = self._resolve_package_name(property_package_spec)
        compound_names, mole_fractions = composition_to_dwsim(composition)
        session_id = await _await_result(
            self._session_client.create_session(flowsheet_name="Canonical DWSIM phase envelope")
        )
        try:
            await self._prepare_session(session_id, compound_names, package_name)
            raw_points = await self._phase_envelope_points(
                session_id=session_id,
                compound_names=compound_names,
                mole_fractions=mole_fractions,
                package_name=package_name,
            )
        finally:
            await _await_result(self._session_client.close_session(session_id))

        return PhaseEnvelope(
            property_package_spec=property_package_spec,
            points=tuple(self._phase_envelope_point(point) for point in raw_points),
        )

    async def list_property_packages(self) -> tuple[PropertyPackageDescriptor, ...]:
        """List property packages that DWSIM exposes."""

        runtime_packages = await self._list_runtime_property_packages()
        descriptors: list[PropertyPackageDescriptor] = []
        for runtime_package in runtime_packages:
            metadata = self._metadata_for_runtime_property_package(runtime_package["name"])
            descriptors.append(metadata.descriptor)
        return tuple(descriptors)

    async def list_components(self) -> tuple[CanonicalComponent, ...]:
        """List canonical components that map to DWSIM compounds."""

        runtime_components = await self._list_runtime_components()
        components: list[CanonicalComponent] = []
        for runtime_component in runtime_components:
            seeded_component = SEEDED_DWSIM_COMPONENTS_BY_BACKEND_NAME.get(runtime_component["name"])
            if seeded_component is None:
                continue
            components.append(self._component_registry.get_by_cas_number(seeded_component.cas_number))
        return tuple(components)

    async def introspect_property_package(
        self,
        spec: PropertyPackageSpec,
    ) -> PropertyPackageMetadata:
        """Inspect DWSIM property-package details in canonical form."""

        package_name = self._resolve_package_name(spec)
        detail = await self._introspect_runtime_property_package(package_name)
        metadata = self._metadata_for_runtime_property_package(package_name)
        descriptor = metadata.descriptor
        return PropertyPackageMetadata(
            descriptor=descriptor,
            parameters=_named_values(detail.get("parameters", {})),
            options=_named_values(detail.get("options", {})),
        )

    async def _execute_flash_problem(self, problem: FlashProblem) -> DwsimFlashResultDict:
        package_name = self._resolve_package_name(problem.property_package_spec)
        compound_names, mole_fractions = composition_to_dwsim(problem.composition)
        condition = _stream_condition_payload(problem.stream_condition)
        calculation_type = flash_type_to_dwsim(problem.stream_condition.flash_calculation_type)

        session_id = await _await_result(
            self._session_client.create_session(flowsheet_name="Canonical DWSIM flash")
        )
        try:
            await self._prepare_session(session_id, compound_names, package_name)
            return await self._flash(
                session_id=session_id,
                compound_names=compound_names,
                mole_fractions=mole_fractions,
                calculation_type=calculation_type,
                condition=condition,
            )
        finally:
            await _await_result(self._session_client.close_session(session_id))

    async def _prepare_session(
        self,
        session_id: str,
        compound_names: Sequence[str],
        package_name: str,
    ) -> None:
        if isinstance(self._session_client, SupportsAdapterSessionPreparation):
            for compound_name in compound_names:
                await _await_result(self._session_client.adapter_add_compound(session_id, compound_name))
            await _await_result(
                self._session_client.adapter_set_property_package(session_id, package_name)
            )
            return

        if not isinstance(self._session_client, HasSessionManager):
            raise AdapterCapabilityError(
                "session_client must expose session_manager or adapter preparation hooks"
            )

        context = self._get_session_context(session_id)
        logger = _create_logger()
        worker = load_dwsim_worker()
        compound_adapter = worker.Adapters.CompoundAdapter(logger, context)
        property_package_adapter = worker.Adapters.PropertyPackageAdapter(logger, context)

        for compound_name in compound_names:
            result = compound_adapter.AddCompound(compound_name)
            _require_success(result, operation=f"add compound {compound_name}")

        package_result = property_package_adapter.SetPropertyPackage(package_name)
        _require_success(package_result, operation=f"set property package {package_name}")

    async def _flash(
        self,
        *,
        session_id: str,
        compound_names: list[str],
        mole_fractions: list[float],
        calculation_type: str,
        condition: DwsimConditionPayload,
    ) -> DwsimFlashResultDict:
        if isinstance(self._session_client, SupportsAdapterFlash):
            return await _await_result(
                self._session_client.adapter_flash(
                    session_id,
                    compound_names=compound_names,
                    mole_fractions=mole_fractions,
                    calculation_type=calculation_type,
                    condition=condition,
                )
            )

        if not isinstance(self._session_client, HasSessionManager):
            raise AdapterCapabilityError(
                "session_client must expose session_manager or adapter_flash()"
            )

        context = self._get_session_context(session_id)
        logger = _create_logger()
        worker = load_dwsim_worker()
        thermodynamics_adapter = worker.Adapters.ThermodynamicsAdapter(logger, context)

        if calculation_type == "TP":
            dto = thermodynamics_adapter.FlashTP(
                compound_names,
                mole_fractions,
                condition["temperature_kelvin"],
                condition["pressure_pascal"],
            )
            return _flash_result_from_dto(dto)
        if calculation_type == "PH":
            dto = thermodynamics_adapter.FlashPH(
                compound_names,
                mole_fractions,
                condition["pressure_pascal"],
                condition["enthalpy_j_per_mol"],
            )
            return _flash_result_from_dto(dto)
        if calculation_type == "PS":
            dto = thermodynamics_adapter.FlashPS(
                compound_names,
                mole_fractions,
                condition["pressure_pascal"],
                condition["entropy_j_per_mol_k"],
            )
            return _flash_result_from_dto(dto)
        raise AdapterCapabilityError(
            f"DWSIM worker does not expose direct flash support for calculation type {calculation_type}"
        )

    async def _phase_envelope_points(
        self,
        *,
        session_id: str,
        compound_names: list[str],
        mole_fractions: list[float],
        package_name: str,
    ) -> list[DwsimPhaseEnvelopePointDict]:
        if isinstance(self._session_client, SupportsAdapterPhaseEnvelope):
            return await _await_result(
                self._session_client.adapter_phase_envelope(
                    session_id,
                    compound_names=compound_names,
                    mole_fractions=mole_fractions,
                    package_name=package_name,
                )
            )
        raise AdapterCapabilityError(
            "the current DWSIM bridge does not expose a phase-envelope API; "
            "provide a session client implementing adapter_phase_envelope()"
        )

    async def _list_runtime_property_packages(self) -> list[DwsimPropertyPackageRecord]:
        if isinstance(self._session_client, SupportsAdapterPropertyPackages):
            return await _await_result(self._session_client.adapter_list_property_packages())

        if not isinstance(self._session_client, HasSessionManager):
            raise AdapterCapabilityError(
                "session_client must expose session_manager or adapter_list_property_packages()"
            )

        session_id = await _await_result(
            self._session_client.create_session(flowsheet_name="Canonical DWSIM package catalog")
        )
        try:
            context = self._get_session_context(session_id)
            logger = _create_logger()
            worker = load_dwsim_worker()
            property_package_adapter = worker.Adapters.PropertyPackageAdapter(logger, context)
            result = property_package_adapter.GetAvailablePackages()
            _require_success(result, operation="list property packages")
            package_names = _validated_types(result)
            return [{"name": package_name} for package_name in package_names]
        finally:
            await _await_result(self._session_client.close_session(session_id))

    async def _list_runtime_components(self) -> list[DwsimComponentRecord]:
        if isinstance(self._session_client, SupportsAdapterComponents):
            return await _await_result(self._session_client.adapter_list_components())

        if not isinstance(self._session_client, HasSessionManager):
            raise AdapterCapabilityError(
                "session_client must expose session_manager or adapter_list_components()"
            )

        session_id = await _await_result(
            self._session_client.create_session(flowsheet_name="Canonical DWSIM component catalog")
        )
        try:
            context = self._get_session_context(session_id)
            logger = _create_logger()
            worker = load_dwsim_worker()
            compound_adapter = worker.Adapters.CompoundAdapter(logger, context)
            result = compound_adapter.ListCompounds("", "", 10000, 0)
            return _component_records(result)
        finally:
            await _await_result(self._session_client.close_session(session_id))

    async def _introspect_runtime_property_package(
        self,
        package_name: str,
    ) -> DwsimPropertyPackageDetail:
        if isinstance(self._session_client, SupportsAdapterPropertyPackages):
            return await _await_result(
                self._session_client.adapter_introspect_property_package(package_name)
            )

        if not isinstance(self._session_client, HasSessionManager):
            raise AdapterCapabilityError(
                "session_client must expose session_manager or adapter_introspect_property_package()"
            )

        session_id = await _await_result(
            self._session_client.create_session(flowsheet_name="Canonical DWSIM package detail")
        )
        try:
            await self._prepare_session(session_id, (), package_name)
            context = self._get_session_context(session_id)
            worker = load_dwsim_worker()
            converter = worker.Converters.CapeOpenConverter()
            property_package = context.GetPropertyPackage()
            dto = converter.ToPropertyPackageDto(property_package)
            parameters = {
                parameter_name: _normalise_scalar(parameter_value)
                for parameter_name, parameter_value in _iter_mapping(getattr(dto, "Parameters", {}))
            }
            return {
                "name": str(getattr(dto, "Name", package_name)),
                "package_type": str(getattr(dto, "PackageType", package_name)),
                "parameters": parameters,
                "options": {"package_type": str(getattr(dto, "PackageType", package_name))},
            }
        finally:
            await _await_result(self._session_client.close_session(session_id))

    def _resolve_package_name(self, spec: PropertyPackageSpec) -> str:
        try:
            return self._property_package_registry.get_by_spec_id(
                _require_spec_id(spec)
            ).descriptor.display_name
        except PropertyPackageNotFoundError:
            pass

        runtime_package = SEEDED_DWSIM_PROPERTY_PACKAGES_BY_MODEL_ID.get(spec.model_id)
        if runtime_package is not None:
            return runtime_package.display_name

        normalized_name = _slugify(spec.model_id)
        for seeded_package in SEEDED_DWSIM_PROPERTY_PACKAGES_BY_MODEL_ID.values():
            if _slugify(seeded_package.display_name) == normalized_name:
                return seeded_package.display_name

        raise PropertyPackageNotFoundError(
            f"property package spec {spec.model_id!r} is not registered for DWSIM"
        )

    def _metadata_for_runtime_property_package(self, package_name: str) -> PropertyPackageMetadata:
        registered_packages = self._property_package_registry.list_registered_property_packages()
        for descriptor in registered_packages:
            if descriptor.display_name == package_name:
                return self._property_package_registry.get_by_spec_id(_require_spec_id(descriptor.spec))

        seeded_package = SEEDED_DWSIM_PROPERTY_PACKAGES_BY_DISPLAY_NAME.get(package_name)
        if seeded_package is None:
            seeded_package = SEEDED_DWSIM_PROPERTY_PACKAGES_BY_MODEL_ID.get(_slugify(package_name))
        if seeded_package is None:
            raise PropertyPackageNotFoundError(
                f"property package {package_name!r} is not mapped in the DWSIM registry"
            )

        parameter_source = self._parameter_source_registry.get_by_backend_id(
            backend_id=DWSIM_BACKEND_ID,
            backend_source_name=DWSIM_PARAMETER_SOURCE_NAME,
        )
        spec = PropertyPackageSpec(
            model_id=seeded_package.model_id,
            parameter_source_id=_require_parameter_source_id(parameter_source.parameter_source_id),
            revision=DWSIM_PARAMETER_SOURCE_REVISION,
        )
        return PropertyPackageMetadata(
            descriptor=PropertyPackageDescriptor(
                spec=spec,
                display_name=seeded_package.display_name,
                description=seeded_package.description,
                supported_flash_calculations=seeded_package.supported_flash_calculations,
            )
        )

    def _build_provenance(
        self,
        *,
        property_package_spec: PropertyPackageSpec,
        stream_condition: StreamCondition,
        started_at: float,
    ) -> Provenance:
        return Provenance(
            backend_id=self.backend_id,
            property_package_spec=property_package_spec,
            solver_params=_solver_params(stream_condition),
            wall_time_seconds=perf_counter() - started_at,
        )

    def _get_session_context(self, session_id: str) -> object:
        session_manager = self._session_client.session_manager
        try:
            guid = _parse_guid(session_id)
            result = session_manager.GetSession(guid)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="session") from exc

        if not getattr(result, "Success", False) or getattr(result, "Data", None) is None:
            message = str(getattr(result, "Message", "Session not found."))
            raise map_dotnet_exception(RuntimeError(message), kind="session")
        return getattr(result, "Data")

    def _select_requested_properties(
        self,
        raw_result: DwsimFlashResultDict,
        property_ids: tuple[str, ...],
    ) -> list[PropertyValue]:
        candidates: dict[str, PropertyValue] = {}
        top_level_payload: dict[str, float] = {}

        if "temperature_k" in raw_result:
            top_level_payload["temperature_k"] = raw_result["temperature_k"]
        if "pressure_pa" in raw_result:
            top_level_payload["pressure_pa"] = raw_result["pressure_pa"]
        top_level_payload.update(raw_result.get("overall_properties", {}))

        for property_value in dwsim_properties_to_property_bundle(top_level_payload).properties:
            candidates[property_value.property_id] = property_value

        phases = sorted(
            raw_result.get("phases", []),
            key=lambda phase_payload: phase_payload.get("phase_fraction", 0.0),
            reverse=True,
        )
        for phase_payload in phases:
            phase_basis = phase_payload.get("phase_label", "").strip().lower() or "overall"
            phase_properties = dwsim_properties_to_property_bundle(
                phase_payload.get("properties", {}),
                basis=phase_basis,
            )
            for property_value in phase_properties.properties:
                candidates.setdefault(property_value.property_id, property_value)

        if not property_ids:
            return list(candidates.values())

        missing_property_ids = [
            property_id for property_id in property_ids if property_id not in candidates
        ]
        if missing_property_ids:
            raise AdapterCapabilityError(
                "DWSIM could not resolve requested properties: "
                + ", ".join(sorted(missing_property_ids))
            )
        return [candidates[property_id] for property_id in property_ids]

    def _phase_envelope_point(
        self,
        point: DwsimPhaseEnvelopePointDict,
    ) -> PhaseEnvelopePoint:
        return PhaseEnvelopePoint(
            temperature_kelvin=point["temperature_kelvin"],
            pressure_pascal=point["pressure_pascal"],
            phase_kinds=tuple(
                _phase_kind_from_label(phase_label)
                for phase_label in point["phase_labels"]
            ),
        )


async def _await_result(value: T | Awaitable[T]) -> T:
    if inspect.isawaitable(value):
        return await cast(Awaitable[T], value)
    return value


def _stream_condition_payload(condition: StreamCondition) -> DwsimConditionPayload:
    payload: DwsimConditionPayload = {}
    if condition.kind in {"tp", "tpq"}:
        payload["temperature_kelvin"] = condition.temperature_kelvin
        payload["pressure_pascal"] = condition.pressure_pascal
    if condition.kind == "ph":
        payload["pressure_pascal"] = condition.pressure_pascal
        payload["enthalpy_j_per_mol"] = condition.enthalpy_j_per_mol
    if condition.kind == "ps":
        payload["pressure_pascal"] = condition.pressure_pascal
        payload["entropy_j_per_mol_k"] = condition.entropy_j_per_mol_k
    if condition.kind == "pq":
        payload["pressure_pascal"] = condition.pressure_pascal
        payload["vapor_fraction"] = condition.vapor_fraction
    if condition.kind == "tpq":
        payload["vapor_fraction"] = condition.vapor_fraction
    return payload


def _solver_params(condition: StreamCondition) -> tuple[NamedValue, ...]:
    solver_params = [NamedValue(name="flash_calculation_type", value=condition.flash_calculation_type.value)]
    if condition.kind in {"tp", "tpq"}:
        solver_params.append(NamedValue(name="temperature_kelvin", value=condition.temperature_kelvin))
        solver_params.append(NamedValue(name="pressure_pascal", value=condition.pressure_pascal))
    if condition.kind == "ph":
        solver_params.append(NamedValue(name="pressure_pascal", value=condition.pressure_pascal))
        solver_params.append(NamedValue(name="enthalpy_j_per_mol", value=condition.enthalpy_j_per_mol))
    if condition.kind == "ps":
        solver_params.append(NamedValue(name="pressure_pascal", value=condition.pressure_pascal))
        solver_params.append(NamedValue(name="entropy_j_per_mol_k", value=condition.entropy_j_per_mol_k))
    if condition.kind == "pq":
        solver_params.append(NamedValue(name="pressure_pascal", value=condition.pressure_pascal))
        solver_params.append(NamedValue(name="vapor_fraction", value=condition.vapor_fraction))
    if condition.kind == "tpq":
        solver_params.append(NamedValue(name="vapor_fraction", value=condition.vapor_fraction))
    return tuple(solver_params)


def _flash_result_from_dto(dto: object) -> DwsimFlashResultDict:
    raw_result: DwsimFlashResultDict = {
        "calculation_type": str(getattr(dto, "CalculationType", "")),
        "temperature_k": float(getattr(dto, "TemperatureK", 0.0)),
        "pressure_pa": float(getattr(dto, "PressurePa", 0.0)),
        "converged": bool(getattr(dto, "Converged", False)),
        "phases": [],
    }
    raw_phases: list[object] = list(getattr(dto, "Phases", []) or [])
    phases = [
        {
            "phase_label": str(getattr(phase, "PhaseLabel", "")),
            "phase_fraction": float(getattr(phase, "PhaseFraction", 0.0)),
            "composition": [
                {
                    "compound": str(getattr(component, "Compound", "")),
                    "mole_fraction": float(getattr(component, "MoleFraction", 0.0)),
                }
                for component in list(getattr(phase, "Composition", []) or [])
            ],
            "properties": {
                key: float(value)
                for key, value in _iter_mapping(getattr(phase, "Properties", {}))
            },
        }
        for phase in raw_phases
    ]
    raw_result["phases"] = phases
    return raw_result


def _validated_types(result: object) -> list[str]:
    return [str(item) for item in list(getattr(result, "ValidatedTypes", []) or [])]


def _component_records(result: object) -> list[DwsimComponentRecord]:
    compounds = list(getattr(result, "Compounds", []) or [])
    component_records: list[DwsimComponentRecord] = []
    for compound in compounds:
        component_records.append(
            {
                "name": str(getattr(compound, "Name", "")),
                "category": str(getattr(compound, "Category", "")),
                "aliases": [str(alias) for alias in list(getattr(compound, "Aliases", []) or [])],
            }
        )
    return component_records


def _iter_mapping(mapping: object) -> list[tuple[str, object]]:
    if isinstance(mapping, dict):
        return [(str(key), value) for key, value in mapping.items()]

    keys = getattr(mapping, "Keys", None)
    if keys is not None:
        return [(str(key), mapping[key]) for key in list(keys)]

    items = getattr(mapping, "items", None)
    if callable(items):
        return [(str(key), value) for key, value in items()]

    return []


def _require_success(result: object, *, operation: str) -> None:
    if getattr(result, "Success", False):
        return
    error = getattr(result, "Error", None)
    message = str(getattr(result, "Message", f"DWSIM operation failed: {operation}"))
    if isinstance(error, BaseException):
        raise map_dotnet_exception(error, kind="interop") from error
    raise RuntimeError(message)


def _named_values(
    values: dict[str, str | int | float | bool],
) -> tuple[NamedValue, ...]:
    return tuple(
        NamedValue(name=name, value=value)
        for name, value in sorted(values.items())
    )


def _normalise_scalar(value: object) -> str | int | float | bool:
    if isinstance(value, (str, int, float, bool)):
        return value
    return str(value)


def _phase_kind_from_label(phase_label: str) -> PhaseKind:
    from dwsim_mcp_server.adapter.translators import dwsim_phase_label_to_kind

    return dwsim_phase_label_to_kind(phase_label)


def _slugify(value: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", value.strip().lower()).strip("-")


def _require_spec_id(spec: PropertyPackageSpec) -> str:
    spec_id = spec.spec_id
    if spec_id is None:
        raise PropertyPackageNotFoundError("property package spec id was not derived")
    return spec_id


def _require_parameter_source_id(parameter_source_id: str | None) -> str:
    if parameter_source_id is None:
        raise PropertyPackageNotFoundError("parameter source id was not derived")
    return parameter_source_id
