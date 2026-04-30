# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

from ol_simulator_interop_services.domain.models import (
    CanonicalComponent,
    Composition,
    CompositionEntry,
    FlashCalculationType,
    PropertyPackageSpec,
    Provenance,
)

from dwsim_mcp_server.adapter.translators import (
    composition_to_dwsim,
    dwsim_phase_to_canonical,
    dwsim_result_to_flash_result,
    flash_type_to_dwsim,
)


def test_composition_to_dwsim_translates_seeded_components():
    composition = Composition(
        entries=(
            CompositionEntry(
                component=CanonicalComponent(
                    cas_number="74-82-8",
                    molecular_formula="CH4",
                    common_name="methane",
                ).reference(),
                mole_fraction=0.7,
            ),
            CompositionEntry(
                component=CanonicalComponent(
                    cas_number="74-84-0",
                    molecular_formula="C2H6",
                    common_name="ethane",
                ).reference(),
                mole_fraction=0.3,
            ),
        )
    )

    compound_names, mole_fractions = composition_to_dwsim(composition)

    assert compound_names == ["Methane", "Ethane"]
    assert mole_fractions == [0.7, 0.3]


def test_dwsim_phase_to_canonical_maps_phase_kind_and_properties():
    phase = dwsim_phase_to_canonical(
        {
            "phase_label": "Vapor",
            "phase_fraction": 0.6,
            "composition": [
                {"compound": "Methane", "mole_fraction": 0.8},
                {"compound": "Ethane", "mole_fraction": 0.2},
            ],
            "properties": {
                "density_kg_per_m3": 12.5,
                "molar_enthalpy_kj_per_kmol": 1550.0,
            },
        }
    )

    assert phase.phase_kind.value == "vapor"
    assert phase.fraction == 0.6
    assert phase.composition.entries[0].component.cas_number == "74-82-8"
    assert phase.properties.get("density") is not None
    assert phase.properties.get("molar_enthalpy") is not None


def test_dwsim_result_to_flash_result_adds_overall_properties_and_provenance():
    fallback_composition = Composition(
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
    provenance = Provenance(
        backend_id="dwsim",
        property_package_spec=PropertyPackageSpec(
            model_id="peng-robinson",
            parameter_source_id="builtin-source",
            revision="builtin",
        ),
        wall_time_seconds=0.125,
    )

    result = dwsim_result_to_flash_result(
        {
            "calculation_type": "TP",
            "temperature_k": 300.0,
            "pressure_pa": 101325.0,
            "converged": True,
            "overall_properties": {"total_molar_flow_mol_per_s": 1.5},
            "phases": [
                {
                    "phase_label": "Vapor",
                    "phase_fraction": 1.0,
                    "composition": [
                        {"compound": "Methane", "mole_fraction": 1.0},
                    ],
                    "properties": {
                        "density_kg_per_m3": 15.0,
                        "molar_entropy_kj_per_kmol_k": 12.0,
                    },
                }
            ],
        },
        provenance=provenance,
        fallback_composition=fallback_composition,
    )

    assert result.converged is True
    assert result.provenance == provenance
    assert result.overall_properties.get("temperature") is not None
    assert result.overall_properties.get("pressure") is not None
    assert result.overall_properties.get("total_molar_flow") is not None


def test_flash_type_to_dwsim_keeps_canonical_identifiers():
    assert flash_type_to_dwsim(FlashCalculationType.TEMPERATURE_PRESSURE) == "TP"
    assert flash_type_to_dwsim(FlashCalculationType.PRESSURE_ENTHALPY) == "PH"
    assert flash_type_to_dwsim(FlashCalculationType.PRESSURE_ENTROPY) == "PS"
