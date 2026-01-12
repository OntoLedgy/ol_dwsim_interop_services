"""Tool registration for MCP server."""

from __future__ import annotations

from typing import Any, Dict, Set

from mcp.server import Server

from dwsim_mcp_server.observability import get_logger
from dwsim_mcp_server.tools.flowsheet import build_flowsheet_tools, handle_flowsheet_tool
from dwsim_mcp_server.tools.session import build_session_tools, handle_session_tool
from dwsim_mcp_server.tools.simulation import build_simulation_tools, handle_simulation_tool


def register_tools(server: Server, dependencies: Any) -> None:
    """Register tool handlers with the MCP server."""
    logger = get_logger(__name__)

    session_tools = build_session_tools()
    flowsheet_tools = build_flowsheet_tools()
    simulation_tools = build_simulation_tools()

    session_tool_names: Set[str] = {tool.name for tool in session_tools}
    flowsheet_tool_names: Set[str] = {tool.name for tool in flowsheet_tools}
    simulation_tool_names: Set[str] = {tool.name for tool in simulation_tools}
    tool_by_name: Dict[str, Any] = {
        tool.name: tool for tool in (*session_tools, *flowsheet_tools, *simulation_tools)
    }

    @server.list_tools()
    async def list_tools():
        return list(tool_by_name.values())

    @server.call_tool()
    async def call_tool(tool_name: str, arguments: dict):
        if tool_name in flowsheet_tool_names:
            return await handle_flowsheet_tool(tool_name, arguments, dependencies)
        if tool_name in session_tool_names:
            return await handle_session_tool(tool_name, arguments, dependencies)
        if tool_name in simulation_tool_names:
            return await handle_simulation_tool(tool_name, arguments, dependencies)
        return await handle_session_tool(tool_name, arguments, dependencies)

    logger.info("tools_registered", tool_count=len(tool_by_name))
