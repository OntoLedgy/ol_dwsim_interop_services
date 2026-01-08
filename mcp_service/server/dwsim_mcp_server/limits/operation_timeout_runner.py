"""Operation timeout enforcement for blocking interop calls."""

from __future__ import annotations

import asyncio
import time
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

    async def run_with_timeout(
        self,
        func: Callable[[], TResult],
        timeout_seconds: float,
        *,
        session_id: Optional[str] = None,
    ) -> TResult:
        if timeout_seconds <= 0:
            return await asyncio.to_thread(func)

        start_time = time.monotonic()
        try:
            return await asyncio.wait_for(
                asyncio.to_thread(func),
                timeout=timeout_seconds,
            )
        except asyncio.TimeoutError as exc:
            elapsed = time.monotonic() - start_time
            message = f"Operation timed out after {timeout_seconds} seconds."
            raise OperationTimeoutError(message, timeout_seconds, elapsed, session_id) from exc
