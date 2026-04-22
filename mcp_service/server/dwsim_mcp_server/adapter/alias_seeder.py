"""Seed canonical registries with DWSIM-specific aliases and defaults."""

from __future__ import annotations

from dataclasses import dataclass

from ol_simulator_interop_services.domain.models import (
    CanonicalComponent,
    CanonicalParameterSource,
    FlashCalculationType,
    PropertyPackageDescriptor,
    PropertyPackageMetadata,
    PropertyPackageSpec,
)
from ol_simulator_interop_services.domain.registries import (
    ComponentRegistry,
    ParameterSourceRegistry,
    PropertyPackageRegistry,
)

DWSIM_BACKEND_ID = "dwsim"
DWSIM_PARAMETER_SOURCE_NAME = "DWSIM Built-in Databank"
DWSIM_PARAMETER_SOURCE_LOCATOR = "dwsim://built-in-databank"
DWSIM_PARAMETER_SOURCE_REVISION = "builtin"


@dataclass(frozen=True)
class SeededComponent:
    """Seed data for a canonical component known to DWSIM."""

    backend_name: str
    cas_number: str
    molecular_formula: str
    common_name: str


@dataclass(frozen=True)
class SeededPropertyPackage:
    """Seed data for a canonical property package exposed by DWSIM."""

    model_id: str
    display_name: str
    description: str
    supported_flash_calculations: tuple[FlashCalculationType, ...]


SEEDED_DWSIM_COMPONENTS: tuple[SeededComponent, ...] = (
    SeededComponent("Water", "7732-18-5", "H2O", "water"),
    SeededComponent("Methane", "74-82-8", "CH4", "methane"),
    SeededComponent("Ethane", "74-84-0", "C2H6", "ethane"),
    SeededComponent("Propane", "74-98-6", "C3H8", "propane"),
    SeededComponent("n-Butane", "106-97-8", "C4H10", "n-butane"),
    SeededComponent("n-Pentane", "109-66-0", "C5H12", "n-pentane"),
    SeededComponent("Benzene", "71-43-2", "C6H6", "benzene"),
    SeededComponent("Toluene", "108-88-3", "C7H8", "toluene"),
    SeededComponent("Ethanol", "64-17-5", "C2H6O", "ethanol"),
    SeededComponent("Methanol", "67-56-1", "CH4O", "methanol"),
    SeededComponent("Carbon Dioxide", "124-38-9", "CO2", "carbon dioxide"),
    SeededComponent("Hydrogen sulfide", "7783-06-4", "H2S", "hydrogen sulfide"),
    SeededComponent("Nitrogen", "7727-37-9", "N2", "nitrogen"),
    SeededComponent("Oxygen", "7782-44-7", "O2", "oxygen"),
    SeededComponent("Hydrogen", "1333-74-0", "H2", "hydrogen"),
)

SEEDED_DWSIM_COMPONENTS_BY_BACKEND_NAME = {
    component.backend_name: component for component in SEEDED_DWSIM_COMPONENTS
}
SEEDED_DWSIM_COMPONENTS_BY_CAS_NUMBER = {
    component.cas_number: component for component in SEEDED_DWSIM_COMPONENTS
}

_COMMON_FLASH_TYPES = (
    FlashCalculationType.TEMPERATURE_PRESSURE,
    FlashCalculationType.PRESSURE_ENTHALPY,
    FlashCalculationType.PRESSURE_ENTROPY,
    FlashCalculationType.TEMPERATURE_PRESSURE_VAPOR_FRACTION,
    FlashCalculationType.PRESSURE_VAPOR_FRACTION,
)

SEEDED_DWSIM_PROPERTY_PACKAGES: tuple[SeededPropertyPackage, ...] = (
    SeededPropertyPackage(
        model_id="peng-robinson",
        display_name="Peng-Robinson",
        description="Cubic equation of state for hydrocarbon and gas-processing systems.",
        supported_flash_calculations=_COMMON_FLASH_TYPES,
    ),
    SeededPropertyPackage(
        model_id="srk",
        display_name="SRK",
        description="Soave-Redlich-Kwong cubic equation of state.",
        supported_flash_calculations=_COMMON_FLASH_TYPES,
    ),
    SeededPropertyPackage(
        model_id="nrtl",
        display_name="NRTL",
        description="Activity-coefficient model for highly non-ideal liquid systems.",
        supported_flash_calculations=_COMMON_FLASH_TYPES,
    ),
    SeededPropertyPackage(
        model_id="uniquac",
        display_name="UNIQUAC",
        description="Activity-coefficient model for strongly non-ideal liquid mixtures.",
        supported_flash_calculations=_COMMON_FLASH_TYPES,
    ),
    SeededPropertyPackage(
        model_id="lee-kesler-plocker",
        display_name="Lee-Kesler-Plocker",
        description="Corresponding-states package for light hydrocarbons and gases.",
        supported_flash_calculations=_COMMON_FLASH_TYPES,
    ),
    SeededPropertyPackage(
        model_id="steam-tables",
        display_name="Steam Tables (IAPWS-IF97)",
        description="Water/steam property package based on the IAPWS-IF97 formulation.",
        supported_flash_calculations=(
            FlashCalculationType.TEMPERATURE_PRESSURE,
            FlashCalculationType.PRESSURE_ENTHALPY,
            FlashCalculationType.PRESSURE_ENTROPY,
        ),
    ),
)

