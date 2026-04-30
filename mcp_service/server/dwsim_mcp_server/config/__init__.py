# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Configuration management for MCP server."""

from dwsim_mcp_server.config.resource_limit_settings import ResourceLimitSettings
from dwsim_mcp_server.config.server_settings import ServerSettings

__all__ = ["ResourceLimitSettings", "ServerSettings"]
