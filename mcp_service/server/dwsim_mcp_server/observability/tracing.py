"""OpenTelemetry tracing utilities for the DWSIM MCP server."""

from __future__ import annotations

from contextlib import contextmanager
from typing import Any, Callable, Iterator, Literal, Optional, TypeVar, cast
import functools
import inspect
import json

from opentelemetry import trace
from opentelemetry.sdk.resources import Resource, SERVICE_NAME
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor, ConsoleSpanExporter
from opentelemetry.sdk.trace.sampling import ParentBased, TraceIdRatioBased
from opentelemetry.trace import Span, Status, StatusCode, Tracer
from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

from dwsim_mcp_server.observability.correlation import get_current_context

ExporterType = Literal["jaeger", "zipkin", "otlp", "console", "none"]

_TRACING_ENV_ENABLED = "DWSIM_TRACING_ENABLED"
_TRACING_ENV_EXPORTER = "DWSIM_TRACING_EXPORTER"
_TRACING_ENV_ENDPOINT = "DWSIM_TRACING_ENDPOINT"
_TRACING_ENV_SAMPLE_RATE = "DWSIM_TRACING_SAMPLE_RATE"

_DEFAULT_SERVICE_NAME = "dwsim-mcp-server"
_MAX_ATTR_LENGTH = 512
_MAX_PARAM_ATTRIBUTES = 20

_tracing_configured = False
_tracing_enabled = False


class TracingSettings(BaseSettings):
    """Settings sourced from environment variables for tracing."""

    model_config = SettingsConfigDict(env_prefix="", case_sensitive=False)

    enabled: bool = Field(
        False,
        validation_alias=_TRACING_ENV_ENABLED,
        description="Enable OpenTelemetry tracing.",
    )
    exporter: ExporterType = Field(
        "none",
        validation_alias=_TRACING_ENV_EXPORTER,
        description="Tracing exporter type.",
    )
    endpoint: Optional[str] = Field(
        None,
        validation_alias=_TRACING_ENV_ENDPOINT,
        description="Exporter endpoint URL.",
    )
    sample_rate: float = Field(
        1.0,
        validation_alias=_TRACING_ENV_SAMPLE_RATE,
        description="Sampling rate from 0.0 to 1.0.",
    )


def configure_tracing(
    service_name: str = _DEFAULT_SERVICE_NAME,
    exporter: ExporterType = "none",
    endpoint: Optional[str] = None,
    sample_rate: float = 1.0,
) -> None:
    """Configure OpenTelemetry tracing for the server."""
    global _tracing_configured, _tracing_enabled

    settings = TracingSettings()
    use_env_defaults = (
        service_name == _DEFAULT_SERVICE_NAME
        and exporter == "none"
        and endpoint is None
        and sample_rate == 1.0
    )

    if use_env_defaults and settings.enabled:
        exporter = settings.exporter
        endpoint = settings.endpoint
        sample_rate = settings.sample_rate

    if not settings.enabled and exporter == "none":
        trace.set_tracer_provider(trace.NoOpTracerProvider())
        _tracing_configured = True
        _tracing_enabled = False
        return

    sample_rate = max(0.0, min(1.0, sample_rate))
    sampler = ParentBased(TraceIdRatioBased(sample_rate))

    resource = Resource.create({SERVICE_NAME: service_name})
    provider = TracerProvider(resource=resource, sampler=sampler)

    span_exporter = _build_exporter(exporter, endpoint)
    if span_exporter is not None:
        provider.add_span_processor(BatchSpanProcessor(span_exporter))

    trace.set_tracer_provider(provider)
    _tracing_configured = True
    _tracing_enabled = exporter != "none"


def get_tracer() -> Tracer:
    """Return the configured tracer or a no-op tracer if disabled."""
    if not _tracing_configured:
        configure_tracing()
    if not _tracing_enabled:
        return trace.NoOpTracerProvider().get_tracer(__name__)
    return trace.get_tracer(__name__)


@contextmanager
def traced_operation(name: str, attributes: Optional[dict[str, Any]] = None) -> Iterator[Span]:
    """Create a traced span around the wrapped operation."""
    tracer = get_tracer()
    with tracer.start_as_current_span(name) as span:
        _apply_correlation_attributes(span)
        if attributes:
            _set_span_attributes(span, attributes)
        try:
            yield span
            span.set_status(Status(StatusCode.OK))
        except Exception as exc:
            _record_exception(span, exc)
            raise


F = TypeVar("F", bound=Callable[..., Any])


