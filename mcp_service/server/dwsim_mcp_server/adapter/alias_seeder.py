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

"""Seed canonical registries with DWSIM-specific aliases and defaults."""

from __future__ import annotations

from dataclasses import dataclass
from importlib.resources import files as _resources_files
from pathlib import Path
import tomllib

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


def _candidate_property_packages_toml_paths() -> tuple[Path, ...]:
    # Installed wheels ship the TOML inside the package via the
    # `prebuilt/DwsimWorker` force-include in pyproject.toml, so it is the
    # install-location-independent location and is preferred. In a fresh dev
    # checkout the worker may not yet have been published, so the canonical
    # `shared/` copy is the dev-mode fallback.
    installed_path = (
        Path(str(_resources_files("dwsim_mcp_server")))
        / "_prebuilt"
        / "property_packages.toml"
    )
    development_path = (
        Path(__file__).resolve().parents[4] / "shared" / "property_packages.toml"
    )
    return (installed_path, development_path)


def _resolve_property_packages_toml_path() -> Path:
    for candidate_path in _candidate_property_packages_toml_paths():
        if candidate_path.is_file():
            return candidate_path
    return _candidate_property_packages_toml_paths()[0]


_PROPERTY_PACKAGES_TOML_PATH = _resolve_property_packages_toml_path()


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

def _load_seeded_property_packages() -> tuple[SeededPropertyPackage, ...]:
    inventory_document = _load_property_packages_inventory_document()
    seeded_property_packages: list[SeededPropertyPackage] = []
    seen_lookup_keys: set[str] = set()
    seen_model_ids: set[str] = set()
    seen_display_names: set[str] = set()

    for package_index, package_document in enumerate(inventory_document):
        model_id = _require_string_field(
            package_document=package_document,
            field_name="model_id",
            package_index=package_index,
        )
        display_name = _require_string_field(
            package_document=package_document,
            field_name="display_name",
            package_index=package_index,
        )
        description = _require_string_field(
            package_document=package_document,
            field_name="description",
            package_index=package_index,
        )
        aliases = _require_aliases(
            package_document=package_document,
            package_index=package_index,
        )
        supported_flash_calculations = _require_supported_flash_calculations(
            package_document=package_document,
            package_index=package_index,
        )

        _require_unique_lookup_key(
            seen_lookup_keys=seen_lookup_keys,
            lookup_key=display_name,
            field_name="display_name",
            package_index=package_index,
        )
        for alias in aliases:
            _require_unique_lookup_key(
                seen_lookup_keys=seen_lookup_keys,
                lookup_key=alias,
                field_name="aliases",
                package_index=package_index,
            )
        _require_unique_value(
            seen_values=seen_model_ids,
            value=model_id,
            field_name="model_id",
            package_index=package_index,
        )
        _require_unique_value(
            seen_values=seen_display_names,
            value=display_name,
            field_name="display_name",
            package_index=package_index,
        )

        seeded_property_packages.append(
            SeededPropertyPackage(
                model_id=model_id,
                display_name=display_name,
                description=description,
                supported_flash_calculations=supported_flash_calculations,
            )
        )

    return tuple(seeded_property_packages)


def _load_worker_supported_model_ids_and_display_names(
    *, seeded_property_packages: tuple[SeededPropertyPackage, ...]
) -> tuple[frozenset[str], frozenset[str]]:
    worker_supported_model_ids: set[str] = set()
    worker_supported_display_names: set[str] = set()
    for seeded_property_package in seeded_property_packages:
        worker_supported_model_ids.add(seeded_property_package.model_id)
        worker_supported_display_names.add(seeded_property_package.display_name)
    return (
        frozenset(worker_supported_model_ids),
        frozenset(worker_supported_display_names),
    )


def _load_property_packages_inventory_document() -> list[dict[str, object]]:
    try:
        with _PROPERTY_PACKAGES_TOML_PATH.open("rb") as property_packages_file:
            parsed_document = tomllib.load(property_packages_file)
    except FileNotFoundError as error:
        searched_paths = ", ".join(
            str(path) for path in _candidate_property_packages_toml_paths()
        )
        raise RuntimeError(
            f"DIS-37 property-package inventory missing — searched: {searched_paths}"
        ) from error
    except tomllib.TOMLDecodeError as error:
        raise RuntimeError(
            f"DIS-37 property-package inventory is malformed at "
            f"{_PROPERTY_PACKAGES_TOML_PATH}: {error}"
        ) from error

    packages = parsed_document.get("packages")
    if not isinstance(packages, list):
        raise RuntimeError(
            "DIS-37 property-package inventory is malformed at "
            f"{_PROPERTY_PACKAGES_TOML_PATH}: expected a top-level 'packages' list"
        )

    inventory_document: list[dict[str, object]] = []
    for package_index, package_document in enumerate(packages):
        if not isinstance(package_document, dict):
            raise RuntimeError(
                "DIS-37 property-package inventory is malformed at "
                f"{_PROPERTY_PACKAGES_TOML_PATH}: package #{package_index} must be a table"
            )
        inventory_document.append(package_document)
    return inventory_document


