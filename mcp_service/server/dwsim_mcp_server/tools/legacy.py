"""Legacy tool helpers for compatibility with pre-FastMCP tests."""

from __future__ import annotations


class _LegacyRequestContext:
    def __init__(self, dependencies) -> None:
        self.lifespan_context = dependencies


class LegacyContext:
    """Compatibility wrapper to emulate FastMCP request context."""

    def __init__(self, dependencies) -> None:
        self.request_context = _LegacyRequestContext(dependencies)
