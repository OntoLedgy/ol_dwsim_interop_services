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

"""Release metadata for the running ol-dwsim-mcp-server package."""

from __future__ import annotations

import os
from importlib import import_module, metadata

PACKAGE_NAME = "ol-dwsim-mcp-server"
SOURCE_URL = "https://github.com/OntoLedgy/ol_dwsim_interop_services"
LICENSE = "AGPL-3.0-or-later"
COMMIT_SHA_ENV_VAR = "DWSIM_MCP_COMMIT_SHA"
UNKNOWN_VERSION = "0.0.0+unknown"
UNKNOWN_COMMIT_SHA = "unknown"


def get_version() -> str:
    try:
        return metadata.version(PACKAGE_NAME)
    except metadata.PackageNotFoundError:
        return UNKNOWN_VERSION


def get_commit_sha() -> str:
    try:
        commit_module = import_module("dwsim_mcp_server._commit_sha")
    except ModuleNotFoundError:
        commit_module = None

    if commit_module is not None:
        commit_sha = getattr(commit_module, "_COMMIT_SHA", None)
        if commit_sha:
            return commit_sha

    env_commit_sha = os.getenv(COMMIT_SHA_ENV_VAR)
    if env_commit_sha:
        return env_commit_sha

    return UNKNOWN_COMMIT_SHA


def get_release_info() -> dict[str, str]:
    return {
        "package": PACKAGE_NAME,
        "version": get_version(),
        "commit_sha": get_commit_sha(),
        "source_url": SOURCE_URL,
        "license": LICENSE,
    }