def _require_string_field(
    *,
    package_document: dict[str, object],
    field_name: str,
    package_index: int,
) -> str:
    value = package_document.get(field_name)
    if not isinstance(value, str) or not value.strip():
        raise RuntimeError(
            "DIS-37 property-package inventory is malformed at "
            f"{_PROPERTY_PACKAGES_TOML_PATH}: package #{package_index} field "
            f"'{field_name}' must be a non-empty string"
        )
    return value


def _require_aliases(
    *,
    package_document: dict[str, object],
    package_index: int,
) -> tuple[str, ...]:
    aliases = package_document.get("aliases")
    if not isinstance(aliases, list):
        raise RuntimeError(
            "DIS-37 property-package inventory is malformed at "
            f"{_PROPERTY_PACKAGES_TOML_PATH}: package #{package_index} field "
            "'aliases' must be a list"
        )

    normalized_aliases: list[str] = []
    for alias_index, alias in enumerate(aliases):
        if not isinstance(alias, str) or not alias.strip():
            raise RuntimeError(
                "DIS-37 property-package inventory is malformed at "
                f"{_PROPERTY_PACKAGES_TOML_PATH}: package #{package_index} alias "
                f"#{alias_index} must be a non-empty string"
            )
        normalized_aliases.append(alias)
    return tuple(normalized_aliases)


def _require_supported_flash_calculations(
    *,
    package_document: dict[str, object],
    package_index: int,
) -> tuple[FlashCalculationType, ...]:
    flash_calculation_names = package_document.get("supported_flash_calculations")
    if not isinstance(flash_calculation_names, list):
        raise RuntimeError(
            "DIS-37 property-package inventory is malformed at "
            f"{_PROPERTY_PACKAGES_TOML_PATH}: package #{package_index} field "
            "'supported_flash_calculations' must be a list"
        )

    supported_flash_calculations: list[FlashCalculationType] = []
    for flash_calculation_index, flash_calculation_name in enumerate(
        flash_calculation_names
    ):
        if not isinstance(flash_calculation_name, str):
            raise RuntimeError(
                "DIS-37 property-package inventory is malformed at "
                f"{_PROPERTY_PACKAGES_TOML_PATH}: package #{package_index} flash "
                f"calculation #{flash_calculation_index} must be a string"
            )
        try:
            supported_flash_calculations.append(
                FlashCalculationType[flash_calculation_name]
            )
        except KeyError as error:
            raise RuntimeError(
                "DIS-37 property-package inventory is malformed at "
                f"{_PROPERTY_PACKAGES_TOML_PATH}: package #{package_index} has "
                "unknown FlashCalculationType "
                f"'{flash_calculation_name}'"
            ) from error
    return tuple(supported_flash_calculations)


def _require_unique_lookup_key(
    *,
    seen_lookup_keys: set[str],
    lookup_key: str,
    field_name: str,
    package_index: int,
) -> None:
    normalized_lookup_key = lookup_key.casefold()
    if normalized_lookup_key in seen_lookup_keys:
        raise RuntimeError(
            "DIS-37 property-package inventory is malformed at "
            f"{_PROPERTY_PACKAGES_TOML_PATH}: duplicate lookup key "
            f"'{lookup_key}' in field '{field_name}' for package #{package_index}"
        )
    seen_lookup_keys.add(normalized_lookup_key)


def _require_unique_value(
    *,
    seen_values: set[str],
    value: str,
    field_name: str,
    package_index: int,
) -> None:
    if value in seen_values:
        raise RuntimeError(
            "DIS-37 property-package inventory is malformed at "
            f"{_PROPERTY_PACKAGES_TOML_PATH}: duplicate {field_name} "
            f"'{value}' for package #{package_index}"
        )
    seen_values.add(value)


SEEDED_DWSIM_PROPERTY_PACKAGES: tuple[
    SeededPropertyPackage, ...
] = _load_seeded_property_packages()
(
    DWSIM_WORKER_SUPPORTED_PROPERTY_PACKAGE_MODEL_IDS,
    DWSIM_WORKER_SUPPORTED_PROPERTY_PACKAGE_DISPLAY_NAMES,
) = _load_worker_supported_model_ids_and_display_names(
    seeded_property_packages=SEEDED_DWSIM_PROPERTY_PACKAGES
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
