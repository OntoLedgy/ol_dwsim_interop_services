import pytest
from types import SimpleNamespace

from ol_simulator_interop_services.domain.registries import (
    InMemoryComponentRegistry,
    InMemoryParameterSourceRegistry,
    InMemoryPropertyPackageRegistry,
)
from ol_simulator_interop_services.domain.models import (
    CanonicalComponent,
    Composition,
    CompositionEntry,
    FlashProblem,
    PressureEnthalpyCondition,
    Provenance,
    PropertyPackageSpec,
    TemperaturePressureCondition,
)

from dwsim_mcp_server.adapter.alias_seeder import (
    DWSIM_BACKEND_ID,
    DWSIM_PARAMETER_SOURCE_NAME,
    DWSIM_PARAMETER_SOURCE_REVISION,
)
from dwsim_mcp_server.adapter.dwsim_adapter import DwsimAdapter, _flash_result_from_dto
from dwsim_mcp_server.adapter.translators import dwsim_result_to_flash_result


class FakeSessionClient:
    def __init__(self) -> None:
        self.created_sessions: list[str] = []
        self.closed_sessions: list[str] = []
        self.added_compounds: list[tuple[str, str]] = []
        self.property_packages: list[tuple[str, str]] = []
        self.flash_calls: list[dict[str, object]] = []
        self.phase_envelope_calls: list[dict[str, object]] = []

    async def create_session(self, flowsheet_name: str | None = None) -> str:
        session_id = f"session-{len(self.created_sessions) + 1}"
        self.created_sessions.append(flowsheet_name or "")
        return session_id

    async def close_session(self, session_id: str) -> bool:
        self.closed_sessions.append(session_id)
        return True

    async def adapter_add_compound(self, session_id: str, compound_name: str) -> None:
        self.added_compounds.append((session_id, compound_name))

    async def adapter_set_property_package(self, session_id: str, package_name: str) -> None:
        self.property_packages.append((session_id, package_name))

    async def adapter_flash(
        self,
        session_id: str,
        *,
        compound_names: list[str],
        mole_fractions: list[float],
        calculation_type: str,
        condition: dict[str, float],
    ) -> dict[str, object]:
        self.flash_calls.append(
            {
                "session_id": session_id,
                "compound_names": compound_names,
                "mole_fractions": mole_fractions,
                "calculation_type": calculation_type,
                "condition": condition,
            }
        )
        return {
            "calculation_type": calculation_type,
            "temperature_k": condition.get("temperature_kelvin", 300.0),
            "pressure_pa": condition["pressure_pascal"],
            "converged": True,
            "overall_properties": {"total_molar_flow_mol_per_s": 1.0},
            "phases": [
                {
                    "phase_label": "Vapor",
                    "phase_fraction": 1.0,
                    "composition": [
                        {"compound": compound_names[0], "mole_fraction": mole_fractions[0]},
                    ],
                    "properties": {
                        "density_kg_per_m3": 10.0,
                        "molar_enthalpy_kj_per_kmol": 2500.0,
                    },
                }
            ],
        }

    async def adapter_list_property_packages(self) -> list[dict[str, str]]:
        return [{"name": "Peng-Robinson"}]

    async def adapter_introspect_property_package(self, package_name: str) -> dict[str, object]:
        return {
            "name": package_name,
            "package_type": "PengRobinsonPropertyPackage",
            "parameters": {"binary_interaction_model": "builtin"},
            "options": {"mixing_rule": "classic"},
        }

    async def adapter_list_components(self) -> list[dict[str, object]]:
        return [
            {"name": "Methane", "category": "Hydrocarbon", "aliases": ["CH4"]},
            {"name": "Unknown", "category": "Other", "aliases": []},
        ]

    async def adapter_phase_envelope(
        self,
        session_id: str,
        *,
        compound_names: list[str],
        mole_fractions: list[float],
        package_name: str,
    ) -> list[dict[str, object]]:
        self.phase_envelope_calls.append(
            {
                "session_id": session_id,
                "compound_names": compound_names,
                "mole_fractions": mole_fractions,
                "package_name": package_name,
            }
        )
        return [
            {
                "temperature_kelvin": 250.0,
                "pressure_pascal": 101325.0,
                "phase_labels": ["Liquid1", "Vapor"],
            },
            {
                "temperature_kelvin": 300.0,
                "pressure_pascal": 150000.0,
                "phase_labels": ["Vapor"],
            },
        ]


