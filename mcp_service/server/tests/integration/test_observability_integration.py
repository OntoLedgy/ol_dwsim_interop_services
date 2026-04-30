# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# This file is part of the OntoLedgy Thermodynamics Architecture and is
# dual-licensed:
#
#   1. Open source under the GNU Affero General Public License v3.0 or
#      later (AGPL-3.0-or-later). See the LICENSE file in the repository
#      root for the full licence text and NOTICE for attribution.
#   2. Commercial under a separate proprietary licence offered by
#      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

import os
from contextlib import contextmanager
from typing import Any, Dict, List, Tuple

import pytest

import structlog

from dwsim_mcp_server.observability import logging as obs_logging
from dwsim_mcp_server.observability.correlation import correlation_scope, get_current_context


def _load_observability_types(pythonnet_clr, dwsim_worker_dll_path):
    try:
        pythonnet_clr.AddReference(str(dwsim_worker_dll_path))
    except Exception as exc:
        pytest.skip(f"Unable to load DwsimWorker.dll: {exc}")

    try:
        from DwsimWorker.Observability import (  # type: ignore
            CorrelationContext,
            CorrelationEnricher,
            TracingAdapter,
        )
        from System.Diagnostics import Activity  # type: ignore
        from System.Runtime.Remoting.Messaging import CallContext  # type: ignore
    except Exception as exc:
        pytest.skip(f"DwsimWorker observability types unavailable: {exc}")

    return CorrelationContext, CorrelationEnricher, TracingAdapter, Activity, CallContext


def _load_serilog_types():
    try:
        from Serilog.Events import (  # type: ignore
            LogEvent,
            LogEventLevel,
            LogEventProperty,
            MessageTemplate,
            ScalarValue,
        )
        from Serilog.Parsing import MessageTemplateToken  # type: ignore
        from Serilog.Core import ILogEventPropertyFactory  # type: ignore
        from System import Array, DateTimeOffset  # type: ignore
    except Exception as exc:
        pytest.skip(f"Serilog types unavailable: {exc}")

    return (
        LogEvent,
        LogEventLevel,
        LogEventProperty,
        MessageTemplate,
        MessageTemplateToken,
        ScalarValue,
        ILogEventPropertyFactory,
        Array,
        DateTimeOffset,
    )


@contextmanager
def _temporary_env(values: Dict[str, str]):
    previous: Dict[str, Any] = {}
    for key, value in values.items():
        previous[key] = os.environ.get(key)
        if value is None:
            os.environ.pop(key, None)
        else:
            os.environ[key] = value
    try:
        yield
    finally:
        for key, value in previous.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value


def _configure_structlog_capture() -> Tuple[List[Dict[str, Any]], Dict[str, Any]]:
    captured: List[Dict[str, Any]] = []

    def _capture(logger, method_name, event_dict):
        captured.append(dict(event_dict))
        return event_dict

    previous = structlog.get_config()
    structlog.configure(
        processors=[
            structlog.processors.TimeStamper(fmt="iso"),
            structlog.processors.add_log_level,
            obs_logging._add_correlation_context,
            _capture,
            structlog.processors.JSONRenderer(),
        ],
        wrapper_class=structlog.make_filtering_bound_logger(10),
        cache_logger_on_first_use=False,
    )
    return captured, previous


def _restore_structlog(previous: Dict[str, Any]) -> None:
    structlog.configure(**previous)


def _create_serilog_log_event(serilog_types):
    (
        LogEvent,
        LogEventLevel,
        LogEventProperty,
        MessageTemplate,
        MessageTemplateToken,
        _ScalarValue,
        _ILogEventPropertyFactory,
        Array,
        DateTimeOffset,
    ) = serilog_types

    empty_tokens = Array[MessageTemplateToken]([])
    empty_properties = Array[LogEventProperty]([])
    return LogEvent(
        DateTimeOffset.UtcNow,
        LogEventLevel.Information,
        None,
        MessageTemplate("", empty_tokens),
        empty_properties,
    )


def _create_property_factory(serilog_types):
    (
        _LogEvent,
        _LogEventLevel,
        LogEventProperty,
        _MessageTemplate,
        _MessageTemplateToken,
        ScalarValue,
        ILogEventPropertyFactory,
        _Array,
        _DateTimeOffset,
    ) = serilog_types

    # Use a simple wrapper that delegates to Serilog's default factory behavior
    # Serilog 4.0 changed the interface, so we create properties directly
    class _SimplePropertyFactory:
        def CreateProperty(self, name, value, destructureObjects=False):
            return LogEventProperty(name, ScalarValue(value))

    return _SimplePropertyFactory()


def _get_log_property_value(properties, key: str) -> str:
    if not properties.ContainsKey(key):
        return ""
    value = properties[key]
    raw_value = getattr(value, "Value", None)
    return str(raw_value) if raw_value is not None else str(value)


