# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Correlation context for cross-layer tracing."""

from __future__ import annotations

from contextlib import contextmanager
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Iterator, Optional
import contextvars
import uuid


@dataclass(frozen=True)
class CorrelationContext:
    """Correlation metadata for a single request."""

    request_id: str
    session_id: Optional[str]
    tool_name: Optional[str]
    start_time: datetime


_current_context: contextvars.ContextVar[Optional[CorrelationContext]] = contextvars.ContextVar(
    "correlation_context",
    default=None,
)


def generate_request_id() -> str:
    """Generate a unique request identifier."""
    return str(uuid.uuid4())


def get_current_context() -> Optional[CorrelationContext]:
    """Return the current correlation context if available."""
    return _current_context.get()


def set_current_context(context: Optional[CorrelationContext]) -> contextvars.Token:
    """Set the current correlation context and return the reset token."""
    return _current_context.set(context)


def _build_context(
    request_id: Optional[str],
    session_id: Optional[str],
    tool_name: Optional[str],
) -> CorrelationContext:
    return CorrelationContext(
        request_id=request_id or generate_request_id(),
        session_id=session_id,
        tool_name=tool_name,
        start_time=datetime.now(timezone.utc),
    )


@contextmanager
def correlation_scope(
    *,
    request_id: Optional[str] = None,
    session_id: Optional[str] = None,
    tool_name: Optional[str] = None,
) -> Iterator[CorrelationContext]:
    """Context manager that sets and restores correlation context."""
    context = _build_context(request_id, session_id, tool_name)
    token = set_current_context(context)
    try:
        yield context
    finally:
        _current_context.reset(token)