def _composition() -> Composition:
    return Composition(
        entries=(
            CompositionEntry(
                component=CanonicalComponent(
                    cas_number="74-82-8",
                    molecular_formula="CH4",
                    common_name="methane",
                ).reference(),
                mole_fraction=1.0,
            ),
        )
    )


def _adapter() -> tuple[DwsimAdapter, FakeSessionClient, InMemoryParameterSourceRegistry]:
    session_client = FakeSessionClient()
    component_registry = InMemoryComponentRegistry()
    parameter_source_registry = InMemoryParameterSourceRegistry()
    property_package_registry = InMemoryPropertyPackageRegistry()
    adapter = DwsimAdapter(
        session_client=session_client,
        component_registry=component_registry,
        parameter_source_registry=parameter_source_registry,
        property_package_registry=property_package_registry,
    )
    return adapter, session_client, parameter_source_registry


def _package_spec(parameter_source_registry: InMemoryParameterSourceRegistry) -> PropertyPackageSpec:
    parameter_source = parameter_source_registry.get_by_backend_id(
        backend_id=DWSIM_BACKEND_ID,
        backend_source_name=DWSIM_PARAMETER_SOURCE_NAME,
    )
    assert parameter_source.parameter_source_id is not None
    return PropertyPackageSpec(
        model_id="peng-robinson",
        parameter_source_id=parameter_source.parameter_source_id,
        revision=DWSIM_PARAMETER_SOURCE_REVISION,
    )


@pytest.mark.asyncio
async def test_flash_translates_problem_and_adds_provenance():
    adapter, session_client, parameter_source_registry = _adapter()
    flash_problem = FlashProblem(
        composition=_composition(),
        stream_condition=TemperaturePressureCondition(
            temperature_kelvin=310.0,
            pressure_pascal=101325.0,
        ),
        property_package_spec=_package_spec(parameter_source_registry),
    )

    result = await adapter.flash(flash_problem)

    assert result.converged is True
    assert result.provenance.backend_id == "dwsim"
    assert session_client.added_compounds == [("session-1", "Methane")]
    assert session_client.property_packages == [("session-1", "Peng-Robinson")]
    assert session_client.flash_calls[0]["calculation_type"] == "TP"
    assert session_client.closed_sessions == ["session-1"]


@pytest.mark.asyncio
async def test_get_properties_selects_top_level_and_phase_properties():
    adapter, session_client, parameter_source_registry = _adapter()

    property_bundle = await adapter.get_properties(
        composition=_composition(),
        condition=PressureEnthalpyCondition(
            pressure_pascal=202650.0,
            enthalpy_j_per_mol=1500.0,
        ),
        property_package_spec=_package_spec(parameter_source_registry),
        property_ids=("pressure", "total_molar_flow", "density"),
    )

    assert property_bundle.get("pressure") is not None
    assert property_bundle.get("total_molar_flow") is not None
    density = property_bundle.get("density")
    assert density is not None
    assert density.basis == "vapor"
    assert session_client.flash_calls[0]["calculation_type"] == "PH"


@pytest.mark.asyncio
async def test_list_components_filters_to_seeded_canonical_components():
    adapter, _, _ = _adapter()

    components = await adapter.list_components()

    assert len(components) == 1
    assert components[0].cas_number == "74-82-8"


@pytest.mark.asyncio
async def test_list_and_introspect_property_packages_use_registry_metadata():
    adapter, _, parameter_source_registry = _adapter()
    package_spec = _package_spec(parameter_source_registry)

    descriptors = await adapter.list_property_packages()
    metadata = await adapter.introspect_property_package(package_spec)

    assert [descriptor.display_name for descriptor in descriptors] == ["Peng-Robinson"]
    assert metadata.descriptor.display_name == "Peng-Robinson"
    assert metadata.parameters[0].name == "binary_interaction_model"
    assert metadata.options[0].name == "mixing_rule"


