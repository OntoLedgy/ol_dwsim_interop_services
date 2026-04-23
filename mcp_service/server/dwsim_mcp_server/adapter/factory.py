"""Factory for fully wired DWSIM adapters."""

from __future__ import annotations

import json
import sys
from dataclasses import dataclass
from typing import Any

from ol_simulator_interop_services.domain.registries import (
    InMemoryComponentRegistry,
    InMemoryParameterSourceRegistry,
    InMemoryPropertyPackageRegistry,
)

from dwsim_mcp_server.adapter.alias_seeder import (
    DWSIM_WORKER_SUPPORTED_PROPERTY_PACKAGE_DISPLAY_NAMES,
    SEEDED_DWSIM_PROPERTY_PACKAGES,
    SEEDED_DWSIM_PROPERTY_PACKAGES_BY_DISPLAY_NAME,
)
from dwsim_mcp_server.adapter.dwsim_adapter import DwsimAdapter
from dwsim_mcp_server.ipc.clr_loader import load_dwsim_worker
from dwsim_mcp_server.ipc.session_client import SessionClient, _create_logger, _parse_guid


@dataclass(frozen=True)
class DwsimAdapterConfig:
    """Configuration for building a DWSIM adapter."""

    dwsim_worker_dll_path: str | None = None
    default_flowsheet_name: str | None = None


def build_dwsim_adapter(config: DwsimAdapterConfig) -> DwsimAdapter:
    load_dwsim_worker(config.dwsim_worker_dll_path)
    adapter = DwsimAdapter(
        session_client=_build_session_client(config),
        component_registry=InMemoryComponentRegistry(),
        parameter_source_registry=InMemoryParameterSourceRegistry(),
        property_package_registry=InMemoryPropertyPackageRegistry(),
    )
    _log_property_package_alignment(adapter)
    return adapter


def _build_session_client(config: DwsimAdapterConfig) -> SessionClient:
    return SessionClient(default_flowsheet_name=config.default_flowsheet_name)


def _log_property_package_alignment(adapter: DwsimAdapter) -> None:
    # Emit directly to stderr as one JSON line. Project-wide configure_logging
    # routes structlog through a PrintLoggerFactory that writes to stdout,
    # which corrupts the MCP stdio transport (Claude Desktop parses every
    # stdout line as JSON-RPC). Stderr is always safe regardless of
    # downstream logging configuration.
    try:
        seeded_model_ids = {
            seeded_package.model_id for seeded_package in SEEDED_DWSIM_PROPERTY_PACKAGES
        }
        worker_supported_model_ids = _model_ids_from_package_names(
            DWSIM_WORKER_SUPPORTED_PROPERTY_PACKAGE_DISPLAY_NAMES
        )
        runtime_loaded_model_ids = _load_runtime_property_package_model_ids(adapter._session_client)
    except Exception as exc:
        _emit_alignment_event_to_stderr(
            event="dwsim_property_package_alignment_unavailable",
            reason=str(exc),
        )
        return

    _emit_alignment_event_to_stderr(
        event="dwsim_property_package_alignment",
        seeded_model_ids=sorted(seeded_model_ids),
        worker_supported_model_ids=sorted(worker_supported_model_ids),
        runtime_loaded_model_ids=sorted(runtime_loaded_model_ids),
        seeded_not_supported=sorted(seeded_model_ids - worker_supported_model_ids),
        supported_not_seeded=sorted(worker_supported_model_ids - seeded_model_ids),
        runtime_not_seeded=sorted(runtime_loaded_model_ids - seeded_model_ids),
        seeded_not_runtime=sorted(seeded_model_ids - runtime_loaded_model_ids),
        worker_not_runtime=sorted(worker_supported_model_ids - runtime_loaded_model_ids),
        runtime_not_worker=sorted(runtime_loaded_model_ids - worker_supported_model_ids),
    )


def _emit_alignment_event_to_stderr(**payload: Any) -> None:
    print(json.dumps(payload), file=sys.stderr, flush=True)


def _load_runtime_property_package_model_ids(session_client: SessionClient) -> set[str]:
    session_id = session_client.create_session(flowsheet_name="Canonical DWSIM package catalog")
    try:
        session_manager = session_client.session_manager
        session_result = session_manager.GetSession(_parse_guid(session_id))
        if not getattr(session_result, "Success", False) or getattr(session_result, "Data", None) is None:
            message = str(getattr(session_result, "Message", "Failed to resolve DWSIM session context."))
            raise RuntimeError(message)

        worker = load_dwsim_worker()
        property_package_adapter = worker.Adapters.PropertyPackageAdapter(
            _create_logger(),
            session_result.Data,
        )
        result = property_package_adapter.GetAvailablePackages()
        _require_success(result, operation="list property packages")
        package_names = {str(item) for item in list(getattr(result, "ValidatedTypes", []) or [])}
        return _model_ids_from_package_names(package_names)
    finally:
        session_client.close_session(session_id)


def _model_ids_from_package_names(package_names: set[str] | frozenset[str]) -> set[str]:
    return {_model_id_from_package_name(package_name) for package_name in package_names}


def _model_id_from_package_name(package_name: str) -> str:
    seeded_package = SEEDED_DWSIM_PROPERTY_PACKAGES_BY_DISPLAY_NAME.get(package_name)
    if seeded_package is not None:
        return seeded_package.model_id
    return package_name.strip().lower().replace(" ", "-")


def _require_success(result: Any, *, operation: str) -> None:
    if getattr(result, "Success", False):
        return
    message = str(getattr(result, "Message", f"DWSIM operation failed: {operation}"))
    raise RuntimeError(message)


