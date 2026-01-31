# Observability Guide

This guide covers the observability infrastructure for the DWSIM MCP Server, including logging, distributed tracing, metrics, and diagnostics.

## Overview

The observability system provides:

- **Structured Logging** with correlation IDs across Python and C# layers
- **Distributed Tracing** via OpenTelemetry for end-to-end request visibility
- **Prometheus Metrics** for operational monitoring
- **Diagnostics Service** for server and session health inspection

## Configuration

All observability settings are configured via environment variables with the `DWSIM_` prefix.

### Environment Variables Reference

| Variable | Default | Description |
|----------|---------|-------------|
| `DWSIM_LOG_LEVEL` | `INFO` | Logging level (DEBUG, INFO, WARNING, ERROR) |
| `DWSIM_LOG_FILE` | (none) | Path for rotating log file output |
| `DWSIM_LOG_FILE_MAX_BYTES` | `10485760` | Max file size before rotation (10MB) |
| `DWSIM_LOG_FILE_BACKUP_COUNT` | `5` | Number of rotated files to keep |
| `DWSIM_SEQ_URL` | (none) | Seq server URL for log aggregation |
| `DWSIM_SEQ_API_KEY` | (none) | Optional Seq API key |
| `DWSIM_TRACING_ENABLED` | `false` | Enable OpenTelemetry tracing |
| `DWSIM_TRACING_EXPORTER` | `none` | Exporter type: jaeger, zipkin, otlp, console, none |
| `DWSIM_TRACING_ENDPOINT` | (none) | Exporter endpoint URL |
| `DWSIM_TRACING_SAMPLE_RATE` | `1.0` | Sampling rate (0.0 to 1.0) |
| `DWSIM_METRICS_ENABLED` | `true` | Enable Prometheus metrics collection |
| `DWSIM_METRICS_PORT` | `9090` | HTTP port for metrics endpoint |

## Structured Logging

### Correlation Context

Every request is assigned a unique `requestId` that propagates through both Python and C# layers, enabling end-to-end log correlation.

Log fields automatically added:
- `requestId` - Unique request identifier (UUID)
- `sessionId` - DWSIM session identifier (when available)
- `toolName` - MCP tool being executed (when available)
- `timestamp` - ISO 8601 timestamp

### Example Log Output

```json
{
  "timestamp": "2026-01-28T14:30:00.123Z",
  "level": "info",
  "event": "tool_call_complete",
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "sessionId": "session-abc123",
  "toolName": "create_session",
  "duration_ms": 150
}
```

### File Logging

Enable rotating file logs:

```bash
export DWSIM_LOG_FILE="/var/log/dwsim/mcp-server.log"
export DWSIM_LOG_FILE_MAX_BYTES=52428800  # 50MB
export DWSIM_LOG_FILE_BACKUP_COUNT=10
```

### Seq Integration

