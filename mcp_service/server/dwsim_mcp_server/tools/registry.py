"""Tool registration for MCP server."""

from __future__ import annotations

from typing import Any

from mcp.server import Server

from dwsim_mcp_server.observability import get_logger


def register_tools(server: Server, dependencies: Any) -> None:
    """Register tool handlers with the MCP server."""
    logger = get_logger(__name__)
    _ = dependencies
    logger.info("tools_registered", tool_count=0)
