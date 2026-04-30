# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

from __future__ import annotations

import os
import threading
import time
from contextlib import contextmanager
from typing import Optional

from pydantic import BaseModel, Field
from prometheus_client import CollectorRegistry, Counter, Gauge, Histogram, generate_latest


class MetricsSettings(BaseModel):
    enabled: bool = Field(default=True, description="Enable Prometheus metrics collection")
    port: int = Field(default=9090, description="Metrics HTTP port")

    @classmethod
    def from_env(cls) -> "MetricsSettings":
        enabled_raw = os.getenv("DWSIM_METRICS_ENABLED", "true").strip().lower()
        enabled = enabled_raw in {"1", "true", "yes", "on"}
        port_raw = os.getenv("DWSIM_METRICS_PORT", "9090").strip()
        try:
            port = int(port_raw)
        except ValueError:
            port = 9090
        return cls(enabled=enabled, port=port)


class MetricsCollector:
    _instance: Optional["MetricsCollector"] = None
    _lock = threading.Lock()

    def __init__(self, settings: Optional[MetricsSettings] = None) -> None:
        self.settings = settings or MetricsSettings.from_env()
        self.enabled = bool(self.settings.enabled)
        self._registry: Optional[CollectorRegistry] = None

        if not self.enabled:
            self.tool_call_total = None
            self.tool_call_duration_seconds = None
            self.active_sessions = None
            self.process_rss_bytes = None
            self.python_heap_bytes = None
            return

        self._registry = CollectorRegistry()
        self.tool_call_total = Counter(
            "dwsim_tool_call_total",
            "Total MCP tool calls",
            ["tool_name", "status"],
            registry=self._registry,
        )
        self.tool_call_duration_seconds = Histogram(
            "dwsim_tool_call_duration_seconds",
            "Tool call duration",
            ["tool_name"],
            buckets=(0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0),
            registry=self._registry,
        )
        self.active_sessions = Gauge(
            "dwsim_active_sessions",
            "Number of active DWSIM sessions",
            registry=self._registry,
        )
        self.process_rss_bytes = Gauge(
            "dwsim_process_rss_bytes",
            "Process resident set size in bytes",
            registry=self._registry,
        )
        self.python_heap_bytes = Gauge(
            "dwsim_python_heap_bytes",
            "Python heap size in bytes if available",
            registry=self._registry,
        )

    @classmethod
    def get_instance(cls) -> "MetricsCollector":
        if cls._instance is None:
            with cls._lock:
                if cls._instance is None:
                    cls._instance = cls()
        return cls._instance

    def record_tool_call(self, tool_name: str, duration_seconds: float, status: str) -> None:
        if not self.enabled:
            return
        if not tool_name:
            tool_name = "unknown"
        if status not in {"success", "error"}:
            status = "error"
        self.tool_call_total.labels(tool_name=tool_name, status=status).inc()
        self.tool_call_duration_seconds.labels(tool_name=tool_name).observe(duration_seconds)

    def set_active_sessions(self, count: int) -> None:
        if not self.enabled:
            return
        self.active_sessions.set(count)

    def record_memory_usage(self) -> None:
        if not self.enabled:
            return

        rss_bytes = _get_process_rss_bytes()
        if rss_bytes is not None:
            self.process_rss_bytes.set(rss_bytes)

        heap_bytes = _get_python_heap_bytes()
        if heap_bytes is not None:
            self.python_heap_bytes.set(heap_bytes)

    def get_metrics_text(self) -> str:
        if not self.enabled or self._registry is None:
            return ""
        return generate_latest(self._registry).decode("utf-8")


@contextmanager
def measure_duration(tool_name: str):
    start = time.perf_counter()
    status = "success"
    try:
        yield
    except Exception:
        status = "error"
        raise
    finally:
        duration = time.perf_counter() - start
        get_metrics_collector().record_tool_call(tool_name, duration, status)


def get_metrics_collector() -> MetricsCollector:
    return MetricsCollector.get_instance()


def _get_process_rss_bytes() -> Optional[int]:
    try:
        import psutil  # type: ignore

        process = psutil.Process(os.getpid())
        return int(process.memory_info().rss)
    except Exception:
        pass

    try:
        import resource

        rss_kb = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
        if rss_kb <= 0:
            return None
        if os.name == "posix":
            return int(rss_kb * 1024)
        return int(rss_kb)
    except Exception:
        return None


def _get_python_heap_bytes() -> Optional[int]:
    try:
        import tracemalloc

        if not tracemalloc.is_tracing():
            return None
        current, _peak = tracemalloc.get_traced_memory()
        return int(current)
    except Exception:
        return None
