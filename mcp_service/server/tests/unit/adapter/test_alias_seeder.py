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

from ol_simulator_interop_services.domain.registries import (
    InMemoryComponentRegistry,
    InMemoryParameterSourceRegistry,
    InMemoryPropertyPackageRegistry,
)

from dwsim_mcp_server.adapter.alias_seeder import (
    DWSIM_BACKEND_ID,
    DWSIM_PARAMETER_SOURCE_NAME,
    _candidate_property_packages_toml_paths,
    seed_dwsim_component_aliases,
    seed_dwsim_parameter_source_aliases,
    seed_dwsim_property_packages,
)


def test_seed_dwsim_component_aliases_registers_common_compounds():
    component_registry = InMemoryComponentRegistry()

    seeded_components = seed_dwsim_component_aliases(component_registry)

    water = component_registry.get_by_backend_id(
        backend_id=DWSIM_BACKEND_ID,
        backend_component_id="Water",
    )
    methane = component_registry.get_by_backend_id(
        backend_id=DWSIM_BACKEND_ID,
        backend_component_id="Methane",
    )

    assert len(seeded_components) >= 15
    assert water.cas_number == "7732-18-5"
    assert methane.cas_number == "74-82-8"


def test_seed_dwsim_parameter_source_aliases_registers_builtin_source():
    parameter_source_registry = InMemoryParameterSourceRegistry()

    parameter_source = seed_dwsim_parameter_source_aliases(parameter_source_registry)

    resolved = parameter_source_registry.get_by_backend_id(
        backend_id=DWSIM_BACKEND_ID,
        backend_source_name=DWSIM_PARAMETER_SOURCE_NAME,
    )

    assert resolved == parameter_source
    assert resolved.display_name == DWSIM_PARAMETER_SOURCE_NAME


def test_seed_dwsim_property_packages_registers_expected_catalog():
    parameter_source_registry = InMemoryParameterSourceRegistry()
    property_package_registry = InMemoryPropertyPackageRegistry()

    registered_packages = seed_dwsim_property_packages(
        property_package_registry,
        parameter_source_registry,
    )
    descriptors = property_package_registry.list_registered_property_packages()

    display_names = {descriptor.display_name for descriptor in descriptors}
    model_ids = {descriptor.spec.model_id for descriptor in descriptors}

    # DIS-35: lock the canonical property-package seed to the worker-supported inventory.
    assert len(registered_packages) == 4
    assert model_ids == {"peng-robinson", "srk", "nrtl", "unifac"}
    assert "Peng-Robinson" in display_names
    assert "UNIFAC" in display_names


def test_property_packages_toml_candidates_cover_installed_and_development_layouts():
    # DIS-37: the inventory must resolve in both installed (uv tool install)
    # and development checkouts. The installed candidate ships inside the
    # package via the pyproject.toml `force-include` of `prebuilt/DwsimWorker`
    # → `dwsim_mcp_server/_prebuilt/`. The development candidate sits at the
    # repo-root `shared/` so a fresh clone works before the worker is built.
    candidate_paths = _candidate_property_packages_toml_paths()

    assert len(candidate_paths) == 2
    installed_path, development_path = candidate_paths
    assert installed_path.parts[-2:] == ("_prebuilt", "property_packages.toml")
    assert "dwsim_mcp_server" in installed_path.parts
    assert development_path.parts[-2:] == ("shared", "property_packages.toml")
    # At least one candidate must exist in any environment that imported the
    # module successfully — otherwise SEEDED_DWSIM_PROPERTY_PACKAGES would have
    # raised at import time.
    assert any(path.is_file() for path in candidate_paths)