@pytest.mark.asyncio
async def test_phase_envelope_translates_mock_points():
    adapter, session_client, parameter_source_registry = _adapter()

    envelope = await adapter.phase_envelope(
        composition=_composition(),
        property_package_spec=_package_spec(parameter_source_registry),
    )

    assert len(envelope.points) == 2
    assert envelope.points[0].phase_kinds[0].value == "liquid"
    assert envelope.points[0].phase_kinds[1].value == "vapor"
    assert session_client.phase_envelope_calls[0]["package_name"] == "Peng-Robinson"


def test_flash_result_from_dto_projects_density_from_named_scalar_attribute():
    dto = SimpleNamespace(
        CalculationType="TP",
        TemperatureK=310.0,
        PressurePa=101325.0,
        Converged=True,
        Phases=[
            SimpleNamespace(
                PhaseLabel="Vapor",
                PhaseFraction=1.0,
                Composition=[
                    SimpleNamespace(
                        Compound="Methane",
                        MoleFraction=1.0,
                    )
                ],
                Properties={},
                DensityKgPerM3=4.2,
                ViscosityPaS=1.1e-5,
            )
        ],
    )

    raw_result = _flash_result_from_dto(dto)
    result = dwsim_result_to_flash_result(
        raw_result,
        provenance=Provenance(
            backend_id="dwsim",
            property_package_spec=PropertyPackageSpec(
                model_id="peng-robinson",
                parameter_source_id="builtin-source",
                revision="builtin",
            ),
            wall_time_seconds=0.1,
        ),
        fallback_composition=_composition(),
    )

    phase = result.phases[0]

    assert raw_result["phases"][0]["properties"]["density_kg_per_m3"] > 0.0
    assert phase.density_kg_per_m3 > 0.0
    assert phase.properties.get("density") is not None


def test_flash_result_from_dto_aliases_capeopen_property_keys_to_canonical():
    # DIS-34: DWSIM's CapeOpenConverter emits camelCase CAPE-OPEN keys
    # (density, enthalpy, viscosity, molecularWeight, entropy) in
    # PhaseDto.Properties. The translator must alias these onto canonical
    # keys (density_kg_per_m3 etc.) and derive molar_enthalpy/molar_entropy
    # from the specific forms via molecular weight.
    dto = SimpleNamespace(
        CalculationType="TP",
        TemperatureK=298.15,
        PressurePa=101325.0,
        Converged=True,
        Phases=[
            SimpleNamespace(
                PhaseLabel="Liquid1",
                PhaseFraction=1.0,
                Composition=[
                    SimpleNamespace(Compound="Water", MoleFraction=1.0),
                ],
                Properties={
                    "density": 996.33,
                    "enthalpy": -2537.04,
                    "entropy": -6.834,
                    "viscosity": 8.976e-4,
                    "molecularWeight": 18.01528,
                },
            )
        ],
    )

    raw_result = _flash_result_from_dto(dto)
    phase_properties = raw_result["phases"][0]["properties"]

    assert phase_properties["density_kg_per_m3"] == 996.33
    assert phase_properties["viscosity_pa_s"] == 8.976e-4
    assert phase_properties["molecular_weight_kg_per_kmol"] == 18.01528
    assert phase_properties["enthalpy_kj_per_kg"] == -2537.04
    assert phase_properties["entropy_kj_per_kg_k"] == -6.834
    # Derived from specific × molecular_weight; 1 kJ/kmol == 1 J/mol.
    assert phase_properties["molar_enthalpy_kj_per_kmol"] == pytest.approx(
        -2537.04 * 18.01528
    )
    assert phase_properties["molar_entropy_kj_per_kmol_k"] == pytest.approx(
        -6.834 * 18.01528
    )
    # CAPE-OPEN keys are preserved alongside the canonical ones.
    assert phase_properties["density"] == 996.33
