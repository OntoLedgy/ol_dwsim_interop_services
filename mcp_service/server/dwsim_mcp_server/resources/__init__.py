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

"""MCP resource providers for documentation, samples, and results."""

from dwsim_mcp_server.resources.base import (
    ResourceProvider,
    BaseResourceProvider,
    ResourceError,
    ResourceNotFoundError,
    ResourceInvalidStateError,
)
from dwsim_mcp_server.resources.docs import DocsProvider
from dwsim_mcp_server.resources.samples import SamplesProvider
from dwsim_mcp_server.resources.results import ResultsProvider
from dwsim_mcp_server.resources.ui_resource_provider import UiResourceProvider
from dwsim_mcp_server.resources.registry import register_resources

__all__ = [
    # Protocol and base
    "ResourceProvider",
    "BaseResourceProvider",
    # Errors
    "ResourceError",
    "ResourceNotFoundError",
    "ResourceInvalidStateError",
    # Providers
    "DocsProvider",
    "SamplesProvider",
    "ResultsProvider",
    "UiResourceProvider",
    # Registration
    "register_resources",
]
