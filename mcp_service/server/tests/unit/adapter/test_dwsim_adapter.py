"""Tests for DwsimAdapter using mocked session client."""

import asyncio

from ol_simulator_interop_services.domain.models import (
    Composition,
    CompositionEntry,
    FlashProblem,
    PropertyPackageSpec,
    TemperaturePressureCondition,
)
from ol_simulator_interop_services.domain.registries import (
    InMemoryComponentRegistry,
    InMemoryParameterSourceRegistry,
    InMemoryPropertyPackageRegistry,
)

from dwsim_mcp_server.adapter.dwsim_adapter import DwsimAdapter


class FakeSessionClient:
    """Mock session client implementing the adapter seams."""

    def __init__(self, flash_result: dict):
        self._flash_result = flash_result
        self.created_sessions: list[str | None] = []
        self.closed_sessions: list[str] = []
        self.flash_calls: list[dict] = []

    def create_session(self, flowsheet_name: str | None = None) -> str:
        self.created_sessions.append(flowsheet_name)
        return "session-fake-123"

    def close_session(self, session_id: str) -> bool:
        self.closed_sessions.append(session_id)
        return True

    def adapter_add_compound(self, session_id: str, compound_name: str) -> None:
        pass

    def adapter_set_property_package(self, session_id: str, package_name: str) -> None:
        pass

    def adapter_flash(
        self,
        session_id: str,
        *,
        compound_names: list[str],
        mole_fractions: list[float],
        calculation_type: str,
        condition: dict,
    ) -> dict:
        self.flash_calls.append(
            {
                "session_id": session_id,
                "compound_names": compound_names,
                "mole_fractions": mole_fractions,
                "calculation_type": calculation_type,
                "condition": condition,
            }
        )
        return self._flash_result


def test_dwsim_adapter_flash_with_mocked_session_client():
    component_registry = InMemoryComponentRegistry()
    parameter_source_registry = InMemoryParameterSourceRegistry()
    property_package_registry = InMemoryPropertyPackageRegistry()

    adapter = DwsimAdapter(
        session_client=FakeSessionClient(
            flash_result={
                "converged": True,
                "temperature_k": 300.0,
                "pressure_pa": 101325.0,
                "phases": [
                    {
                        "phase_label": "Liquid",
                        "phase_fraction": 0.25,
                        "composition": [
                            {"compound": "Water", "mole_fraction": 0.9},
                            {"compound": "Methane", "mole_fraction": 0.1},
                        ],
                        "properties": {"density_kg_per_m3": 820.0},
                    },
                    {
                        "phase_label": "Vapor",
                        "phase_fraction": 0.75,
                        "composition": [
                            {"compound": "Water", "mole_fraction": 0.1},
                            {"compound": "Methane", "mole_fraction": 0.9},
                        ],
                        "properties": {"density_kg_per_m3": 2.1},
                    },
                ],
            }
        ),
        component_registry=component_registry,
        parameter_source_registry=parameter_source_registry,
        property_package_registry=property_package_registry,
    )

    # Get the seeded water and methane from the auto-populated registries
    water = component_registry.get_by_backend_id(backend_id="dwsim", backend_component_id="Water")
    methane = component_registry.get_by_backend_id(backend_id="dwsim", backend_component_id="Methane")
    pp_descriptors = property_package_registry.list_registered_property_packages()

    problem = FlashProblem(
        composition=Composition(
            entries=(
                CompositionEntry(component=water.reference(), mole_fraction=0.4),
                CompositionEntry(component=methane.reference(), mole_fraction=0.6),
            )
        ),
        stream_condition=TemperaturePressureCondition(
            temperature_kelvin=300.0,
            pressure_pascal=101325.0,
        ),
        property_package_spec=pp_descriptors[0].spec,
    )

    result = asyncio.run(adapter.flash(problem))

    assert adapter.adapter_id == "dwsim"
    assert result.converged is True
    assert len(result.phases) == 2
    assert result.provenance.backend_id == "dwsim"
    assert result.provenance.wall_time_seconds > 0.0
