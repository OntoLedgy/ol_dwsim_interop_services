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

"""Interop exception definitions and helpers for pythonnet bridge."""

from __future__ import annotations

from typing import Iterable, Optional


class InteropError(RuntimeError):
    """Base error for pythonnet interop failures."""

    def __init__(self, message: str, kind: str = "interop") -> None:
        super().__init__(message)
        self.kind = kind


class AssemblyLoadError(InteropError):
    """Raised when the .NET assembly cannot be loaded."""

    def __init__(self, message: str) -> None:
        super().__init__(message, kind="assembly_load")


class SessionError(InteropError):
    """Raised when session operations fail."""

    def __init__(self, message: str) -> None:
        super().__init__(message, kind="session")


class TypeConversionError(InteropError):
    """Raised when interop type conversion fails."""

    def __init__(self, message: str) -> None:
        super().__init__(message, kind="conversion")


def _iter_exception_chain(exc: BaseException) -> Iterable[str]:
    current: Optional[BaseException] = exc
    while current is not None:
        message = getattr(current, "Message", None)
        yield str(message or current)
        inner = getattr(current, "InnerException", None)
        if inner is not None:
            current = inner
            continue
        current = current.__cause__ or current.__context__


def flatten_exception_messages(exc: BaseException) -> str:
    """Return a condensed causal chain message for an exception."""
    messages = [msg for msg in _iter_exception_chain(exc) if msg]
    return " -> ".join(messages) if messages else str(exc)


def map_dotnet_exception(exc: BaseException, kind: str = "interop") -> InteropError:
    """Map a .NET exception to an interop-specific Python exception."""
    message = flatten_exception_messages(exc)
    if kind == "assembly_load":
        return AssemblyLoadError(message)
    if kind == "session":
        return SessionError(message)
    if kind == "conversion":
        return TypeConversionError(message)
    return InteropError(message, kind=kind)