SEEDED_DWSIM_PROPERTY_PACKAGES_BY_DISPLAY_NAME = {
    package.display_name: package for package in SEEDED_DWSIM_PROPERTY_PACKAGES
}
SEEDED_DWSIM_PROPERTY_PACKAGES_BY_MODEL_ID = {
    package.model_id: package for package in SEEDED_DWSIM_PROPERTY_PACKAGES
}


def seed_dwsim_component_aliases(
    component_registry: ComponentRegistry,
) -> tuple[CanonicalComponent, ...]:
    """Populate the component registry with common DWSIM compounds."""

    registered_components: list[CanonicalComponent] = []
    for seeded_component in SEEDED_DWSIM_COMPONENTS:
        component = component_registry.register_component(
            CanonicalComponent(
                cas_number=seeded_component.cas_number,
                molecular_formula=seeded_component.molecular_formula,
                common_name=seeded_component.common_name,
            )
        )
        component_registry.register_alias(
            canonical_component_id=_require_canonical_component_id(component),
            backend_id=DWSIM_BACKEND_ID,
            backend_component_id=seeded_component.backend_name,
        )
        registered_components.append(component)
    return tuple(registered_components)


def seed_dwsim_parameter_source_aliases(
    parameter_source_registry: ParameterSourceRegistry,
) -> CanonicalParameterSource:
    """Populate the parameter-source registry with the built-in DWSIM source."""

    parameter_source = parameter_source_registry.register_parameter_source(
        CanonicalParameterSource(
            source_locator=DWSIM_PARAMETER_SOURCE_LOCATOR,
            display_name=DWSIM_PARAMETER_SOURCE_NAME,
            citation="DWSIM built-in compound and property-package databank",
            source_revision=DWSIM_PARAMETER_SOURCE_REVISION,
        )
    )
    parameter_source_registry.register_alias(
        parameter_source_id=_require_parameter_source_id(parameter_source),
        backend_id=DWSIM_BACKEND_ID,
        backend_source_name=DWSIM_PARAMETER_SOURCE_NAME,
    )
    return parameter_source


def seed_dwsim_property_packages(
    property_package_registry: PropertyPackageRegistry,
    parameter_source_registry: ParameterSourceRegistry,
) -> tuple[PropertyPackageMetadata, ...]:
    """Populate the property-package registry with the DWSIM package catalog."""

    parameter_source = seed_dwsim_parameter_source_aliases(parameter_source_registry)
    parameter_source_id = _require_parameter_source_id(parameter_source)

    registered_packages: list[PropertyPackageMetadata] = []
    for seeded_package in SEEDED_DWSIM_PROPERTY_PACKAGES:
        metadata = property_package_registry.register_property_package(
            PropertyPackageMetadata(
                descriptor=PropertyPackageDescriptor(
                    spec=PropertyPackageSpec(
                        model_id=seeded_package.model_id,
                        parameter_source_id=parameter_source_id,
                        revision=DWSIM_PARAMETER_SOURCE_REVISION,
                    ),
                    display_name=seeded_package.display_name,
                    description=seeded_package.description,
                    supported_flash_calculations=seeded_package.supported_flash_calculations,
                )
            )
        )
        registered_packages.append(metadata)
    return tuple(registered_packages)


def _require_canonical_component_id(component: CanonicalComponent) -> str:
    canonical_component_id = component.canonical_component_id
    if canonical_component_id is None:
        raise ValueError("canonical component id was not derived")
    return canonical_component_id


def _require_parameter_source_id(parameter_source: CanonicalParameterSource) -> str:
    parameter_source_id = parameter_source.parameter_source_id
    if parameter_source_id is None:
        raise ValueError("parameter source id was not derived")
    return parameter_source_id