def trace_tool(func: F) -> F:
    """Decorator that traces MCP tool invocations."""
    if inspect.iscoroutinefunction(func):

        @functools.wraps(func)
        async def async_wrapper(*args: Any, **kwargs: Any) -> Any:
            tracer = get_tracer()
            with tracer.start_as_current_span(func.__name__) as span:
                _apply_correlation_attributes(span)
                _set_tool_attributes(span, func, args, kwargs)
                try:
                    result = await func(*args, **kwargs)
                    span.set_status(Status(StatusCode.OK))
                    return result
                except Exception as exc:
                    _record_exception(span, exc)
                    raise

        return cast(F, async_wrapper)

    @functools.wraps(func)
    def sync_wrapper(*args: Any, **kwargs: Any) -> Any:
        tracer = get_tracer()
        with tracer.start_as_current_span(func.__name__) as span:
            _apply_correlation_attributes(span)
            _set_tool_attributes(span, func, args, kwargs)
            try:
                result = func(*args, **kwargs)
                span.set_status(Status(StatusCode.OK))
                return result
            except Exception as exc:
                _record_exception(span, exc)
                raise

    return cast(F, sync_wrapper)


def _build_exporter(exporter: ExporterType, endpoint: Optional[str]) -> Any:
    if exporter == "none":
        return None
    if exporter == "console":
        return ConsoleSpanExporter()
    if exporter == "jaeger":
        from opentelemetry.exporter.jaeger.thrift import JaegerExporter

        return JaegerExporter(endpoint=endpoint)
    if exporter == "zipkin":
        from opentelemetry.exporter.zipkin.proto.http import ZipkinExporter

        return ZipkinExporter(endpoint=endpoint)
    if exporter == "otlp":
        from opentelemetry.exporter.otlp.proto.http.trace_exporter import OTLPSpanExporter

        return OTLPSpanExporter(endpoint=endpoint) if endpoint else OTLPSpanExporter()
    raise ValueError(f"Unsupported exporter: {exporter}")


def _apply_correlation_attributes(span: Span) -> None:
    context = get_current_context()
    if context is None:
        return
    span.set_attribute("correlation.request_id", context.request_id)
    if context.session_id is not None:
        span.set_attribute("correlation.session_id", context.session_id)
    if context.tool_name is not None:
        span.set_attribute("correlation.tool_name", context.tool_name)
    span.set_attribute("correlation.start_time", context.start_time.isoformat())


def _set_tool_attributes(
    span: Span, func: Callable[..., Any], args: tuple[Any, ...], kwargs: dict[str, Any]
) -> None:
    span.set_attribute("tool.name", func.__name__)
    if func.__qualname__ != func.__name__:
        span.set_attribute("tool.qualified_name", func.__qualname__)

    try:
        signature = inspect.signature(func)
        bound = signature.bind_partial(*args, **kwargs)
        parameters = bound.arguments
    except (TypeError, ValueError):
        parameters = {}

    count = 0
    for key, value in parameters.items():
        if key in {"self", "cls"}:
            continue
        if count >= _MAX_PARAM_ATTRIBUTES:
            span.set_attribute("tool.params.truncated", True)
            break
        attr_key = f"tool.param.{key}"
        _set_span_attribute(span, attr_key, value)
        count += 1


def _set_span_attributes(span: Span, attributes: dict[str, Any]) -> None:
    for key, value in attributes.items():
        _set_span_attribute(span, str(key), value)


def _set_span_attribute(span: Span, key: str, value: Any) -> None:
    if _is_sensitive_key(key):
        span.set_attribute(key, "[REDACTED]")
        return
    span.set_attribute(key, _coerce_attribute_value(value))


def _coerce_attribute_value(value: Any) -> Any:
    if value is None:
        return "null"
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return value
    if isinstance(value, str):
        return _truncate(value)
    if isinstance(value, bytes):
        return _truncate(value.decode(errors="replace"))
    if isinstance(value, (list, tuple, set)):
        return _truncate(_safe_json(value))
    if isinstance(value, dict):
        return _truncate(_safe_json(value))
    return _truncate(str(value))


def _safe_json(value: Any) -> str:
    try:
        return json.dumps(value, default=str)
    except (TypeError, ValueError):
        return str(value)


def _truncate(value: str) -> str:
    if len(value) <= _MAX_ATTR_LENGTH:
        return value
    return f"{value[: _MAX_ATTR_LENGTH - 3]}..."


def _is_sensitive_key(key: str) -> bool:
    lowered = key.lower()
    sensitive_tokens = (
        "password",
        "secret",
        "token",
        "apikey",
        "api_key",
        "credential",
        "authorization",
        "auth",
        "cookie",
        "bearer",
        "jwt",
    )
    return any(token in lowered for token in sensitive_tokens)


def _record_exception(span: Span, exc: Exception) -> None:
    if span.is_recording():
        span.record_exception(exc)
        span.set_status(Status(StatusCode.ERROR, str(exc)))
