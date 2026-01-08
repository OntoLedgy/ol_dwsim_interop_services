"""Session lifetime tracking for resource limits."""

from __future__ import annotations

import threading
import time
from typing import Dict, Optional, Tuple


class SessionLifetimeTracker:
    """Tracks session lifetimes using monotonic time."""

    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._sessions: Dict[str, Tuple[float, float]] = {}

    def register(self, session_id: str, timeout_seconds: float) -> None:
        """Register a session with its timeout in seconds."""
        start_time = time.monotonic()
        with self._lock:
            self._sessions[session_id] = (start_time, timeout_seconds)

    def is_expired(self, session_id: str) -> bool:
        """Return True if the session has exceeded its lifetime."""
        with self._lock:
            entry = self._sessions.get(session_id)
        if entry is None:
            return False
        start_time, timeout_seconds = entry
        if timeout_seconds <= 0:
            return True
        return (time.monotonic() - start_time) > timeout_seconds

    def remaining(self, session_id: str) -> Optional[float]:
        """Return remaining lifetime in seconds, or None if unknown."""
        with self._lock:
            entry = self._sessions.get(session_id)
        if entry is None:
            return None
        start_time, timeout_seconds = entry
        if timeout_seconds <= 0:
            return 0.0
        remaining = timeout_seconds - (time.monotonic() - start_time)
        return max(0.0, remaining)

    def remove(self, session_id: str) -> None:
        """Remove tracking information for a session."""
        with self._lock:
            self._sessions.pop(session_id, None)
