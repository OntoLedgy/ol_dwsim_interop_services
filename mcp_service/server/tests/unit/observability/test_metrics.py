import pytest

from dwsim_mcp_server.observability import metrics
from dwsim_mcp_server.observability.metrics import MetricsCollector, MetricsSettings, measure_duration


@pytest.fixture(autouse=True)
def reset_metrics_collector() -> None:
    metrics.MetricsCollector._instance = None
    yield
    metrics.MetricsCollector._instance = None


def _sample_value(metric, name: str, labels: dict) -> float:
    for sample in metric.collect()[0].samples:
        if sample.name == name and sample.labels == labels:
            return float(sample.value)
    raise AssertionError(f"Missing sample {name} with labels {labels}")


def test_metrics_collector_singleton_returns_same_instance() -> None:
    first = MetricsCollector.get_instance()
    second = MetricsCollector.get_instance()

    assert first is second


def test_record_tool_call_increments_counter() -> None:
    collector = MetricsCollector(MetricsSettings(enabled=True))

    collector.record_tool_call("tool-a", 0.05, "success")

    value = _sample_value(
        collector.tool_call_total,
        "dwsim_tool_call_total",
        {"tool_name": "tool-a", "status": "success"},
    )
    assert value == 1.0


def test_record_tool_call_observes_histogram() -> None:
    collector = MetricsCollector(MetricsSettings(enabled=True))
    duration = 0.125

    collector.record_tool_call("tool-b", duration, "success")

    count = _sample_value(
        collector.tool_call_duration_seconds,
        "dwsim_tool_call_duration_seconds_count",
        {"tool_name": "tool-b"},
    )
    total = _sample_value(
        collector.tool_call_duration_seconds,
        "dwsim_tool_call_duration_seconds_sum",
        {"tool_name": "tool-b"},
    )

    assert count == 1.0
    assert total == pytest.approx(duration, rel=1e-6)


def test_set_active_sessions_updates_gauge() -> None:
    collector = MetricsCollector(MetricsSettings(enabled=True))

    collector.set_active_sessions(3)

    value = _sample_value(collector.active_sessions, "dwsim_active_sessions", {})
    assert value == 3.0


def test_record_memory_usage_updates_gauges(monkeypatch: pytest.MonkeyPatch) -> None:
    collector = MetricsCollector(MetricsSettings(enabled=True))
    monkeypatch.setattr(metrics, "_get_process_rss_bytes", lambda: 12345)
    monkeypatch.setattr(metrics, "_get_python_heap_bytes", lambda: 67890)

    collector.record_memory_usage()

    rss_value = _sample_value(collector.process_rss_bytes, "dwsim_process_rss_bytes", {})
    heap_value = _sample_value(collector.python_heap_bytes, "dwsim_python_heap_bytes", {})
    assert rss_value == 12345.0
    assert heap_value == 67890.0


def test_get_metrics_text_returns_prometheus_format() -> None:
    collector = MetricsCollector(MetricsSettings(enabled=True))
    collector.record_tool_call("tool-c", 0.01, "success")

    text = collector.get_metrics_text()

    assert "# HELP dwsim_tool_call_total" in text
    assert "# TYPE dwsim_tool_call_total counter" in text
    assert 'tool_name="tool-c"' in text


def test_measure_duration_records_success(monkeypatch: pytest.MonkeyPatch) -> None:
    collector = MetricsCollector(MetricsSettings(enabled=True))
    monkeypatch.setattr(metrics, "get_metrics_collector", lambda: collector)

    with measure_duration("tool-d"):
        pass

    value = _sample_value(
        collector.tool_call_total,
        "dwsim_tool_call_total",
        {"tool_name": "tool-d", "status": "success"},
    )
    count = _sample_value(
        collector.tool_call_duration_seconds,
        "dwsim_tool_call_duration_seconds_count",
        {"tool_name": "tool-d"},
    )
    assert value == 1.0
    assert count == 1.0


def test_measure_duration_records_error(monkeypatch: pytest.MonkeyPatch) -> None:
    collector = MetricsCollector(MetricsSettings(enabled=True))
    monkeypatch.setattr(metrics, "get_metrics_collector", lambda: collector)

    with pytest.raises(RuntimeError):
        with measure_duration("tool-e"):
            raise RuntimeError("boom")

    value = _sample_value(
        collector.tool_call_total,
        "dwsim_tool_call_total",
        {"tool_name": "tool-e", "status": "error"},
    )
    assert value == 1.0


def test_disabled_metrics_do_not_error(monkeypatch: pytest.MonkeyPatch) -> None:
    collector = MetricsCollector(MetricsSettings(enabled=False))

    collector.record_tool_call("tool-f", 0.02, "success")
    collector.set_active_sessions(2)
    collector.record_memory_usage()

    assert collector.tool_call_total is None
    assert collector.tool_call_duration_seconds is None
    assert collector.active_sessions is None
    assert collector.process_rss_bytes is None
    assert collector.python_heap_bytes is None
    assert collector.get_metrics_text() == ""

    monkeypatch.setattr(metrics, "get_metrics_collector", lambda: collector)
    with measure_duration("tool-f"):
        pass
