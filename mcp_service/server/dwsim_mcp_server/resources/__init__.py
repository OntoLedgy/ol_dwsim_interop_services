# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
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
