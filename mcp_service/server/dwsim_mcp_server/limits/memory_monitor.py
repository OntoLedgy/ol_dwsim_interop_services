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
        self._recovery_pending = False
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
            if self._breached and self._recovery_pending and self._last_sample_at is not None:
                recovery_threshold = int(self._limit_bytes * self._recovery_ratio)
                if (
                    self._last_rss_bytes <= recovery_threshold
                    and (time.monotonic() - self._last_sample_at) >= self._poll_interval
                ):
                    self._breached = False
                    self._recovery_pending = False
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
                now = time.monotonic()
                recovery_threshold = int(self._limit_bytes * self._recovery_ratio)
                with self._lock:
                    self._last_rss_bytes = rss_bytes
                    self._last_sample_at = now
                    if rss_bytes > self._limit_bytes and not self._breached:
                        self._breached = True
                        self._recovery_pending = False
                        self._logger.warning(
                            "memory_limit_exceeded",
                            extra={
                                "rss_bytes": rss_bytes,
                                "limit_bytes": self._limit_bytes,
                            },
                        )
                    elif self._breached:
                        if rss_bytes <= recovery_threshold:
                            if self._recovery_pending:
                                self._breached = False
                                self._recovery_pending = False
                                self._logger.info(
                                    "memory_limit_recovered",
                                    extra={
                                        "rss_bytes": rss_bytes,
                                        "recovery_threshold_bytes": recovery_threshold,
                                    },
                                )
                            else:
                                self._recovery_pending = True
                        else:
                            self._recovery_pending = False
            except Exception:
                self._logger.exception("memory_monitor_failed")