For centralized log aggregation with [Seq](https://datalust.co/seq):

```bash
export DWSIM_SEQ_URL="http://localhost:5341"
export DWSIM_SEQ_API_KEY="your-api-key"  # Optional
```

Logs are shipped asynchronously in CLEF (Compact Log Event Format) with automatic buffering and retry on network failures.

## Distributed Tracing

### Enabling Tracing

```bash
export DWSIM_TRACING_ENABLED=true
export DWSIM_TRACING_EXPORTER=otlp
export DWSIM_TRACING_ENDPOINT=http://localhost:4317
export DWSIM_TRACING_SAMPLE_RATE=1.0
```

### Supported Exporters

#### OTLP (OpenTelemetry Protocol)

```bash
export DWSIM_TRACING_EXPORTER=otlp
export DWSIM_TRACING_ENDPOINT=http://localhost:4317
```

Works with Jaeger, Tempo, and other OTLP-compatible backends.

#### Jaeger

```bash
export DWSIM_TRACING_EXPORTER=jaeger
export DWSIM_TRACING_ENDPOINT=http://localhost:6831
```

#### Zipkin

```bash
export DWSIM_TRACING_EXPORTER=zipkin
export DWSIM_TRACING_ENDPOINT=http://localhost:9411/api/v2/spans
```

#### Console (Development)

```bash
export DWSIM_TRACING_EXPORTER=console
```

Outputs traces to stdout for local debugging.

### Span Attributes

Spans automatically include:
- `correlation.request_id` - Request correlation ID
- `correlation.session_id` - Session identifier
- `correlation.tool_name` - MCP tool name
- `tool.param.*` - Tool parameters (sanitized)

### Cross-Layer Tracing

The tracing system propagates W3C Trace Context between Python and C# layers, enabling unified trace views across the entire request path.

## Prometheus Metrics

### Endpoints

When enabled, the server exposes:

- `GET /metrics` - Prometheus metrics in text format
- `GET /health` - Health check endpoint

### Available Metrics

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `dwsim_tool_call_total` | Counter | tool_name, status | Total MCP tool invocations |
| `dwsim_tool_call_duration_seconds` | Histogram | tool_name | Tool execution duration |
| `dwsim_active_sessions` | Gauge | | Current active session count |
| `dwsim_process_rss_bytes` | Gauge | | Process resident memory |
| `dwsim_python_heap_bytes` | Gauge | | Python heap memory |

### Prometheus Configuration

Add to your `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'dwsim-mcp'
    static_configs:
      - targets: ['localhost:9090']
    scrape_interval: 15s
```

### Grafana Dashboard

Example queries:

```promql
# Tool call rate by status
rate(dwsim_tool_call_total[5m])

# 95th percentile latency
histogram_quantile(0.95, rate(dwsim_tool_call_duration_seconds_bucket[5m]))

# Error rate
sum(rate(dwsim_tool_call_total{status="error"}[5m])) / sum(rate(dwsim_tool_call_total[5m]))
```

## Diagnostics Service

The `get_diagnostics` MCP tool provides runtime inspection capabilities.

### Server Diagnostics

Call without parameters to get server-wide diagnostics:

```json
{
  "uptime_seconds": 3600.5,
  "active_sessions": 3,
  "memory": {
    "rss_bytes": 104857600,
    "limit_bytes": 536870912,
    "recovery_threshold_bytes": 429496729,
    "breached": false
  },
  "error_count": 2,
  "metrics_text": "# HELP dwsim_tool_call_total..."
}
```

### Session Diagnostics

Provide a `session_id` to get session-specific diagnostics:

```json
{
  "session_id": "session-abc123",
  "state": "active",
  "remaining_lifetime_seconds": 1800,
  "memory": {
    "rss_bytes": 104857600,
    "limit_bytes": 536870912,
    "recovery_threshold_bytes": 429496729,
    "breached": false
  }
}
```

## Troubleshooting

### Logs Not Appearing in Seq

1. Verify `DWSIM_SEQ_URL` is set correctly
2. Check network connectivity to Seq server
3. Look for "Seq sink unavailable" warnings in console output
4. Ensure the API key has write permissions (if using authentication)

### Traces Not Appearing

1. Verify `DWSIM_TRACING_ENABLED=true`
2. Check the exporter endpoint is reachable
3. Verify the exporter type matches your backend
4. Check sample rate is not set to 0

### Metrics Endpoint Not Responding

1. Verify `DWSIM_METRICS_ENABLED=true`
2. Check the port is not in use: `netstat -an | grep 9090`
3. Look for "metrics_server_start_failed" in logs
4. Try a different port via `DWSIM_METRICS_PORT`

### Correlation IDs Missing in C# Logs

1. Verify `CorrelationEnricher` is added to Serilog configuration
2. Check that correlation context is being set before C# operations
3. Ensure `CorrelationContext.Begin()` is called with valid parameters

## Development Setup

### Local Jaeger Instance

```bash
docker run -d --name jaeger \
  -p 6831:6831/udp \
  -p 16686:16686 \
  -p 4317:4317 \
  jaegertracing/all-in-one:latest
```

Access UI at http://localhost:16686

### Local Seq Instance

```bash
docker run -d --name seq \
  -p 5341:80 \
  -e ACCEPT_EULA=Y \
  datalust/seq:latest
```

Access UI at http://localhost:5341

### Full Development Configuration

```bash
# Logging
export DWSIM_LOG_LEVEL=DEBUG
export DWSIM_LOG_FILE=./logs/dwsim-mcp.log
export DWSIM_SEQ_URL=http://localhost:5341

# Tracing
export DWSIM_TRACING_ENABLED=true
export DWSIM_TRACING_EXPORTER=otlp
export DWSIM_TRACING_ENDPOINT=http://localhost:4317
export DWSIM_TRACING_SAMPLE_RATE=1.0

# Metrics
export DWSIM_METRICS_ENABLED=true
export DWSIM_METRICS_PORT=9090
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    MCP Client (LLM)                         │
└─────────────────────────┬───────────────────────────────────┘
                          │ MCP Protocol
┌─────────────────────────▼───────────────────────────────────┐
│                  Python MCP Server                          │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Observability Layer                                   │  │
│  │  • CorrelationContext (contextvars)                  │  │
│  │  • structlog + correlation processor                 │  │
│  │  • OpenTelemetry tracer                              │  │
│  │  • Prometheus MetricsCollector                       │  │
│  │  • DiagnosticsService                                │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────┬───────────────────────────────────┘
                          │ Named Pipes
┌─────────────────────────▼───────────────────────────────────┐
│                   C# DWSIM Worker                           │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Observability Layer                                   │  │
│  │  • CorrelationContext (AsyncLocal<T>)                │  │
│  │  • Serilog + CorrelationEnricher                     │  │
│  │  • TracingAdapter (OpenTelemetry)                    │  │
│  │  • DiagnosticsCollector                              │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                          │
        ┌─────────────────┼─────────────────┐
        ▼                 ▼                 ▼
   ┌─────────┐      ┌─────────┐      ┌─────────────┐
   │  Seq    │      │ Jaeger/ │      │ Prometheus  │
   │ (Logs)  │      │ Tempo   │      │ (Metrics)   │
   └─────────┘      └─────────┘      └─────────────┘
```

## Security Considerations

- Sensitive parameters (passwords, tokens, API keys) are automatically redacted from trace spans
- Stack traces in diagnostic bundles are truncated to prevent information leakage
- Seq API keys should be stored in secure environment variables, not in code
- Metrics endpoints should be protected in production (firewall or authentication proxy)
