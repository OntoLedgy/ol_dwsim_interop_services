"""Structured logging configuration for the MCP server."""

from __future__ import annotations

import asyncio
import json
import logging
from logging.handlers import RotatingFileHandler
import os
import sys
from collections import deque
from typing import Deque, Dict, List, Optional

import aiohttp
import structlog

from .correlation import get_current_context

_SEQ_URL_ENV = "DWSIM_SEQ_URL"
_SEQ_API_KEY_ENV = "DWSIM_SEQ_API_KEY"
_SEQ_ENDPOINT_PATH = "/api/events/raw"
_MAX_SEQ_BUFFER = 100
_DEFAULT_SEQ_BATCH_SIZE = 50
_DEFAULT_SEQ_FLUSH_INTERVAL = 1.0
_LOG_FILE_ENV = "DWSIM_LOG_FILE"
_LOG_FILE_MAX_BYTES_ENV = "DWSIM_LOG_FILE_MAX_BYTES"
_LOG_FILE_BACKUP_COUNT_ENV = "DWSIM_LOG_FILE_BACKUP_COUNT"
_DEFAULT_LOG_FILE_MAX_BYTES = 10 * 1024 * 1024
_DEFAULT_LOG_FILE_BACKUP_COUNT = 5


class SeqSink:
    def __init__(self) -> None:
        self._url = (os.getenv(_SEQ_URL_ENV) or "").strip()
        self._api_key = (os.getenv(_SEQ_API_KEY_ENV) or "").strip()
        self._buffer: Deque[Dict[str, object]] = deque()
        self._buffer_lock = asyncio.Lock()
        self._flush_event = asyncio.Event()
        self._stop_event = asyncio.Event()
        self._task: Optional[asyncio.Task] = None
        self._session: Optional[aiohttp.ClientSession] = None

    @property
    def enabled(self) -> bool:
        return bool(self._url)

    def enqueue(self, event_dict: Dict[str, object]) -> None:
        if not self.enabled:
            return
        clef_event = _to_clef(event_dict)
        if not clef_event:
            return
        loop = _get_running_loop()
        if loop is None:
            _enqueue_sync(self._buffer, clef_event)
            return
        loop.call_soon_threadsafe(self._enqueue_async, clef_event)

    def _enqueue_async(self, clef_event: Dict[str, object]) -> None:
        async def _enqueue() -> None:
            async with self._buffer_lock:
                _append_bounded(self._buffer, clef_event)
            self._flush_event.set()

        asyncio.create_task(_enqueue())

    async def start(self) -> None:
        if not self.enabled:
            return
        if self._task is not None:
            return
        self._session = aiohttp.ClientSession(timeout=aiohttp.ClientTimeout(total=5))
        self._stop_event.clear()
        self._task = asyncio.create_task(self._flush_loop())
        self._flush_event.set()

    async def stop(self) -> None:
        if self._task is None:
            return
        self._stop_event.set()
        self._flush_event.set()
        await self._task
        self._task = None
        if self._session is not None:
            await self._session.close()
            self._session = None

    async def _flush_loop(self) -> None:
        while not self._stop_event.is_set():
            try:
                await asyncio.wait_for(self._flush_event.wait(), timeout=_DEFAULT_SEQ_FLUSH_INTERVAL)
            except asyncio.TimeoutError:
                pass
            self._flush_event.clear()
            await self._flush_once()

    async def _flush_once(self) -> None:
        while True:
            batch = await self._drain_batch(_DEFAULT_SEQ_BATCH_SIZE)
            if not batch:
                return
            success = await self._send_batch(batch)
            if not success:
                return

    async def _drain_batch(self, max_items: int) -> List[Dict[str, object]]:
        async with self._buffer_lock:
            if not self._buffer:
                return []
            batch: List[Dict[str, object]] = []
            for _ in range(min(max_items, len(self._buffer))):
                batch.append(self._buffer.popleft())
            return batch

    async def _send_batch(self, batch: List[Dict[str, object]]) -> bool:
        if not batch:
            return True
        if not self._session:
            await self._requeue(batch)
            return False

        url = self._url.rstrip("/") + _SEQ_ENDPOINT_PATH
        headers = {"Content-Type": "application/vnd.serilog.clef"}
        if self._api_key:
            headers["X-Seq-ApiKey"] = self._api_key

        payload = "\n".join(json.dumps(item, separators=(",", ":"), default=str) for item in batch)

        try:
            async with self._session.post(url, data=payload, headers=headers) as response:
                if response.status >= 400:
                    raise RuntimeError(f"Seq returned HTTP {response.status}")
        except Exception as exc:  # noqa: BLE001 - surface all failures as warning
            logging.getLogger(__name__).warning("Seq sink unavailable: %s", exc)
            await self._requeue(batch)
            return False
        return True

    async def _requeue(self, batch: List[Dict[str, object]]) -> None:
        async with self._buffer_lock:
            for item in reversed(batch):
                _append_bounded_left(self._buffer, item)