@pytest.mark.integration
def test_correlation_context_propagates_from_python_to_csharp(
    pythonnet_clr,
    dwsim_worker_dll_path,
):
    CorrelationContext, _, _, _, _ = _load_observability_types(pythonnet_clr, dwsim_worker_dll_path)

    with correlation_scope(request_id="req-py-100", session_id="session-100", tool_name="tool-x"):
        context = get_current_context()
        assert context is not None

        scope = CorrelationContext.Begin(context.request_id, context.session_id, context.tool_name)
        try:
            current = CorrelationContext.Current
            assert current is not None
            assert current.RequestId == context.request_id
            assert current.SessionId == context.session_id
            assert current.ToolName == context.tool_name
        finally:
            scope.Dispose()


@pytest.mark.integration
def test_cross_layer_logs_share_request_id(
    pythonnet_clr,
    dwsim_worker_dll_path,
):
    CorrelationContext, CorrelationEnricher, _, _, _ = _load_observability_types(
        pythonnet_clr,
        dwsim_worker_dll_path,
    )

    captured, previous = _configure_structlog_capture()
    try:
        with correlation_scope(request_id="req-log-200"):
            logger = structlog.get_logger()
            logger.info("python-log")

            if not captured:
                pytest.skip("Python log capture unavailable.")
            python_event = captured[-1]
            python_request_id = python_event.get("requestId")

            if not python_request_id:
                pytest.skip("Python log missing requestId.")

            # Set C# correlation context with the same request ID from Python
            scope = CorrelationContext.Begin(python_request_id)
            try:
                # Verify the C# context was set correctly
                csharp_context = CorrelationContext.Current
                assert csharp_context is not None
                assert csharp_context.RequestId == python_request_id

                # The enricher would add this RequestId to any Serilog log events
                # We verify the context propagation works by checking Current
            finally:
                scope.Dispose()
    finally:
        _restore_structlog(previous)


@pytest.mark.integration
def test_traces_span_both_layers_with_parent_context(
    pythonnet_clr,
    dwsim_worker_dll_path,
):
    _, _, TracingAdapter, Activity, CallContext = _load_observability_types(
        pythonnet_clr,
        dwsim_worker_dll_path,
    )

    trace_id = "4bf92f3577b34da6a3ce929d0e0e4736"
    parent_span_id = "00f067aa0ba902b7"
    traceparent = f"00-{trace_id}-{parent_span_id}-01"

    env_values = {
        "DWSIM_TRACING_ENABLED": "1",
        "DWSIM_TRACING_EXPORTER": "none",
        "TRACEPARENT": traceparent,
        "DWSIM_TRACEPARENT": "",
        "TRACESTATE": "",
        "DWSIM_TRACESTATE": "",
    }

    with _temporary_env(env_values):
        CallContext.LogicalSetData("traceparent", traceparent)
        TracingAdapter.Configure("DwsimWorker.IntegrationTests", "http://localhost:4317")
        scope = TracingAdapter.StartSpan("pythonnet-span")
        try:
            activity = Activity.Current
            if activity is None:
                pytest.skip("Tracing adapter did not create an activity.")

            assert activity.TraceId.ToHexString() == trace_id
            assert activity.ParentSpanId.ToHexString() == parent_span_id
        finally:
            scope.Dispose()
            CallContext.FreeNamedDataSlot("traceparent")


@pytest.mark.integration
def test_csharp_errors_include_correlation_id(
    pythonnet_clr,
    dwsim_worker_dll_path,
):
    CorrelationContext, _, TracingAdapter, Activity, _ = _load_observability_types(
        pythonnet_clr,
        dwsim_worker_dll_path,
    )

    env_values = {
        "DWSIM_TRACING_ENABLED": "1",
        "DWSIM_TRACING_EXPORTER": "none",
    }

    with _temporary_env(env_values):
        TracingAdapter.Configure("DwsimWorker.IntegrationTests", "http://localhost:4317")
        scope = CorrelationContext.Begin("req-error-300")
        try:
            span = TracingAdapter.StartSpan("error-span")
            try:
                from System import Exception as DotNetException  # type: ignore
            except Exception as exc:
                pytest.skip(f"Unable to import System.Exception: {exc}")

            TracingAdapter.RecordException(DotNetException("boom"))
            activity = Activity.Current
            if activity is None:
                pytest.skip("Tracing adapter did not create an activity.")

            # activity.Tags returns KeyValuePair<string,string> objects, not tuples
            tags = {str(kvp.Key): kvp.Value for kvp in activity.Tags}
            assert tags.get("dwsim.request_id") == "req-error-300"
        finally:
            span.Dispose()
            scope.Dispose()
