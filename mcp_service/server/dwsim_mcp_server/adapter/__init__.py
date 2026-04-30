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

"""Canonical DWSIM simulator adapter surface."""

from dwsim_mcp_server.adapter.alias_seeder import (
    seed_dwsim_component_aliases,
    seed_dwsim_parameter_source_aliases,
    seed_dwsim_property_packages,
)
from dwsim_mcp_server.adapter.dwsim_adapter import DwsimAdapter
from dwsim_mcp_server.adapter.factory import DwsimAdapterConfig, build_dwsim_adapter

__all__ = [
    "DwsimAdapter",
    "DwsimAdapterConfig",
    "build_dwsim_adapter",
    "seed_dwsim_component_aliases",
    "seed_dwsim_parameter_source_aliases",
    "seed_dwsim_property_packages",
]
