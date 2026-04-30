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

"""Process memory monitoring for resource limit enforcement."""

from __future__ import annotations

import asyncio
import logging
import os
import time
import threading
from typing import Optional

import psutil


class MemoryMonitor:
    """Monitor process memory usage and track breach state."""

    def __init__(
        self,
        *,
        memory_limit_mb: int,
        poll_interval_seconds: float,
        recovery_ratio: float,
        logger: Optional[logging.Logger] = None,
    ) -> None:
        self._limit_bytes = memory_limit_mb * 1024 * 1024
        self._poll_interval = poll_interval_seconds
        self._recovery_ratio = recovery_ratio
        self._logger = logger or logging.getLogger(__name__)
        self._process = psutil.Process(os.getpid())
        self._lock = threading.Lock()
        self._breached = False
        self._last_rss_bytes = 0
        self._last_sample_at: Optional[float] = None
        self._task: Optional[asyncio.Task[None]] = None
        self._running = False

    def start(self) -> None:
        """Start the background memory polling task."""
        if self._task is not None:
            return
        self._running = True
        loop = asyncio.get_running_loop()
        self._task = loop.create_task(self._run())

    async def stop(self) -> None:
        """Stop the background memory polling task."""
        if self._task is None:
            return
        self._running = False
        self._task.cancel()
        try:
            await self._task
        except asyncio.CancelledError:
            pass
        finally:
            self._task = None

    def is_exceeded(self) -> bool:
        """Return True if the memory limit breach flag is set."""
        with self._lock:
            return self._breached

    def snapshot(self) -> dict:
        """Return the latest memory snapshot and configured thresholds."""
        with self._lock:
            return {
                "rss_bytes": self._last_rss_bytes,
                "limit_bytes": self._limit_bytes,
                "recovery_threshold_bytes": int(self._limit_bytes * self._recovery_ratio),
                "breached": self._breached,
            }

    async def _run(self) -> None:
        while self._running:
            await asyncio.sleep(self._poll_interval)
            try:
                rss_bytes = self._process.memory_info().rss
                self._update_state(rss_bytes)
            except Exception:
                self._logger.exception("memory_monitor_failed")

    def _update_state(self, rss_bytes: int) -> None:
        now = time.monotonic()
        recovery_threshold = int(self._limit_bytes * self._recovery_ratio)
        with self._lock:
            self._last_rss_bytes = rss_bytes
            self._last_sample_at = now
            if rss_bytes > self._limit_bytes and not self._breached:
                self._breached = True
                self._logger.warning(
                    "memory_limit_exceeded",
                    extra={
                        "rss_bytes": rss_bytes,
                        "limit_bytes": self._limit_bytes,
                    },
                )
            elif self._breached and rss_bytes <= recovery_threshold:
                self._breached = False
                self._logger.info(
                    "memory_limit_recovered",
                    extra={
                        "rss_bytes": rss_bytes,
                        "recovery_threshold_bytes": recovery_threshold,
                    },
                )
