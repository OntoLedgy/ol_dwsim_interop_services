# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
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
