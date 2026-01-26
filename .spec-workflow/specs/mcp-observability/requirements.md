# Requirements Document: MCP Observability and Debugging Tools

## Introduction

This specification defines the observability and debugging infrastructure for the DWSIM MCP Server. The feature provides comprehensive logging, distributed tracing, metrics collection, and diagnostic tools that enable developers, operators, and LLM agents to monitor system health, identify performance bottlenecks, and troubleshoot failures across the Python MCP server and C# DWSIM worker layers.

Observability is critical for production readiness, enabling:
- Rapid root cause analysis when simulations fail
- Performance optimization through latency and resource metrics
- Operational visibility into session activity and system health
- Debugging support for LLM agents interacting with the system

## Alignment with Product Vision

This feature directly supports key product principles outlined in product.md:

1. **Observable by Default**: "All operations are logged with structured data; failures produce actionable diagnostics" - this spec implements that principle comprehensively

2. **Safety First**: Observability enables detection of resource exhaustion, runaway simulations, and abnormal behavior before they impact system stability

3. **Success Metrics**: The product defines key metrics (P95 latency < 2s, >95% success rate, support 10+ concurrent sessions) that require observability infrastructure to measure

4. **Monitoring & Visibility** (product.md): The product explicitly requires:
   - Structured logging with correlation IDs
   - Per-request metrics (latency, success rate, engine time, memory)
   - OpenTelemetry integration for tracing
   - Diagnostic snapshots on failure

5. **Technology Stack** (tech.md): The tech stack specifies:
   - Serilog for .NET logging
   - structlog for Python logging
   - OpenTelemetry for distributed tracing
   - Seq for log aggregation

## Requirements

### REQ-OBS-1: Structured Logging with Correlation IDs

**User Story:** As an operator, I want all log entries to include correlation IDs (sessionId, requestId, toolName) so that I can trace a single request across all system components.

#### Acceptance Criteria

