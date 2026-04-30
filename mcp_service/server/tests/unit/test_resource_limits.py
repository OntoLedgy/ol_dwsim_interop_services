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

import asyncio
from types import SimpleNamespace

import pytest

from dwsim_mcp_server.limits.memory_monitor import MemoryMonitor
from dwsim_mcp_server.limits.operation_timeout_runner import (
    OperationTimeoutError,
    OperationTimeoutRunner,
)
from dwsim_mcp_server.limits.session_lifetime_tracker import SessionLifetimeTracker


def test_session_lifetime_tracker_expiry(monkeypatch):
    tracker = SessionLifetimeTracker()
    times = iter([10.0, 12.0, 15.0, 16.0])
    monkeypatch.setattr("dwsim_mcp_server.limits.session_lifetime_tracker.time.monotonic", lambda: next(times))

    tracker.register("session-1", 5.0)
    assert tracker.is_expired("session-1") is False
    assert tracker.remaining("session-1") == pytest.approx(0.0)
    assert tracker.is_expired("session-1") is True


def test_operation_timeout_runner_times_out():
    runner = OperationTimeoutRunner()

    def slow_operation():
        import time

        time.sleep(0.05)
        return "done"

    with pytest.raises(OperationTimeoutError) as exc_info:
        asyncio.run(runner.run_with_timeout(slow_operation, 0.01, session_id="session-1"))

    assert exc_info.value.session_id == "session-1"
    assert exc_info.value.timeout_seconds == pytest.approx(0.01)


def test_memory_monitor_breach_and_recovery(monkeypatch):
    class FakeProcess:
        def __init__(self):
            self._values = [500_000, 2_000_000, 700_000]
            self._index = 0

        def memory_info(self):
            if self._index < len(self._values):
                value = self._values[self._index]
                self._index += 1
            else:
                value = self._values[-1]
            return SimpleNamespace(rss=value)

    monkeypatch.setattr("dwsim_mcp_server.limits.memory_monitor.psutil.Process", lambda _pid: FakeProcess())

    monitor = MemoryMonitor(
        memory_limit_mb=1,
        poll_interval_seconds=0.01,
        recovery_ratio=0.7,
    )
    async def _run_monitor_checks():
        monitor.start()
        await asyncio.sleep(0.03)  # Increased for reliability
        first = monitor.is_exceeded()
        await asyncio.sleep(0.03)  # Increased for reliability
        second = monitor.is_exceeded()
        await asyncio.sleep(0.03)  # Increased for reliability
        third = monitor.is_exceeded()
        snapshot = monitor.snapshot()
        await monitor.stop()
        return first, second, third, snapshot

    first, second, third, snapshot = asyncio.run(_run_monitor_checks())
    assert first is False
    assert second is True
    assert third is False
    assert snapshot["limit_bytes"] == 1 * 1024 * 1024
