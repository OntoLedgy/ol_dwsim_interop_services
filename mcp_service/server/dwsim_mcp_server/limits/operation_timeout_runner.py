# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Operation timeout enforcement for blocking interop calls."""

from __future__ import annotations

import asyncio
import queue
import sys
import threading
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


class STAThreadExecutor:
    """Custom executor that runs tasks on a single STA thread for COM interop."""

    def __init__(self) -> None:
        self._task_queue: queue.Queue = queue.Queue()
        self._thread: Optional[threading.Thread] = None
        self._shutdown = False
        self._start_worker()

    def _start_worker(self) -> None:
        """Start the STA worker thread."""
        self._thread = threading.Thread(target=self._worker_loop, daemon=True, name="STAWorker")
        self._thread.start()
        print(f"[STAThreadExecutor] Started STA worker thread {self._thread.ident}", file=sys.stderr)

    def _worker_loop(self) -> None:
        """Worker loop that runs on the STA thread."""
        thread_id = threading.get_ident()
        print(f"[STAWorker] Thread {thread_id} starting", file=sys.stderr)

        # CRITICAL: Initialize COM as STA using Windows API BEFORE loading pythonnet
        # This must happen before any pythonnet/CLR initialization
        try:
            import ctypes
            # CoInitializeEx flags: COINIT_APARTMENTTHREADED = 0x2 (STA)
            ole32 = ctypes.windll.ole32
            result = ole32.CoInitializeEx(None, 0x2)  # 0x2 = COINIT_APARTMENTTHREADED
            if result == 0 or result == 1:  # S_OK or S_FALSE (already initialized)
                print(f"[STAWorker] Thread {thread_id} COM initialized as STA (result: {result})", file=sys.stderr)
            else:
                print(f"[STAWorker] Thread {thread_id} CoInitializeEx returned: {hex(result)}", file=sys.stderr)
        except Exception as e:
            print(f"[STAWorker] Failed to initialize COM as STA: {e}", file=sys.stderr)

        # Now load pythonnet - it should respect the existing COM apartment state
        try:
            from dwsim_mcp_server.ipc.clr_loader import load_dwsim_worker
            load_dwsim_worker()

            # Verify the apartment state
            from System.Threading import Thread as DotNetThread  # type: ignore
            state = DotNetThread.CurrentThread.GetApartmentState()
            print(f"[STAWorker] Thread {thread_id} .NET apartment state: {state}", file=sys.stderr)
        except Exception as e:
            print(f"[STAWorker] Failed to verify apartment state: {e}", file=sys.stderr)

        # Process tasks
        while not self._shutdown:
            try:
                task = self._task_queue.get(timeout=0.1)
                if task is None:  # Shutdown signal
                    break

                func, result_queue = task
                try:
                    result = func()
                    result_queue.put(("success", result))
                except Exception as e:
                    result_queue.put(("error", e))
            except queue.Empty:
                continue

        print(f"[STAWorker] Thread {thread_id} exiting", file=sys.stderr)

    def submit(self, func: Callable[[], TResult]) -> TResult:
        """Submit a function to run on the STA thread."""
        if self._shutdown:
            raise RuntimeError("Executor is shut down")

        result_queue: queue.Queue = queue.Queue()
        self._task_queue.put((func, result_queue))

        # Wait for result
        status, value = result_queue.get()
        if status == "error":
            raise value
        return value

    def shutdown(self) -> None:
        """Shutdown the executor."""
        self._shutdown = True
        self._task_queue.put(None)  # Signal shutdown
        if self._thread:
            self._thread.join(timeout=5.0)


class OperationTimeoutRunner:
    """Run blocking operations with asyncio timeout enforcement."""

    def __init__(self) -> None:
        self._executor = STAThreadExecutor()

    async def run_with_timeout(
        self,
        func: Callable[[], TResult],
        timeout_seconds: float,
        *,
        session_id: Optional[str] = None,
    ) -> TResult:
        loop = asyncio.get_running_loop()

        # Wrap the synchronous executor.submit in an async call
        def run_in_sta_thread():
            return self._executor.submit(func)

        if timeout_seconds <= 0:
            return await loop.run_in_executor(None, run_in_sta_thread)

        start_time = time.monotonic()
        try:
            return await asyncio.wait_for(
                loop.run_in_executor(None, run_in_sta_thread),
                timeout=timeout_seconds,
            )
        except asyncio.TimeoutError as exc:
            elapsed = time.monotonic() - start_time
            message = f"Operation timed out after {timeout_seconds} seconds."
            raise OperationTimeoutError(message, timeout_seconds, elapsed, session_id) from exc
