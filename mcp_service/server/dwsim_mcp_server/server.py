"""MCP server bootstrap entry point."""

from __future__ import annotations

import asyncio

from mcp.server import Server
from mcp.server.stdio import stdio_server

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.observability import configure_logging, get_logger
from dwsim_mcp_server.tools.registry import register_tools


class ServerDependencies:
    """Container for server-scoped dependencies."""

    def __init__(self, *, settings: ServerSettings) -> None:
        self.settings = settings
        self.session_client = LimitedSessionClient(settings.resource_limits)

    async def start(self) -> None:
        self.session_client.start_monitoring()

    async def close(self) -> None:
        await self.session_client.stop_monitoring()
        self.session_client.dispose()


def create_server(settings: ServerSettings, dependencies: ServerDependencies) -> Server:
    """Create and register MCP server instance."""
    server = Server("dwsim-mcp-server")
    register_tools(server, dependencies)
    return server


async def main() -> None:
    settings = ServerSettings()
    configure_logging(settings.log_level)
    logger = get_logger(__name__)

    dependencies = ServerDependencies(settings=settings)
    server = create_server(settings, dependencies)
    logger.info("server_starting", log_level=settings.log_level)
    await dependencies.start()

    try:
        async with stdio_server() as (read_stream, write_stream):
            await server.run(
                read_stream,
                write_stream,
                server_name="dwsim-mcp-server",
                server_version="0.1.0",
            )
    finally:
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
