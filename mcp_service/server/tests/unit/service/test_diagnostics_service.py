import threading
from datetime import datetime, timedelta, timezone

import pytest

from dwsim_mcp_server.models.diagnostics import DiagnosticBundle, MemorySnapshot
from dwsim_mcp_server.service.diagnostics_service import (
    DiagnosticsService,
    SessionNotFoundError,
)


class FakeMetricsCollector:
    def __init__(self, metrics_text="metrics") -> None:
        self._metrics_text = metrics_text

    def get_metrics_text(self) -> str:
        return self._metrics_text


class FakeMemoryMonitor:
    def __init__(self, snapshot: dict) -> None:
        self._snapshot = dict(snapshot)

    def snapshot(self) -> dict:
        return dict(self._snapshot)


class FakeSessionTracker:
    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._sessions = {}
        self._remaining_by_id = {}
        self._expired_by_id = {}

    def add_session(self, session_id: str, *, remaining: float = 120.0, expired: bool = False) -> None:
        with self._lock:
            self._sessions[session_id] = (0.0, 0.0)
        self._remaining_by_id[session_id] = remaining
        self._expired_by_id[session_id] = expired

    def remaining(self, session_id: str):
        return self._remaining_by_id.get(session_id)

    def is_expired(self, session_id: str) -> bool:
        return bool(self._expired_by_id.get(session_id, False))


class FakeSessionClient:
    def __init__(self, tracker: FakeSessionTracker, memory_monitor: FakeMemoryMonitor) -> None:
        self._session_tracker = tracker
        self._memory_monitor = memory_monitor


def _make_service(monkeypatch, *, metrics_text="metrics"):
    tracker = FakeSessionTracker()
    memory_monitor = FakeMemoryMonitor(
        {
            "rss_bytes": 1024,
            "limit_bytes": 2048,
            "recovery_threshold_bytes": 1536,
            "breached": False,
        }
    )
    session_client = FakeSessionClient(tracker, memory_monitor)

    monkeypatch.setattr(
        "dwsim_mcp_server.service.diagnostics_service.MetricsCollector.get_instance",
        lambda: FakeMetricsCollector(metrics_text=metrics_text),
    )

    return DiagnosticsService(session_client=session_client), tracker


def test_get_server_diagnostics_returns_expected_snapshot(monkeypatch):
    service, tracker = _make_service(monkeypatch, metrics_text="sample-metrics")
    tracker.add_session("session-a")
    tracker.add_session("session-b")

    diagnostics = service.get_server_diagnostics()

    assert diagnostics.active_sessions == 2
    assert diagnostics.memory == MemorySnapshot(
        rss_bytes=1024,
        limit_bytes=2048,
        recovery_threshold_bytes=1536,
        breached=False,
    )
    assert diagnostics.metrics_text == "sample-metrics"
    assert diagnostics.error_count == 0
    assert diagnostics.uptime_seconds >= 0


def test_get_server_diagnostics_includes_error_count(monkeypatch):
    service, _tracker = _make_service(monkeypatch, metrics_text="")

    service.record_error("session-x", RuntimeError("boom"))

    diagnostics = service.get_server_diagnostics()
    assert diagnostics.error_count == 1
    assert diagnostics.metrics_text is None


def test_get_session_diagnostics_returns_details(monkeypatch):
    service, tracker = _make_service(monkeypatch)
    tracker.add_session("session-1", remaining=42.5, expired=False)

    diagnostics = service.get_session_diagnostics("session-1")

    assert diagnostics.session_id == "session-1"
    assert diagnostics.state == "active"
    assert diagnostics.remaining_lifetime_seconds == 42.5
    assert diagnostics.memory.rss_bytes == 1024


def test_get_session_diagnostics_marks_expired(monkeypatch):
    service, tracker = _make_service(monkeypatch)
    tracker.add_session("session-2", remaining=0.0, expired=True)

    diagnostics = service.get_session_diagnostics("session-2")

    assert diagnostics.state == "expired"
    assert diagnostics.remaining_lifetime_seconds == 0.0


def test_get_session_diagnostics_raises_for_missing_session(monkeypatch):
    service, _tracker = _make_service(monkeypatch)

    with pytest.raises(SessionNotFoundError):
        service.get_session_diagnostics("missing")


def test_record_error_creates_bundle_and_returns_id(monkeypatch):
    service, _tracker = _make_service(monkeypatch, metrics_text="metrics-text")

    bundle_id = service.record_error(
        "session-3",
        ValueError("bad input"),
        context={"step": "validate"},
    )

    bundle = service.get_diagnostic_bundle(bundle_id)
    assert bundle is not None
    assert bundle.bundle_id == bundle_id
    assert bundle.session_id == "session-3"
    assert bundle.error_type == "ValueError"
    assert "bad input" in bundle.error_message
    assert bundle.stack_trace is not None
    assert bundle.context == {"step": "validate"}
    assert bundle.memory is not None
    assert bundle.metrics_text == "metrics-text"


def test_record_error_handles_non_exception(monkeypatch):
    service, _tracker = _make_service(monkeypatch)

    bundle_id = service.record_error(None, "plain error")
    bundle = service.get_diagnostic_bundle(bundle_id)

    assert bundle is not None
    assert bundle.error_type == "Error"
    assert bundle.error_message == "plain error"
    assert bundle.stack_trace is None


def test_get_diagnostic_bundle_returns_none_when_missing(monkeypatch):
    service, _tracker = _make_service(monkeypatch)

    assert service.get_diagnostic_bundle("missing") is None


def test_bundle_prunes_expired_entries(monkeypatch):
    service, _tracker = _make_service(monkeypatch)
    service._retention_seconds = 1

    expired_bundle = DiagnosticBundle(
        bundle_id="expired",
        session_id=None,
        created_at=datetime.now(timezone.utc) - timedelta(seconds=5),
        error_type="Error",
        error_message="expired",
        stack_trace=None,
        context={},
        server_uptime_seconds=0.0,
        memory=None,
        metrics_text=None,
    )

    with service._bundles_lock:
        service._bundles["expired"] = expired_bundle

    assert service.get_diagnostic_bundle("expired") is None
    with service._bundles_lock:
        assert "expired" not in service._bundles


def test_bundle_prunes_oldest_when_over_max(monkeypatch):
    service, _tracker = _make_service(monkeypatch)
    service._max_bundles = 3

    bundle_ids = []
    for idx in range(4):
        bundle_ids.append(service.record_error(f"session-{idx}", RuntimeError("err")))

    with service._bundles_lock:
        remaining_ids = list(service._bundles.keys())

    assert len(remaining_ids) == 3
    assert bundle_ids[0] not in remaining_ids
