from unittest.mock import MagicMock

from dwsim_mcp_server.adapter.alias_seeder import (
    DWSIM_BACKEND_ID,
    DWSIM_PARAMETER_SOURCE_NAME,
)
from dwsim_mcp_server.adapter.factory import DwsimAdapterConfig, build_dwsim_adapter


class StubSessionClient:
    def __init__(self, default_flowsheet_name: str | None = None) -> None:
        self.default_flowsheet_name = default_flowsheet_name

    def create_session(self, flowsheet_name: str | None = None) -> str:
        return flowsheet_name or "session-1"

    def close_session(self, session_id: str) -> bool:
        return bool(session_id)

    @property
    def session_manager(self) -> object:
        return object()


def test_build_dwsim_adapter_wires_registries_and_seeds_aliases(monkeypatch):
    load_worker = MagicMock()

    monkeypatch.setattr("dwsim_mcp_server.adapter.factory.load_dwsim_worker", load_worker)
    monkeypatch.setattr("dwsim_mcp_server.adapter.factory.SessionClient", StubSessionClient)

    adapter = build_dwsim_adapter(DwsimAdapterConfig())

    water = adapter._component_registry.get_by_backend_id(
        backend_id=DWSIM_BACKEND_ID,
        backend_component_id="Water",
    )
    parameter_source = adapter._parameter_source_registry.get_by_backend_id(
        backend_id=DWSIM_BACKEND_ID,
        backend_source_name=DWSIM_PARAMETER_SOURCE_NAME,
    )
    packages = adapter._property_package_registry.list_registered_property_packages()
    display_names = {descriptor.display_name for descriptor in packages}

    assert adapter.backend_id == "dwsim"
    assert water.cas_number == "7732-18-5"
    assert parameter_source.display_name == DWSIM_PARAMETER_SOURCE_NAME
    assert "Peng-Robinson" in display_names
    load_worker.assert_called_once_with(None)


def test_build_dwsim_adapter_loads_configured_worker_path(monkeypatch):
    load_worker = MagicMock()

    monkeypatch.setattr("dwsim_mcp_server.adapter.factory.load_dwsim_worker", load_worker)
    monkeypatch.setattr("dwsim_mcp_server.adapter.factory.SessionClient", StubSessionClient)

    config = DwsimAdapterConfig(
        dwsim_worker_dll_path="C:\\DWSIM\\DwsimWorker.dll",
        default_flowsheet_name="Factory flowsheet",
    )

    adapter = build_dwsim_adapter(config)

    assert adapter._session_client.default_flowsheet_name == "Factory flowsheet"
    load_worker.assert_called_once_with("C:\\DWSIM\\DwsimWorker.dll")