1. WHEN an MCP tool is invoked THEN the system SHALL generate a unique requestId and propagate it through all layers (Python service → C# worker → DWSIM engine)

2. WHEN a log entry is written THEN it SHALL include structured fields: timestamp, level, message, sessionId (if applicable), requestId, toolName, duration (for completed operations)

3. IF a request spans multiple layers THEN all log entries for that request SHALL share the same requestId for correlation

4. WHEN logs are written THEN they SHALL be formatted as JSON for machine parsing and ingestion into log aggregation systems

5. WHEN Python and C# layers write logs THEN they SHALL use consistent field names (sessionId, requestId, toolName) for cross-layer correlation

### REQ-OBS-2: OpenTelemetry Distributed Tracing

**User Story:** As a developer, I want distributed traces across Python and C# layers so that I can visualize request flow and identify performance bottlenecks.

#### Acceptance Criteria

1. WHEN an MCP tool is invoked THEN the Python layer SHALL create a root span with tool name, session ID, and input parameters

2. WHEN the Python layer calls the C# worker THEN it SHALL propagate trace context (trace ID, span ID) via pythonnet call metadata or environment

3. WHEN the C# worker processes a request THEN it SHALL create child spans for: SessionManager operations, Adapter calls, DWSIM engine calculations

4. WHEN a span completes THEN it SHALL include: duration, status (OK/ERROR), error message (if failed), and semantic attributes (session_id, tool_name, object_count)

5. WHEN tracing is enabled THEN spans SHALL be exportable to Jaeger, Zipkin, or any OTLP-compatible collector

6. IF tracing is disabled via configuration THEN the system SHALL continue to function without performance overhead

### REQ-OBS-3: Metrics Collection

**User Story:** As an operator, I want real-time metrics on tool call latency, success rates, and resource usage so that I can monitor system health and capacity.

#### Acceptance Criteria

1. WHEN MCP tools are invoked THEN the system SHALL collect and aggregate metrics: tool_call_count (by tool name), tool_call_duration_seconds (histogram with P50, P95, P99), tool_call_success_rate (by tool name)

2. WHEN sessions are active THEN the system SHALL track: active_session_count, session_lifetime_seconds, memory_usage_per_session_bytes (if measurable via pythonnet)

3. WHEN the C# worker processes calculations THEN it SHALL record: calculation_duration_seconds, solver_iteration_count, convergence_status

4. IF a Prometheus metrics endpoint is configured THEN the system SHALL expose metrics at /metrics in Prometheus exposition format

5. WHEN metrics are collected THEN they SHALL include labels for sessionId, toolName, and status (success/failure) for dimensional analysis

### REQ-OBS-4: Diagnostic MCP Tool (get_diagnostics)

**User Story:** As an LLM agent, I want to query system diagnostics so that I can troubleshoot simulation failures and understand session state.

#### Acceptance Criteria

1. WHEN get_diagnostics is called with no parameters THEN it SHALL return: server uptime, active session count, total tool calls, recent error count, memory usage

2. WHEN get_diagnostics is called with sessionId THEN it SHALL return: session state (active/closed), objects in flowsheet (count and types), last 5 errors for that session, calculation status, resource usage for session

3. IF the specified sessionId does not exist THEN get_diagnostics SHALL return an error with code "SessionNotFound" and helpful message

4. WHEN diagnostics are returned THEN they SHALL be formatted as structured JSON suitable for LLM parsing

5. WHEN recent errors are included THEN they SHALL contain: timestamp, error code, error message, stack trace summary (not full trace)

### REQ-OBS-5: Error Tracking and Diagnostic Bundles

**User Story:** As a developer, I want diagnostic bundles captured on failure so that I can reproduce and fix issues without requiring user to provide manual details.

#### Acceptance Criteria

1. WHEN an unhandled exception occurs THEN the system SHALL capture a diagnostic bundle containing: full stack trace, request parameters, session state snapshot, recent log entries (last 50)

2. WHEN a diagnostic bundle is created THEN it SHALL be stored in a configurable location (file path or memory buffer)

3. IF diagnostic bundle storage exceeds configured limits THEN oldest bundles SHALL be automatically pruned

4. WHEN a simulation fails with convergence error THEN the diagnostic bundle SHALL include: flowsheet object graph, input stream properties, solver parameters, iteration history

5. WHEN get_diagnostics is called after a failure THEN it SHALL include reference to the most recent diagnostic bundle ID for that session

### REQ-OBS-6: Log Export and Aggregation Support

**User Story:** As an operator, I want to export logs to Seq, Elasticsearch, or file so that I can use my existing log management infrastructure.

#### Acceptance Criteria

1. WHEN log export is configured for Seq THEN the system SHALL send structured logs via HTTP to the configured Seq server URL

2. WHEN log export is configured for Elasticsearch THEN the system SHALL write logs in Elasticsearch-compatible JSON format (ECS or custom)

3. WHEN log export is configured for file THEN the system SHALL write JSON-formatted logs to rotating log files with configurable retention

4. IF multiple log sinks are configured THEN the system SHALL write to all configured sinks concurrently

5. WHEN network log sinks (Seq, Elasticsearch) are unavailable THEN the system SHALL buffer logs locally and retry with exponential backoff

### REQ-OBS-7: Performance Profiling Hooks

**User Story:** As a developer, I want to enable detailed profiling for specific sessions so that I can diagnose performance issues in complex simulations.

#### Acceptance Criteria

1. WHEN a session is created with profiling_enabled=true THEN the system SHALL capture detailed timing for every adapter method call

2. WHEN profiling is enabled THEN the system SHALL record: method entry/exit times, parameter sizes, return value sizes, memory allocations (if measurable)

3. IF profiling is enabled THEN the profiling data SHALL be accessible via get_diagnostics for that session

4. WHEN profiling data exceeds 10MB THEN the system SHALL automatically disable profiling and log a warning

5. IF profiling is not explicitly enabled THEN no profiling overhead SHALL be incurred (default off)

## Non-Functional Requirements

### Code Architecture and Modularity

- **Single Responsibility Principle**: Logging, tracing, and metrics shall be implemented in separate modules that can be independently configured and tested
- **Modular Design**: Observability components shall use dependency injection allowing replacement of log sinks, trace exporters, and metrics backends
- **Dependency Management**: OpenTelemetry, structlog, and Serilog shall be isolated behind interfaces allowing future replacement
- **Clear Interfaces**: Python and C# layers shall share common observability contracts (field names, trace context format, metric names)

### Performance

- **Logging Overhead**: Structured logging shall add < 1ms overhead per log entry
- **Tracing Overhead**: Span creation and export shall add < 5ms overhead per request when enabled
- **Metrics Collection**: Metric recording shall add < 0.1ms overhead per metric point
- **Async Export**: Log and trace export shall be asynchronous to avoid blocking request processing
- **Sampling**: Tracing shall support configurable sampling rates (1%, 10%, 100%) to control overhead

### Security

- **No Sensitive Data in Logs**: Logs shall not include raw simulation data, file contents, or potentially sensitive compound names without explicit opt-in
- **Log Sanitization**: Stack traces shall be truncated to avoid leaking internal implementation details in production
- **Access Control**: Diagnostic bundles shall be stored in access-controlled directories (not world-readable)

### Reliability

- **Graceful Degradation**: If log/trace sinks are unavailable, the system shall continue operating without observability rather than failing requests
- **Buffer Management**: Log buffers shall have bounded size (configurable) with oldest-first eviction to prevent memory exhaustion
- **Error Isolation**: Failures in observability code shall not propagate to business logic

### Usability

- **Configuration**: All observability features shall be configurable via environment variables or config file without code changes
- **Default On**: Basic structured logging shall be enabled by default; advanced features (tracing, metrics endpoint) shall be opt-in
- **LLM-Friendly Output**: get_diagnostics output shall be formatted for easy parsing by LLM agents (consistent structure, clear field names)
- **Human-Readable Fallback**: Console logs shall have human-readable format option for development environments
