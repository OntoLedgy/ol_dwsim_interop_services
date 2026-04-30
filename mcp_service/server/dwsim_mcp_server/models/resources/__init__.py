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

"""Resource data models for MCP resource providers."""

from dwsim_mcp_server.models.resources.resource_metadata import ResourceMetadata
from dwsim_mcp_server.models.resources.sample_case_info import SampleCaseInfo
from dwsim_mcp_server.models.resources.documentation_topic import DocumentationTopic
from dwsim_mcp_server.models.resources.session_result_resource import SessionResultResource

__all__ = [
    "ResourceMetadata",
    "SampleCaseInfo",
    "DocumentationTopic",
    "SessionResultResource",
]
