"""Operation timeout enforcement for blocking interop calls."""

from __future__ import annotations

import asyncio
import time
from concurrent.futures import ThreadPoolExecutor
from typing import Callable, Optional, TypeVar

TResult = TypeVar("TResult")


class OperationTimeoutError(RuntimeError):
    """Raised when an operation exceeds its configured timeout."""

    def __init__(
        self,
        message: str,
        timeout_seconds: float,
        elapsed_seconds: float,
        session_id: Optional[str] = None,
    ) -> None:
        super().__init__(message)
        self.timeout_seconds = timeout_seconds
        self.elapsed_seconds = elapsed_seconds
        self.session_id = session_id


class OperationTimeoutRunner:
    """Run blocking operations with asyncio timeout enforcement."""

    def __init__(self) -> None:
        self._executor = ThreadPoolExecutor(max_workers=1, thread_name_prefix="interop")

    async def run_with_timeout(
        self,
        func: Callable[[], TResult],
        timeout_seconds: float,
        *,
        session_id: Optional[str] = None,
    ) -> TResult:
        loop = asyncio.get_running_loop()
        if timeout_seconds <= 0:
            return await loop.run_in_executor(self._executor, func)

        start_time = time.monotonic()
        try:
            return await asyncio.wait_for(
                loop.run_in_executor(self._executor, func),
                timeout=timeout_seconds,
            )
        except asyncio.TimeoutError as exc:
            elapsed = time.monotonic() - start_time
            message = f"Operation timed out after {timeout_seconds} seconds."
            raise OperationTimeoutError(message, timeout_seconds, elapsed, session_id) from exc
