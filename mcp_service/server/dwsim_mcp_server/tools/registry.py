# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# This file is part of the OntoLedgy Thermodynamics Architecture and is
# dual-licensed:
#
#   1. Open source under the GNU Affero General Public License v3.0 or
#      later (AGPL-3.0-or-later). See the LICENSE file in the repository
#      root for the full licence text and NOTICE for attribution.
#   2. Commercial under a separate proprietary licence offered by
#      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Tool registration for FastMCP server."""

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
    get_logger(__name__).info("tools_registered", tool_count=35)
