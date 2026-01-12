"""SessionClient wrapper that enforces resource limits."""

from __future__ import annotations

import logging
from typing import Optional

from dwsim_mcp_server.config.resource_limit_settings import ResourceLimitSettings
from dwsim_mcp_server.ipc.session_client import SessionClient
from dwsim_mcp_server.limits.memory_monitor import MemoryMonitor
from dwsim_mcp_server.limits.operation_timeout_runner import OperationTimeoutRunner
from dwsim_mcp_server.limits.resource_limit_guard import ResourceLimitGuard
from dwsim_mcp_server.limits.session_lifetime_tracker import SessionLifetimeTracker


class LimitedSessionClient:
    """SessionClient wrapper with timeout, session, and memory limits."""

    def __init__(
        self,
        settings: ResourceLimitSettings,
        *,
        session_client: Optional[SessionClient] = None,
        logger: Optional[logging.Logger] = None,
    ) -> None:
        self._settings = settings
        self._logger = logger or logging.getLogger(__name__)
        self._session_client = session_client or SessionClient()
        self._session_tracker = SessionLifetimeTracker()
        self._timeout_runner = OperationTimeoutRunner()
        self._memory_monitor = MemoryMonitor(
            memory_limit_mb=settings.memory_limit_mb,
            poll_interval_seconds=settings.memory_poll_interval_seconds,
            recovery_ratio=settings.memory_recovery_ratio,
            logger=self._logger,
        )
        self._guard = ResourceLimitGuard(
            settings,
            self._session_tracker,
            self._timeout_runner,
            self._memory_monitor,
            logger=self._logger,
        )

    def start_monitoring(self) -> None:
        """Start background memory monitoring."""
        self._memory_monitor.start()

    async def stop_monitoring(self) -> None:
        """Stop background memory monitoring."""
        await self._memory_monitor.stop()

    async def create_session(
        self,
        *,
        flowsheet_name: Optional[str] = None,
        timeout_seconds: Optional[int] = None,
    ) -> str:
        def _create() -> str:
            return self._session_client.create_session(flowsheet_name)

        session_id = await self._guard.run(None, _create)
        session_timeout = timeout_seconds or self._settings.session_timeout_seconds
        self._session_tracker.register(session_id, float(session_timeout))
        return session_id

    async def close_session(self, session_id: str) -> bool:
        def _close() -> bool:
            return self._session_client.close_session(session_id)

        result = await self._guard.run(
            session_id,
            _close,
            on_expired=lambda: self._session_client.close_session(session_id),
        )
        if result:
            self._session_tracker.remove(session_id)
        return result

    async def save_case(self, session_id: str, file_path: str) -> bool:
        def _save() -> bool:
            return self._session_client.save_case(session_id, file_path)

        return await self.run_session_operation(session_id, _save)

    async def load_case(self, session_id: str, file_path: str) -> bool:
        def _load() -> bool:
            return self._session_client.load_case(session_id, file_path)

        return await self.run_session_operation(session_id, _load)

    async def run_calculation(self, session_id: str, *, timeout_seconds: Optional[int] = None):
        def _run():
            return self._session_client.run_calculation(session_id, timeout_seconds=timeout_seconds)

        return await self.run_session_operation(
            session_id,
            _run,
            timeout_override=float(timeout_seconds) if timeout_seconds is not None else None,
        )

    async def get_calculation_status(self, session_id: str):
        def _status():
            return self._session_client.get_calculation_status(session_id)

        return await self.run_session_operation(session_id, _status)

    async def get_calculation_results(self, session_id: str, *, object_id: Optional[str] = None):
        def _results():
            return self._session_client.get_calculation_results(session_id, object_id=object_id)

        return await self.run_session_operation(session_id, _results)

    async def run_session_operation(self, session_id: str, func, *, timeout_override: Optional[float] = None):
        """Run an operation within a session with limit enforcement."""
        return await self._guard.run(
            session_id,
            func,
            timeout_override=timeout_override,
            on_expired=lambda: self._session_client.close_session(session_id),
        )

    def dispose(self) -> None:
        """Dispose underlying session resources."""
        self._session_client.dispose()

    @property
    def session_manager(self):
        """Expose the underlying SessionManager for dependent clients."""
        return self._session_client.session_manager