_seq_sink: Optional[SeqSink] = None


def _get_running_loop() -> Optional[asyncio.AbstractEventLoop]:
    try:
        return asyncio.get_running_loop()
    except RuntimeError:
        return None


def _append_bounded(buffer: Deque[Dict[str, object]], item: Dict[str, object]) -> None:
    if len(buffer) >= _MAX_SEQ_BUFFER:
        buffer.popleft()
    buffer.append(item)


def _append_bounded_left(buffer: Deque[Dict[str, object]], item: Dict[str, object]) -> None:
    if len(buffer) >= _MAX_SEQ_BUFFER:
        buffer.pop()
    buffer.appendleft(item)


def _enqueue_sync(buffer: Deque[Dict[str, object]], item: Dict[str, object]) -> None:
    if len(buffer) >= _MAX_SEQ_BUFFER:
        buffer.popleft()
    buffer.append(item)


def _is_seq_configured() -> bool:
    return bool((os.getenv(_SEQ_URL_ENV) or "").strip())


def _to_clef(event_dict: Dict[str, object]) -> Dict[str, object]:
    clef: Dict[str, object] = {}
    timestamp = event_dict.get("timestamp")
    if timestamp:
        clef["@t"] = timestamp
    message = event_dict.get("event")
    if message is not None:
        clef["@m"] = str(message)
    level = event_dict.get("level")
    if level is not None:
        clef["@l"] = str(level)

    for key, value in event_dict.items():
        if key in {"timestamp", "event", "level"}:
            continue
        clef[key] = value

    return clef


def _seq_processor(logger, method_name, event_dict):
    if _seq_sink is None or not _seq_sink.enabled:
        return event_dict
    _seq_sink.enqueue(event_dict)
    return event_dict


def _add_correlation_context(logger, method_name, event_dict):
    context = get_current_context()
    if context is None:
        return event_dict

    event_dict["requestId"] = context.request_id
    if context.session_id:
        event_dict["sessionId"] = context.session_id
    if context.tool_name:
        event_dict["toolName"] = context.tool_name
    return event_dict


def _setup_file_handler() -> None:
    log_file = (os.getenv(_LOG_FILE_ENV) or "").strip()
    if not log_file:
        return

    max_bytes_raw = os.getenv(_LOG_FILE_MAX_BYTES_ENV)
    backup_count_raw = os.getenv(_LOG_FILE_BACKUP_COUNT_ENV)

    try:
        max_bytes = int(max_bytes_raw) if max_bytes_raw else _DEFAULT_LOG_FILE_MAX_BYTES
    except ValueError:
        max_bytes = _DEFAULT_LOG_FILE_MAX_BYTES

    try:
        backup_count = int(backup_count_raw) if backup_count_raw else _DEFAULT_LOG_FILE_BACKUP_COUNT
    except ValueError:
        backup_count = _DEFAULT_LOG_FILE_BACKUP_COUNT

    try:
        handler = RotatingFileHandler(
            log_file,
            maxBytes=max_bytes,
            backupCount=backup_count,
            encoding="utf-8",
        )
    except (OSError, PermissionError) as exc:
        logging.getLogger(__name__).warning("Unable to configure file logging: %s", exc)
        return

    handler.setFormatter(logging.Formatter("%(message)s"))
    logging.getLogger().addHandler(handler)


def configure_logging(log_level: str, *, json_format: bool = True) -> None:
    """Configure stdlib logging and structlog.

    Stdio MCP reserves stdout for JSON-RPC messages, so human-readable and
    structured diagnostics must go to stderr unless a file sink is configured.
    """
    level = getattr(logging, log_level.upper(), logging.INFO)
    logging.basicConfig(level=level, format="%(message)s", stream=sys.stderr)
    _setup_file_handler()

    processors = [
        structlog.processors.TimeStamper(fmt="iso"),
        structlog.processors.add_log_level,
        _add_correlation_context,
        structlog.processors.StackInfoRenderer(),
        structlog.processors.format_exc_info,
    ]

    if _is_seq_configured():
        processors.append(_seq_processor)

    renderer = structlog.processors.JSONRenderer() if json_format else structlog.dev.ConsoleRenderer()

    structlog.configure(
        processors=processors + [renderer],
        wrapper_class=structlog.make_filtering_bound_logger(level),
        logger_factory=structlog.PrintLoggerFactory(file=sys.stderr),
        cache_logger_on_first_use=True,
    )


async def start_seq_sink() -> None:
    global _seq_sink
    if _seq_sink is None:
        _seq_sink = SeqSink()
    if not _seq_sink.enabled:
        return
    await _seq_sink.start()


async def stop_seq_sink() -> None:
    global _seq_sink
    if _seq_sink is None:
        return
    await _seq_sink.stop()
    _seq_sink = None


def get_logger(name: Optional[str] = None) -> structlog.BoundLogger:
    """Return a structlog logger with optional name binding."""
    logger = structlog.get_logger()
    if name:
        return logger.bind(logger=name)
    return logger
