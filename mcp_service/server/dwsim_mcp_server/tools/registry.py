"""Tool registration for MCP server."""

from __future__ import annotations

from typing import Any

from mcp.server import Server

from dwsim_mcp_server.observability import get_logger
from dwsim_mcp_server.tools.session import build_session_tools, handle_session_tool


def register_tools(server: Server, dependencies: Any) -> None:
    """Register tool handlers with the MCP server."""
    logger = get_logger(__name__)

    session_tools = build_session_tools()

    @server.list_tools()
    async def list_tools():
        return session_tools

    @server.call_tool()
    async def call_tool(tool_name: str, arguments: dict):
        return await handle_session_tool(tool_name, arguments, dependencies)

    logger.info("tools_registered", tool_count=len(session_tools))
