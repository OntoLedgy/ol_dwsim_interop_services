from ol_simulator_interop_services.domain.registries import (
    InMemoryComponentRegistry,
    InMemoryParameterSourceRegistry,
    InMemoryPropertyPackageRegistry,
)

from dwsim_mcp_server.adapter.alias_seeder import (
    DWSIM_BACKEND_ID,
    DWSIM_PARAMETER_SOURCE_NAME,
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

    assert len(registered_packages) == 6
    assert "Peng-Robinson" in display_names
    assert "Lee-Kesler-Plocker" in display_names
    assert "Steam Tables (IAPWS-IF97)" in display_names
