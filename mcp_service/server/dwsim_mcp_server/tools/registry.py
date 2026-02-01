"""Tool registration for FastMCP server."""

from __future__ import annotations

from dwsim_mcp_server.observability import get_logger
from dwsim_mcp_server.tools.analysis import register_analysis_tools
from dwsim_mcp_server.tools.compound import register_compound_tools
from dwsim_mcp_server.tools.diagnostics import register_diagnostics_tools
from dwsim_mcp_server.tools.export import register_export_tools
from dwsim_mcp_server.tools.flowsheet import register_flowsheet_tools
from dwsim_mcp_server.tools.sensitivity import register_sensitivity_tools
from dwsim_mcp_server.tools.session import register_session_tools
from dwsim_mcp_server.tools.simulation import register_simulation_tools


def register_all_tools(mcp) -> None:
    """Register all tools with FastMCP."""
    register_session_tools(mcp)
    register_flowsheet_tools(mcp)
    register_simulation_tools(mcp)
    register_analysis_tools(mcp)
    register_sensitivity_tools(mcp)
    register_export_tools(mcp)
    register_compound_tools(mcp)
    register_diagnostics_tools(mcp)
    get_logger(__name__).info("tools_registered", tool_count=33)
