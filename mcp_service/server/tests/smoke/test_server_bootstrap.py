import asyncio

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.observability import configure_logging, get_logger
from dwsim_mcp_server.server import ServerDependencies, create_server


class FakeSessionClient:
    def __init__(self, settings):
        self.settings = settings
        self.started = False
        self.stopped = False
        self.disposed = False

    def start_monitoring(self):
        self.started = True

    async def stop_monitoring(self):
        self.stopped = True

    def dispose(self):
        self.disposed = True


def test_server_settings_env_override(monkeypatch):
    monkeypatch.setenv("DWSIM_LOG_LEVEL", "DEBUG")
    monkeypatch.setenv("DWSIM_ENABLE_PYTHONNET", "false")
    settings = ServerSettings()
    assert settings.log_level == "DEBUG"
    assert settings.enable_pythonnet is False


def test_configure_logging_returns_logger():
    configure_logging("INFO", json_format=False)
    logger = get_logger(__name__)
    assert logger is not None


def test_create_server_with_fake_dependencies(monkeypatch):
    monkeypatch.setattr("dwsim_mcp_server.server.LimitedSessionClient", FakeSessionClient)
    settings = ServerSettings()
    dependencies = ServerDependencies(settings=settings)

    async def _run():
        await dependencies.start()
        server = create_server(settings, dependencies)
        await dependencies.close()
        return server

    server = asyncio.run(_run())
    assert server is not None
