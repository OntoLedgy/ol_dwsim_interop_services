"""DiagnosticsService for server/session diagnostics and error bundles."""

from __future__ import annotations

from collections import OrderedDict
from datetime import datetime, timezone
import threading
import time
import traceback
import uuid
from typing import Any, Dict, Optional

from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.limits.memory_monitor import MemoryMonitor
from dwsim_mcp_server.limits.session_lifetime_tracker import SessionLifetimeTracker
from dwsim_mcp_server.models.diagnostics import (
    DiagnosticBundle,
    MemorySnapshot,
    ServerDiagnostics,
    SessionDiagnostics,
)
from dwsim_mcp_server.observability.metrics import MetricsCollector


class SessionNotFoundError(KeyError):
    """Raised when a session is missing from diagnostics tracking."""


class DiagnosticsService:
    """Service for diagnostics and error bundle capture."""

    _max_bundles = 100
    _retention_seconds = 24 * 60 * 60

    def __init__(self, *, session_client: LimitedSessionClient) -> None:
        self._session_client = session_client
        self._server_start = time.monotonic()
        self._bundles: "OrderedDict[str, DiagnosticBundle]" = OrderedDict()
        self._bundles_lock = threading.Lock()
        self._error_count = 0
        self._metrics = MetricsCollector.get_instance()

    def get_server_diagnostics(self) -> ServerDiagnostics:
        uptime_seconds = time.monotonic() - self._server_start
        active_sessions = self._get_active_sessions_count(self._session_client._session_tracker)
        memory = self._get_memory_snapshot(self._session_client._memory_monitor)
        error_count = self._get_error_count()
        metrics_text = self._metrics.get_metrics_text()
        return ServerDiagnostics(
            uptime_seconds=uptime_seconds,
            active_sessions=active_sessions,
            memory=memory,
            error_count=error_count,
            metrics_text=metrics_text or None,
        )

    def get_session_diagnostics(self, session_id: str) -> SessionDiagnostics:
        tracker = self._session_client._session_tracker
        with tracker._lock:
            exists = session_id in tracker._sessions
        if not exists:
            raise SessionNotFoundError(f"Session '{session_id}' not found.")

        remaining = tracker.remaining(session_id)
        state = "expired" if tracker.is_expired(session_id) else "active"
        memory = self._get_memory_snapshot(self._session_client._memory_monitor)
        return SessionDiagnostics(
            session_id=session_id,
            state=state,
            remaining_lifetime_seconds=remaining,
            memory=memory,
        )

    def record_error(
        self,
        session_id: Optional[str],
        error: object,
        context: Optional[Dict[str, Any]] = None,
    ) -> str:
        bundle_id = str(uuid.uuid4())
        created_at = datetime.now(timezone.utc)
        error_type = _error_type_name(error)
        error_message = _error_message(error)
        stack_trace = _format_stack_trace(error)
        memory = self._get_memory_snapshot(self._session_client._memory_monitor)
        uptime_seconds = time.monotonic() - self._server_start
        metrics_text = self._metrics.get_metrics_text()
        bundle = DiagnosticBundle(
            bundle_id=bundle_id,
            session_id=session_id,
            created_at=created_at,
            error_type=error_type,
            error_message=error_message,
            stack_trace=stack_trace,
            context=_normalize_context(context),
            server_uptime_seconds=uptime_seconds,
            memory=memory,
            metrics_text=metrics_text or None,
        )

        with self._bundles_lock:
            self._error_count += 1
            self._bundles[bundle_id] = bundle
            self._prune_locked()

        return bundle_id

    def get_diagnostic_bundle(self, bundle_id: str) -> Optional[DiagnosticBundle]:
        with self._bundles_lock:
            self._prune_locked()
            return self._bundles.get(bundle_id)

    def _prune_locked(self) -> None:
        now = datetime.now(timezone.utc)
        cutoff = now.timestamp() - self._retention_seconds
        expired_ids = []
        for bundle_id, bundle in self._bundles.items():
            created_at = bundle.created_at
            if created_at.tzinfo is None:
                created_at = created_at.replace(tzinfo=timezone.utc)
            if created_at.timestamp() < cutoff:
                expired_ids.append(bundle_id)
        for bundle_id in expired_ids:
            self._bundles.pop(bundle_id, None)
        while len(self._bundles) > self._max_bundles:
            self._bundles.popitem(last=False)

    def _get_error_count(self) -> int:
        with self._bundles_lock:
            return self._error_count

    @staticmethod
    def _get_memory_snapshot(memory_monitor: MemoryMonitor) -> MemorySnapshot:
        snapshot = memory_monitor.snapshot()
        return MemorySnapshot(
            rss_bytes=int(snapshot.get("rss_bytes", 0)),
            limit_bytes=int(snapshot.get("limit_bytes", 0)),
            recovery_threshold_bytes=int(snapshot.get("recovery_threshold_bytes", 0)),
            breached=bool(snapshot.get("breached", False)),
        )

    @staticmethod
    def _get_active_sessions_count(tracker: SessionLifetimeTracker) -> int:
        with tracker._lock:
            return len(tracker._sessions)


def _error_type_name(error: object) -> str:
    if isinstance(error, BaseException):
        return error.__class__.__name__
    return "Error"


def _error_message(error: object) -> str:
    if isinstance(error, BaseException):
        message = str(error)
        return message or repr(error)
    return str(error)


def _format_stack_trace(error: object) -> Optional[str]:
    if not isinstance(error, BaseException):
        return None
    trace = "".join(traceback.format_exception(type(error), error, error.__traceback__))
    if not trace:
        return None
    return _truncate_stack_trace(trace)


def _truncate_stack_trace(trace: str, *, max_lines: int = 20, max_chars: int = 2000) -> str:
    lines = trace.splitlines()
    truncated = False
    if len(lines) > max_lines:
        lines = lines[:max_lines]
        truncated = True
    result = "\n".join(lines)
    if len(result) > max_chars:
        result = result[:max_chars]
        truncated = True
    if truncated:
        result = result.rstrip() + "\n... (truncated)"
    return result


def _normalize_context(context: Optional[Dict[str, Any]]) -> Dict[str, Any]:
    if context is None:
        return {}
    if isinstance(context, dict):
        return dict(context)
    return {"context": context}
