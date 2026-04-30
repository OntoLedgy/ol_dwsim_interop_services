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

"""Resource limit guard for enforcing timeouts and memory caps."""

from __future__ import annotations

import logging
from typing import Callable, Optional, TypeVar

from dwsim_mcp_server.models.errors.resource_limit_error import ResourceLimitError

from dwsim_mcp_server.config.resource_limit_settings import ResourceLimitSettings
from dwsim_mcp_server.limits.memory_monitor import MemoryMonitor
from dwsim_mcp_server.limits.operation_timeout_runner import (
    OperationTimeoutError,
    OperationTimeoutRunner,
)
from dwsim_mcp_server.limits.session_lifetime_tracker import SessionLifetimeTracker

TResult = TypeVar("TResult")


class ResourceLimitViolation(RuntimeError):
    """Raised when a resource limit is exceeded."""

    def __init__(self, error: ResourceLimitError) -> None:
        super().__init__(error.message)
        self.error = error


class ResourceLimitGuard:
    """Coordinates resource limit checks before executing operations."""

    def __init__(
        self,
        settings: ResourceLimitSettings,
        session_tracker: SessionLifetimeTracker,
        timeout_runner: OperationTimeoutRunner,
        memory_monitor: MemoryMonitor,
        *,
        logger: Optional[logging.Logger] = None,
    ) -> None:
        self._settings = settings
        self._session_tracker = session_tracker
        self._timeout_runner = timeout_runner
        self._memory_monitor = memory_monitor
        self._logger = logger or logging.getLogger(__name__)

    def ensure_can_run(
        self,
        session_id: Optional[str],
        *,
        on_expired: Optional[Callable[[], None]] = None,
    ) -> None:
        if self._memory_monitor.is_exceeded():
            snapshot = self._memory_monitor.snapshot()
            error = ResourceLimitError(
                code="RESOURCE_LIMIT_EXCEEDED",
                message="Memory limit exceeded; new operations are blocked.",
                session_id=session_id,
                details=snapshot,
            )
            self._logger.warning("resource_limit_exceeded", extra={"error": error.model_dump()})
            raise ResourceLimitViolation(error)

        if session_id and self._session_tracker.is_expired(session_id):
            if on_expired is not None:
                try:
                    on_expired()
                except Exception:
                    self._logger.exception("session_close_failed", extra={"session_id": session_id})
            error = ResourceLimitError(
                code="SESSION_EXPIRED",
                message="Session expired and must be recreated.",
                session_id=session_id,
                details={"remaining_seconds": 0},
            )
            self._logger.warning("session_expired", extra={"error": error.model_dump()})
            raise ResourceLimitViolation(error)

    async def run(
        self,
        session_id: Optional[str],
        func: Callable[[], TResult],
        *,
        timeout_override: Optional[float] = None,
        on_expired: Optional[Callable[[], None]] = None,
    ) -> TResult:
        self.ensure_can_run(session_id, on_expired=on_expired)
        timeout_seconds = (
            timeout_override if timeout_override is not None else self._settings.operation_timeout_seconds
        )
        try:
            return await self._timeout_runner.run_with_timeout(
                func,
                timeout_seconds,
                session_id=session_id,
            )
        except OperationTimeoutError as exc:
            error = ResourceLimitError(
                code="TIMEOUT",
                message=str(exc),
                session_id=session_id,
                details={
                    "timeout_seconds": exc.timeout_seconds,
                    "elapsed_seconds": exc.elapsed_seconds,
                },
            )
            self._logger.warning("operation_timeout", extra={"error": error.model_dump()})
            raise ResourceLimitViolation(error) from exc
