# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Legacy tool helpers for compatibility with pre-FastMCP tests."""


class _LegacyRequestContext:
    def __init__(self, dependencies) -> None:
        self.lifespan_context = dependencies


class LegacyContext:
    """Compatibility wrapper to emulate FastMCP request context."""

    def __init__(self, dependencies) -> None:
        self.lifespan_context = dependencies
        # Keep request_context for backward compatibility with older tests
        self.request_context = _LegacyRequestContext(dependencies)
