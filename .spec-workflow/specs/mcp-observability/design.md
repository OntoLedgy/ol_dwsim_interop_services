# Design Document: MCP Observability and Debugging Tools

## Overview

This design implements comprehensive observability infrastructure for the DWSIM MCP Server, providing structured logging with correlation IDs, OpenTelemetry distributed tracing, metrics collection, and a diagnostic MCP tool. The implementation enhances the existing Serilog (C#) and structlog (Python) foundations to enable full request traceability across the Python MCP server and C# DwsimWorker layers.

The observability stack follows a layered approach:
1. **Correlation Context** - Propagates requestId/sessionId across all layers
2. **Structured Logging** - Enhanced logging with consistent fields in both Python and C#
3. **Distributed Tracing** - OpenTelemetry spans from MCP tool to DWSIM engine
4. **Metrics Collection** - Counters, histograms, and gauges for operational visibility
5. **Diagnostics Tool** - MCP-accessible system and session diagnostics

## Steering Document Alignment

### Technical Standards (tech.md)

The design follows documented technical patterns:

- **Serilog** (C#): Extends existing Program.cs configuration with enrichers for correlation IDs
- **structlog** (Python): Extends existing observability/logging.py with context binding
- **OpenTelemetry**: Uses opentelemetry-api and opentelemetry-sdk as specified in tech.md
- **Pydantic**: Diagnostic models use Pydantic for validation and serialization
- **Dependency Injection**: Observability components injected via existing patterns
- **CAPE-OPEN Domain Model**: Diagnostics expose flowsheet object counts using CAPE-OPEN vocabulary

### Project Structure (structure.md)

Implementation follows project organization:

```
mcp_service/
├── server/
│   └── dwsim_mcp_server/
│       ├── observability/           # Existing - extend
│       │   ├── logging.py           # Existing - enhance
│       │   ├── tracing.py           # NEW - OpenTelemetry setup
│       │   ├── metrics.py           # NEW - Prometheus metrics
│       │   └── correlation.py       # NEW - Context propagation
│       ├── tools/
│       │   └── diagnostics.py       # NEW - get_diagnostics tool
│       └── service/
│           └── diagnostics_service.py  # NEW - Diagnostics logic
├── dwsim_worker/
│   └── DwsimWorker/
│       ├── Observability/           # NEW - C# observability module
│       │   ├── CorrelationContext.cs
│       │   ├── TracingAdapter.cs
│       │   └── DiagnosticsCollector.cs
│       └── Program.cs               # Existing - enhance Serilog config
```

## Code Reuse Analysis

### Existing Components to Leverage

- **observability/logging.py**: Existing structlog configuration with JSON/console rendering - extend with correlation context
- **Program.cs Serilog setup**: Existing logger configuration - add correlation enrichers
- **FlowsheetContext.cs logging**: 117 existing log calls - add correlation IDs to all
- **resource_limit_guard.py**: Existing error models - reuse for diagnostic bundle structure
- **memory_monitor.py**: Existing memory tracking - expose via metrics and diagnostics
- **DwsimException hierarchy**: Existing typed exceptions - enhance with diagnostic context

### Integration Points

- **FlowsheetService**: Add tracing spans around all operations
- **SessionManager**: Track session metrics, expose via diagnostics
- **pythonnet bridge**: Propagate trace context via environment/call metadata
- **MCP tool handlers**: Wrap with tracing and metrics decorators

## Architecture

The observability architecture follows a cross-cutting concerns pattern with context propagation:

```mermaid
graph TD
    subgraph "MCP Client (LLM Agent)"
        A[Tool Call]
    end

    subgraph "Python MCP Server"
        B[Tool Handler]
        C[Correlation Middleware]
        D[Tracing Middleware]
        E[Metrics Collector]
        F[FlowsheetService]
    end

    subgraph "C# DwsimWorker"
        G[CorrelationContext]
        H[TracingAdapter]
        I[SessionManager]
        J[FlowsheetContext]
        K[DWSIM Engine]
    end

    subgraph "Observability Backends"
        L[Seq / Elasticsearch]
        M[Jaeger / Zipkin]
        N[Prometheus]
    end

    A -->|MCP Request| B
    B --> C
    C -->|Generate requestId| D
    D -->|Create root span| E
    E --> F
    F -->|pythonnet + context| G
    G -->|Propagate correlation| H
    H -->|Create child spans| I
    I --> J
    J --> K

    C -.->|Structured logs| L
    G -.->|Structured logs| L
    D -.->|Trace spans| M
    H -.->|Trace spans| M
    E -.->|Metrics| N
</thinking>

### Modular Design Principles

- **Single File Responsibility**: Each observability concern (logging, tracing, metrics) in separate module
- **Component Isolation**: Correlation context independent of tracing implementation
- **Service Layer Separation**: DiagnosticsService separate from MCP tool handler
- **Utility Modularity**: Shared correlation ID generation in dedicated module

## Components and Interfaces

### Component 1: Correlation Context (Python)

**File:** `mcp_service/server/dwsim_mcp_server/observability/correlation.py`

- **Purpose:** Generate and propagate correlation IDs (requestId, sessionId) through Python layer
- **Interfaces:**
  ```python
  class CorrelationContext:
      request_id: str
      session_id: Optional[str]
      tool_name: str
      start_time: datetime

  def generate_request_id() -> str
  def get_current_context() -> Optional[CorrelationContext]
  def set_current_context(ctx: CorrelationContext) -> None

  @contextmanager
  def correlation_scope(tool_name: str, session_id: Optional[str] = None) -> CorrelationContext
  ```
- **Dependencies:** contextvars (stdlib), uuid (stdlib)
- **Reuses:** None (new foundational component)

### Component 2: Correlation Context (C#)

**File:** `mcp_service/dwsim_worker/DwsimWorker/Observability/CorrelationContext.cs`

- **Purpose:** Receive and propagate correlation IDs through C# layer, enrich Serilog logs
- **Interfaces:**
  ```csharp
  public class CorrelationContext : IDisposable
  {
      public string RequestId { get; }
      public string? SessionId { get; }
      public string ToolName { get; }

      public static CorrelationContext Current { get; }
      public static CorrelationContext Begin(string requestId, string? sessionId, string toolName);
  }

  public class CorrelationEnricher : ILogEventEnricher
  {
      public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory);
  }
  ```
- **Dependencies:** Serilog, AsyncLocal<T>
- **Reuses:** Existing Serilog configuration in Program.cs

### Component 3: Tracing Setup (Python)

**File:** `mcp_service/server/dwsim_mcp_server/observability/tracing.py`

- **Purpose:** Configure OpenTelemetry tracing with exporters and provide span creation utilities
- **Interfaces:**
  ```python
  def configure_tracing(
      service_name: str = "dwsim-mcp-server",
      exporter: Literal["jaeger", "zipkin", "otlp", "console", "none"] = "none",
      endpoint: Optional[str] = None,
      sample_rate: float = 1.0
  ) -> None

  def get_tracer() -> Tracer

  @contextmanager
  def traced_operation(name: str, attributes: dict = None) -> Span

  def trace_tool(func: Callable) -> Callable  # Decorator for MCP tools
  ```
- **Dependencies:** opentelemetry-api, opentelemetry-sdk, opentelemetry-exporter-jaeger
- **Reuses:** CorrelationContext for span attributes

### Component 4: Tracing Adapter (C#)

**File:** `mcp_service/dwsim_worker/DwsimWorker/Observability/TracingAdapter.cs`

- **Purpose:** Create OpenTelemetry spans in C# layer, propagate trace context from Python
- **Interfaces:**
  ```csharp
  public static class TracingAdapter
  {
      public static void Configure(string serviceName, string exporterEndpoint);
      public static IDisposable StartSpan(string operationName, IDictionary<string, object> attributes = null);
      public static void SetSpanAttribute(string key, object value);
      public static void RecordException(Exception ex);
  }
  ```
- **Dependencies:** OpenTelemetry.Api, OpenTelemetry.Exporter.Jaeger (NuGet)
- **Reuses:** CorrelationContext for trace parent extraction

### Component 5: Metrics Collector (Python)

**File:** `mcp_service/server/dwsim_mcp_server/observability/metrics.py`

- **Purpose:** Collect and expose Prometheus metrics for tool calls, sessions, and performance
- **Interfaces:**
  ```python
  class MetricsCollector:
      def record_tool_call(self, tool_name: str, duration_seconds: float, success: bool) -> None
      def set_active_sessions(self, count: int) -> None
      def record_memory_usage(self, session_id: str, bytes: int) -> None
      def get_metrics_text() -> str  # Prometheus exposition format

  # Pre-defined metrics
  tool_call_total: Counter  # Labels: tool_name, status
  tool_call_duration_seconds: Histogram  # Labels: tool_name
  active_sessions: Gauge
  session_memory_bytes: Gauge  # Labels: session_id
  ```
- **Dependencies:** prometheus_client
- **Reuses:** None (new component)

### Component 6: Diagnostics Service (Python)

**File:** `mcp_service/server/dwsim_mcp_server/service/diagnostics_service.py`

- **Purpose:** Collect and format diagnostic information from Python and C# layers
- **Interfaces:**
  ```python
  class DiagnosticsService:
      def __init__(self, flowsheet_service: FlowsheetService, metrics: MetricsCollector)

      async def get_server_diagnostics(self) -> ServerDiagnostics
      async def get_session_diagnostics(self, session_id: str) -> SessionDiagnostics
      def record_error(self, session_id: Optional[str], error: Exception) -> str  # Returns bundle_id
      def get_diagnostic_bundle(self, bundle_id: str) -> Optional[DiagnosticBundle]
  ```
- **Dependencies:** FlowsheetService, MetricsCollector, MemoryMonitor
- **Reuses:** Existing memory_monitor.py, resource_limit_guard.py error models

### Component 7: Diagnostics Collector (C#)

**File:** `mcp_service/dwsim_worker/DwsimWorker/Observability/DiagnosticsCollector.cs`

- **Purpose:** Collect C#-side diagnostics: session state, flowsheet details, error history
- **Interfaces:**
  ```csharp
  public class DiagnosticsCollector
  {
      public SessionDiagnosticsDto GetSessionDiagnostics(string sessionId);
      public FlowsheetDiagnosticsDto GetFlowsheetDiagnostics(FlowsheetContext context);
      public void RecordError(string sessionId, Exception ex);
      public IReadOnlyList<ErrorRecord> GetRecentErrors(string sessionId, int count = 5);
  }

  public record SessionDiagnosticsDto(
      string SessionId,
      string State,
      int ObjectCount,
      int StreamCount,
      int UnitCount,
      long MemoryBytes,
      DateTime CreatedAt,
      DateTime? LastActivityAt
  );
  ```
- **Dependencies:** SessionManager, FlowsheetContext
- **Reuses:** Existing session and flowsheet state

### Component 8: MCP Diagnostics Tool

**File:** `mcp_service/server/dwsim_mcp_server/tools/diagnostics.py`

- **Purpose:** Expose get_diagnostics MCP tool for LLM agents
- **Interfaces:**
  ```python
  @mcp_tool(name="get_diagnostics")
  async def get_diagnostics(session_id: Optional[str] = None) -> DiagnosticsResult:
      """
      Get system or session diagnostics for troubleshooting.

      Args:
          session_id: Optional session ID. If provided, returns session-specific diagnostics.
                     If omitted, returns server-wide diagnostics.

      Returns:
          Diagnostic information including status, metrics, and recent errors.
      """
  ```
- **Dependencies:** DiagnosticsService
- **Reuses:** Existing MCP tool registration pattern from tools/*.py

## Data Models

### CorrelationContext (Python)

```python
from pydantic import BaseModel
from datetime import datetime
from typing import Optional

class CorrelationContext(BaseModel):
    request_id: str          # UUID, generated per MCP tool call
    session_id: Optional[str] = None  # DWSIM session ID if applicable
    tool_name: str           # MCP tool name (e.g., "run", "add_stream")
    start_time: datetime     # Request start timestamp (UTC)

    class Config:
        frozen = True
```

### ServerDiagnostics (Python)

```python
class ServerDiagnostics(BaseModel):
    server_version: str
    uptime_seconds: float
    active_session_count: int
    total_tool_calls: int
    recent_error_count: int          # Errors in last 5 minutes
    memory_usage_bytes: int
    memory_limit_bytes: int
    tracing_enabled: bool
    metrics_enabled: bool

class SessionDiagnostics(BaseModel):
    session_id: str
    state: Literal["active", "closed", "error"]
    created_at: datetime
    last_activity_at: Optional[datetime]
    object_count: int
    stream_count: int
    unit_count: int
    compound_count: int
    calculation_status: Optional[str]
    memory_usage_bytes: Optional[int]
    recent_errors: list[ErrorSummary]
    diagnostic_bundle_id: Optional[str]  # If errors occurred

class ErrorSummary(BaseModel):
    timestamp: datetime
    error_code: str
    message: str
    stack_trace_summary: Optional[str]  # First 3 lines only
```

### DiagnosticBundle (Python)

```python
class DiagnosticBundle(BaseModel):
    bundle_id: str
    created_at: datetime
    session_id: Optional[str]
    request_id: str
    tool_name: str

    # Error details
    error_type: str
    error_message: str
    full_stack_trace: str

    # Context
    request_parameters: dict
    session_state_snapshot: Optional[dict]
    recent_log_entries: list[str]  # Last 50 logs

    # Flowsheet state (if available)
    flowsheet_object_graph: Optional[dict]
    input_stream_properties: Optional[dict]
    solver_parameters: Optional[dict]
```

### C# DTOs

```csharp
public record SessionDiagnosticsDto(
    string SessionId,
    string State,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    int ObjectCount,
    int StreamCount,
    int UnitCount,
    int CompoundCount,
    string? CalculationStatus,
    long? MemoryBytes,
    List<ErrorRecordDto> RecentErrors
);

public record ErrorRecordDto(
    DateTime Timestamp,
    string ErrorCode,
    string Message,
    string? StackTraceSummary
);

public record FlowsheetSnapshotDto(
    Dictionary<string, object> Objects,
    List<ConnectionDto> Connections,
    Dictionary<string, object> SolverState
);
```

## Error Handling

### Error Scenarios

1. **Tracing Backend Unavailable**
   - **Handling:** Log warning, continue without tracing (graceful degradation)
   - **User Impact:** None - operations continue normally, tracing data lost

2. **Metrics Endpoint Unreachable**
   - **Handling:** Buffer metrics locally, log warning, retry with backoff
   - **User Impact:** None - metrics may be delayed but operations unaffected

3. **Diagnostic Bundle Storage Full**
   - **Handling:** Prune oldest bundles (FIFO), log warning
   - **User Impact:** Oldest diagnostic data lost, recent data preserved

4. **Session Not Found for Diagnostics**
   - **Handling:** Return structured error with code "SessionNotFound"
   - **User Impact:** LLM agent receives clear error message to handle

5. **C# Layer Unreachable for Diagnostics**
   - **Handling:** Return partial diagnostics (Python-side only), indicate incomplete data
   - **User Impact:** Partial diagnostics with clear indication of missing data

6. **Profiling Memory Exceeded**
   - **Handling:** Automatically disable profiling, log warning, include notice in diagnostics
   - **User Impact:** Profiling stops, session continues normally

## Testing Strategy

### Unit Testing

**Python:**
- `test_correlation.py`: Context generation, propagation, scope management
- `test_tracing.py`: Span creation, attribute setting, exporter configuration
- `test_metrics.py`: Counter/histogram/gauge operations, Prometheus format
- `test_diagnostics_service.py`: Diagnostic collection, error recording, bundle creation

**C#:**
- `CorrelationContextTests.cs`: AsyncLocal propagation, Serilog enrichment
- `TracingAdapterTests.cs`: Span lifecycle, exception recording
- `DiagnosticsCollectorTests.cs`: Session/flowsheet diagnostics extraction

### Integration Testing

- **Cross-layer correlation**: Verify requestId appears in both Python and C# logs
- **End-to-end tracing**: Verify complete trace from MCP tool to DWSIM calculation
- **Metrics accuracy**: Verify counters increment correctly across tool calls
- **Diagnostic tool**: Verify get_diagnostics returns accurate session state

### End-to-End Testing

- **Golden path**: Run simulation, verify traces in Jaeger, logs in Seq, metrics in Prometheus
- **Failure scenario**: Trigger calculation error, verify diagnostic bundle captured
- **Performance**: Verify observability overhead < 5ms per request
- **Load test**: 100 concurrent tool calls, verify no observability bottlenecks

## Configuration

### Environment Variables

```bash
# Logging
DWSIM_LOG_LEVEL=INFO                    # DEBUG, INFO, WARNING, ERROR
DWSIM_LOG_FORMAT=json                   # json, console
DWSIM_LOG_FILE=/var/log/dwsim/server.log  # Optional file sink

# Tracing
DWSIM_TRACING_ENABLED=true
DWSIM_TRACING_EXPORTER=jaeger           # jaeger, zipkin, otlp, console, none
DWSIM_TRACING_ENDPOINT=http://localhost:14268/api/traces
DWSIM_TRACING_SAMPLE_RATE=1.0           # 0.0 to 1.0

# Metrics
DWSIM_METRICS_ENABLED=true
DWSIM_METRICS_PORT=9090                 # Prometheus scrape port

# Diagnostics
DWSIM_DIAGNOSTICS_BUNDLE_PATH=/var/lib/dwsim/diagnostics
DWSIM_DIAGNOSTICS_MAX_BUNDLES=100
DWSIM_DIAGNOSTICS_RETENTION_HOURS=24

# Seq (optional)
DWSIM_SEQ_URL=http://localhost:5341
DWSIM_SEQ_API_KEY=                      # Optional API key
```

### Pydantic Settings Model

```python
class ObservabilitySettings(BaseSettings):
    log_level: str = "INFO"
    log_format: Literal["json", "console"] = "json"
    log_file: Optional[Path] = None

    tracing_enabled: bool = False
    tracing_exporter: Literal["jaeger", "zipkin", "otlp", "console", "none"] = "none"
    tracing_endpoint: Optional[str] = None
    tracing_sample_rate: float = 1.0

    metrics_enabled: bool = False
    metrics_port: int = 9090

    diagnostics_bundle_path: Path = Path("/var/lib/dwsim/diagnostics")
    diagnostics_max_bundles: int = 100
    diagnostics_retention_hours: int = 24

    seq_url: Optional[str] = None
    seq_api_key: Optional[str] = None

    class Config:
        env_prefix = "DWSIM_"
```
