"""MCP server bootstrap entry point."""

from __future__ import annotations

import asyncio

from mcp.server import Server
from mcp.server.stdio import stdio_server

from typing import Optional

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.ipc.flowsheet_client import FlowsheetClient
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.observability import configure_logging, get_logger
from dwsim_mcp_server.observability.logging import start_seq_sink, stop_seq_sink
from dwsim_mcp_server.observability.metrics_server import start_metrics_server, stop_metrics_server
from dwsim_mcp_server.observability.settings import ObservabilitySettings
from dwsim_mcp_server.observability.tracing import configure_tracing
from dwsim_mcp_server.service import FlowsheetService
from dwsim_mcp_server.service.diagnostics_service import DiagnosticsService
from dwsim_mcp_server.services import ThermodynamicsService
from dwsim_mcp_server.services.sensitivity_service import SensitivityService
from dwsim_mcp_server.tools.registry import register_tools
from dwsim_mcp_server.resources.registry import register_resources


class ServerDependencies:
    """Container for server-scoped dependencies."""

    def __init__(
        self,
        *,
        settings: ServerSettings,
        flowsheet_service: Optional[FlowsheetService] = None,
        thermodynamics_service: Optional[ThermodynamicsService] = None,
        sensitivity_service: Optional[SensitivityService] = None,
        diagnostics_service: Optional[DiagnosticsService] = None,
    ) -> None:
        self.settings = settings
        self.session_client = LimitedSessionClient(settings.resource_limits)
        self.flowsheet_client = FlowsheetClient(self.session_client)
        self.flowsheet_service = flowsheet_service or FlowsheetService(
            session_client=self.session_client,
            flowsheet_client=self.flowsheet_client,
        )
        self.thermodynamics_service = thermodynamics_service or ThermodynamicsService(
            session_client=self.session_client,
        )
        self.sensitivity_service = sensitivity_service or SensitivityService(
            session_client=self.session_client,
            flowsheet_client=self.flowsheet_client,
            allowed_export_roots=settings.case_storage_roots
            if hasattr(settings, "case_storage_roots")
            else [],
        )
        self.diagnostics_service = diagnostics_service or DiagnosticsService(
            session_client=self.session_client,
        )

    async def start(self) -> None:
        self.session_client.start_monitoring()

    async def close(self) -> None:
        await self.session_client.stop_monitoring()
        self.session_client.dispose()


def create_server(settings: ServerSettings, dependencies: ServerDependencies) -> Server:
    """Create and register MCP server instance."""
    server = Server("dwsim-mcp-server")
    register_tools(server, dependencies)
    register_resources(server, dependencies)
    return server


async def main() -> None:
    settings = ServerSettings()
    obs_settings = ObservabilitySettings.from_env()

    configure_logging(obs_settings.log_level)
    logger = get_logger(__name__)

    configure_tracing(
        exporter=obs_settings.tracing_exporter,
        endpoint=obs_settings.tracing_endpoint,
        sample_rate=obs_settings.tracing_sample_rate,
    )

    logger.info(
        "observability_configured",
        log_level=obs_settings.log_level,
        log_file=obs_settings.log_file,
        seq_enabled=bool(obs_settings.seq_url),
        tracing_enabled=obs_settings.tracing_enabled,
        tracing_exporter=obs_settings.tracing_exporter,
        tracing_sample_rate=obs_settings.tracing_sample_rate,
        metrics_enabled=obs_settings.metrics_enabled,
        metrics_port=obs_settings.metrics_port,
    )

    dependencies = ServerDependencies(settings=settings)
    server = create_server(settings, dependencies)
    logger.info("server_starting", log_level=obs_settings.log_level)

    await dependencies.start()
    await start_seq_sink()
    metrics_server = await start_metrics_server() if obs_settings.metrics_enabled else None

    try:
        async with stdio_server() as (read_stream, write_stream):
            await server.run(
                read_stream,
                write_stream,
                server.create_initialization_options(),
            )
    finally:
        if metrics_server:
            await stop_metrics_server(metrics_server)
        await stop_seq_sink()
        await dependencies.close()
        logger.info("server_shutdown")


def run() -> None:
    """Synchronous entry point for CLI usage."""
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    run()
