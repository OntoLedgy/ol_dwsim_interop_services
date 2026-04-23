"""Factory for fully wired DWSIM adapters."""

from __future__ import annotations

from dataclasses import dataclass

from ol_simulator_interop_services.domain.registries import (
    InMemoryComponentRegistry,
    InMemoryParameterSourceRegistry,
    InMemoryPropertyPackageRegistry,
)

from dwsim_mcp_server.adapter.dwsim_adapter import DwsimAdapter
from dwsim_mcp_server.ipc.clr_loader import load_dwsim_worker
from dwsim_mcp_server.ipc.session_client import SessionClient


@dataclass(frozen=True)
class DwsimAdapterConfig:
    """Configuration for building a DWSIM adapter."""

    dwsim_worker_dll_path: str | None = None
    default_flowsheet_name: str | None = None


def build_dwsim_adapter(config: DwsimAdapterConfig) -> DwsimAdapter:
    load_dwsim_worker(config.dwsim_worker_dll_path)
    return DwsimAdapter(
        session_client=_build_session_client(config),
        component_registry=InMemoryComponentRegistry(),
        parameter_source_registry=InMemoryParameterSourceRegistry(),
        property_package_registry=InMemoryPropertyPackageRegistry(),
    )


def _build_session_client(config: DwsimAdapterConfig) -> SessionClient:
    return SessionClient(default_flowsheet_name=config.default_flowsheet_name)
