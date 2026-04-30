# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Error models with structured error codes."""

from dwsim_mcp_server.models.errors.resource_limit_error import ResourceLimitError
from dwsim_mcp_server.models.errors.session_error import SessionError
from dwsim_mcp_server.models.errors.simulation_error import SimulationError

__all__ = ["ResourceLimitError", "SessionError", "SimulationError"]
