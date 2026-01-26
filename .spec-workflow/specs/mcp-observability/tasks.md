# Tasks Document: MCP Observability and Debugging Tools

## Phase 1: Correlation Context Infrastructure

- [x] 1.1 Create Python Correlation Context module
  - File: `mcp_service/server/dwsim_mcp_server/observability/correlation.py`
  - Implement CorrelationContext dataclass with request_id, session_id, tool_name, start_time
  - Create generate_request_id() using UUID4
  - Implement contextvars-based get_current_context() and set_current_context()
  - Create correlation_scope() context manager for automatic context management
  - Purpose: Foundation for cross-layer request tracing
  - _Leverage: existing observability/logging.py patterns_
  - _Requirements: REQ-OBS-1_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in async patterns and context propagation | Task: Create correlation context module using contextvars for request ID propagation, implementing CorrelationContext class and correlation_scope context manager following REQ-OBS-1 | Restrictions: Do not modify existing logging.py, use stdlib only (no external deps), ensure thread-safety with contextvars | _Leverage: observability/logging.py for structlog patterns | _Requirements: REQ-OBS-1 (correlation IDs across layers) | Success: Context propagates correctly across async calls, requestId generated uniquely per request, context manager handles cleanup properly | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [x] 1.2 Create C# CorrelationContext class
  - File: `mcp_service/dwsim_worker/DwsimWorker/Observability/CorrelationContext.cs`
  - Implement CorrelationContext with RequestId, SessionId, ToolName properties
  - Use AsyncLocal<T> for ambient context storage
  - Create Begin() factory method returning IDisposable scope
  - Implement Current static property for context access
  - Purpose: Receive and store correlation IDs from Python layer
  - _Leverage: existing Program.cs Serilog configuration_
  - _Requirements: REQ-OBS-1_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with expertise in async patterns and Serilog | Task: Create CorrelationContext class using AsyncLocal<T> for ambient context, with IDisposable scope pattern for automatic cleanup following REQ-OBS-1 | Restrictions: Do not modify existing Program.cs yet, keep class self-contained, ensure thread-safety | _Leverage: Serilog patterns in Program.cs | _Requirements: REQ-OBS-1 (correlation across C# layer) | Success: AsyncLocal correctly stores/retrieves context, Begin() returns proper disposable scope, Current property works from any thread | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [x] 1.3 Create Serilog CorrelationEnricher
  - File: `mcp_service/dwsim_worker/DwsimWorker/Observability/CorrelationEnricher.cs`
  - Implement ILogEventEnricher interface
  - Add RequestId, SessionId, ToolName properties to all log events from CorrelationContext.Current
  - Purpose: Automatically enrich all Serilog logs with correlation IDs
  - _Leverage: existing Serilog configuration in Program.cs_
  - _Requirements: REQ-OBS-1_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer specializing in Serilog and logging infrastructure | Task: Implement ILogEventEnricher that reads from CorrelationContext.Current and adds correlation properties to every log event following REQ-OBS-1 | Restrictions: Handle null context gracefully, do not modify Program.cs yet, follow Serilog best practices | _Leverage: CorrelationContext from task 1.2, Program.cs Serilog setup | _Requirements: REQ-OBS-1 (structured logging fields) | Success: Enricher adds properties only when context exists, handles null safely, integrates with Serilog pipeline | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [x] 1.4 Integrate CorrelationEnricher into Serilog configuration
  - File: `mcp_service/dwsim_worker/DwsimWorker/Program.cs` (modify existing)
  - Add CorrelationEnricher to Serilog LoggerConfiguration
  - Ensure enricher runs for all log events
  - Purpose: Enable correlation ID logging throughout C# layer
  - _Leverage: existing Program.cs Serilog setup, CorrelationEnricher from task 1.3_
  - _Requirements: REQ-OBS-1_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with Serilog expertise | Task: Modify Program.cs to add CorrelationEnricher to Serilog pipeline using .Enrich.With<CorrelationEnricher>() following REQ-OBS-1 | Restrictions: Preserve existing enrichers and sinks, minimal changes to Program.cs, test that logs include correlation fields | _Leverage: CorrelationEnricher from task 1.3, existing Serilog config | _Requirements: REQ-OBS-1 | Success: All C# logs include RequestId/SessionId/ToolName when context is set, existing logging behavior preserved | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [x] 1.5 Enhance Python structlog with correlation context
  - File: `mcp_service/server/dwsim_mcp_server/observability/logging.py` (modify existing)
  - Add processor to inject correlation context into all log entries
  - Ensure requestId, sessionId, toolName appear in JSON logs
  - Purpose: Consistent correlation fields in Python logs
  - _Leverage: correlation.py from task 1.1, existing logging.py_
  - _Requirements: REQ-OBS-1_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with structlog expertise | Task: Add structlog processor to inject CorrelationContext fields into all log entries, modifying configure_logging() following REQ-OBS-1 | Restrictions: Preserve existing processors, handle missing context gracefully, maintain JSON output format | _Leverage: correlation.py context, existing logging.py processors | _Requirements: REQ-OBS-1 (Python structured logging) | Success: All Python logs include correlation fields when context set, backward compatible with existing code | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

## Phase 2: OpenTelemetry Distributed Tracing

- [ ] 2.1 Create Python tracing module
  - File: `mcp_service/server/dwsim_mcp_server/observability/tracing.py`
  - Implement configure_tracing() with exporter selection (jaeger, zipkin, otlp, console, none)
  - Create get_tracer() returning configured Tracer
  - Implement traced_operation() context manager for span creation
  - Create @trace_tool decorator for MCP tool functions
  - Add Pydantic settings for tracing configuration
  - Purpose: OpenTelemetry infrastructure for Python layer
  - _Leverage: correlation.py for span attributes_
  - _Requirements: REQ-OBS-2_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with OpenTelemetry expertise | Task: Create tracing module with configurable exporters, traced_operation context manager, and @trace_tool decorator following REQ-OBS-2 | Restrictions: Support graceful degradation when tracing disabled, use opentelemetry-api and opentelemetry-sdk, ensure sampling rate is configurable | _Leverage: correlation.py for context, ObservabilitySettings from design | _Requirements: REQ-OBS-2 (distributed tracing) | Success: Tracing configurable via env vars, spans created with correct attributes, decorator works on async functions | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 2.2 Create C# TracingAdapter
  - File: `mcp_service/dwsim_worker/DwsimWorker/Observability/TracingAdapter.cs`
  - Implement static Configure() for OpenTelemetry setup
  - Create StartSpan() returning IDisposable span scope
  - Implement SetSpanAttribute() and RecordException() methods
  - Add trace context extraction from environment/call metadata
  - Purpose: OpenTelemetry spans in C# layer linked to Python traces
  - _Leverage: CorrelationContext for trace parent extraction_
  - _Requirements: REQ-OBS-2_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with OpenTelemetry expertise | Task: Create TracingAdapter with static methods for span management, supporting trace context propagation from Python layer following REQ-OBS-2 | Restrictions: Use OpenTelemetry.Api NuGet package, handle disabled tracing gracefully, ensure spans link to parent context | _Leverage: CorrelationContext for parent trace ID, OpenTelemetry .NET SDK | _Requirements: REQ-OBS-2 (C# tracing layer) | Success: Spans created with correct parent context, exceptions recorded properly, configuration via appsettings or env vars | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 2.3 Add tracing to FlowsheetService
  - File: `mcp_service/server/dwsim_mcp_server/service/flowsheet_service.py` (modify existing)
  - Wrap key methods with @trace_tool or traced_operation()
  - Add span attributes for session_id, object_count, operation_type
  - Purpose: Trace visibility into flowsheet operations
  - _Leverage: tracing.py from task 2.1, existing FlowsheetService_
  - _Requirements: REQ-OBS-2_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Add tracing instrumentation to FlowsheetService methods using traced_operation() context manager, adding semantic attributes following REQ-OBS-2 | Restrictions: Minimal code changes, preserve existing behavior, add tracing at method boundaries not inside loops | _Leverage: tracing.py decorators, existing FlowsheetService methods | _Requirements: REQ-OBS-2 (trace flowsheet operations) | Success: Key operations create spans, attributes include session_id and operation details, no performance regression | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 2.4 Add tracing to C# adapters
  - Files: `mcp_service/dwsim_worker/DwsimWorker/Adapters/*.cs` (modify existing)
  - Add TracingAdapter.StartSpan() calls to key adapter methods
  - Record exceptions in spans for error tracking
  - Purpose: Trace visibility into C# DWSIM operations
  - _Leverage: TracingAdapter from task 2.2, existing adapters_
  - _Requirements: REQ-OBS-2_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Add tracing spans to adapter methods (StreamAdapter, CalculationAdapter, etc.) using TracingAdapter, recording exceptions following REQ-OBS-2 | Restrictions: Add spans at method entry/exit, don't trace every property access, use try-finally for span cleanup | _Leverage: TracingAdapter from task 2.2, existing adapter patterns | _Requirements: REQ-OBS-2 (C# layer tracing) | Success: Adapter operations visible in traces, exceptions recorded with stack info, spans properly nested | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

## Phase 3: Metrics Collection

- [ ] 3.1 Create Python metrics module
  - File: `mcp_service/server/dwsim_mcp_server/observability/metrics.py`
  - Implement MetricsCollector class with tool_call_total Counter, tool_call_duration_seconds Histogram, active_sessions Gauge
  - Add record_tool_call(), set_active_sessions(), record_memory_usage() methods
  - Implement get_metrics_text() for Prometheus exposition format
  - Purpose: Operational metrics for monitoring
  - _Leverage: prometheus_client library_
  - _Requirements: REQ-OBS-3_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with Prometheus metrics expertise | Task: Create MetricsCollector using prometheus_client with Counter, Histogram, and Gauge metrics following REQ-OBS-3 | Restrictions: Use prometheus_client library, label cardinality should be bounded, ensure thread-safety | _Leverage: prometheus_client patterns | _Requirements: REQ-OBS-3 (metrics collection) | Success: Metrics recorded correctly, Prometheus format output works, labels include tool_name and status | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 3.2 Add metrics endpoint to MCP server
  - File: `mcp_service/server/dwsim_mcp_server/server.py` (modify existing if applicable, or create metrics endpoint)
  - Expose /metrics endpoint for Prometheus scraping
  - Configure port via DWSIM_METRICS_PORT env var
  - Purpose: Enable Prometheus to scrape metrics
  - _Leverage: metrics.py from task 3.1_
  - _Requirements: REQ-OBS-3_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with HTTP server expertise | Task: Add HTTP endpoint serving Prometheus metrics format, configurable port via environment variable following REQ-OBS-3 | Restrictions: Don't block MCP server, use simple HTTP server or integrate with existing framework, handle disabled metrics gracefully | _Leverage: MetricsCollector.get_metrics_text() from task 3.1 | _Requirements: REQ-OBS-3 (Prometheus endpoint) | Success: /metrics returns valid Prometheus format, configurable port, doesn't interfere with MCP protocol | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 3.3 Integrate metrics into tool handlers
  - File: `mcp_service/server/dwsim_mcp_server/tools/*.py` (modify existing)
  - Add MetricsCollector.record_tool_call() to all tool handlers
  - Track duration and success/failure status
  - Purpose: Capture metrics for every MCP tool invocation
  - _Leverage: metrics.py from task 3.1, existing tool handlers_
  - _Requirements: REQ-OBS-3_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Add metrics recording to all MCP tool handlers, capturing duration and success/failure status following REQ-OBS-3 | Restrictions: Use decorator or wrapper pattern for consistency, measure wall-clock time, don't add metrics inside loops | _Leverage: MetricsCollector from task 3.1, existing tool handler patterns | _Requirements: REQ-OBS-3 (tool call metrics) | Success: Every tool call records counter and histogram, status label is correct, minimal code duplication | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

## Phase 4: Diagnostics Infrastructure

- [ ] 4.1 Create Python DiagnosticsService
  - File: `mcp_service/server/dwsim_mcp_server/service/diagnostics_service.py`
  - Implement DiagnosticsService class with get_server_diagnostics(), get_session_diagnostics() methods
  - Add record_error() for capturing diagnostic bundles
  - Implement get_diagnostic_bundle() for retrieving captured errors
  - Add bundle storage with retention limits
  - Purpose: Central diagnostics collection and retrieval
  - _Leverage: FlowsheetService, MetricsCollector, MemoryMonitor_
  - _Requirements: REQ-OBS-4, REQ-OBS-5_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with diagnostics expertise | Task: Create DiagnosticsService collecting server and session diagnostics, with error bundle capture following REQ-OBS-4 and REQ-OBS-5 | Restrictions: Limit bundle storage (max 100, 24h retention), truncate stack traces for security, handle missing sessions gracefully | _Leverage: FlowsheetService for session data, MetricsCollector for stats, MemoryMonitor for memory info | _Requirements: REQ-OBS-4 (diagnostics tool), REQ-OBS-5 (error bundles) | Success: Server diagnostics include uptime/sessions/errors, session diagnostics show state/objects/errors, bundles auto-pruned | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 4.2 Create C# DiagnosticsCollector
  - File: `mcp_service/dwsim_worker/DwsimWorker/Observability/DiagnosticsCollector.cs`
  - Implement GetSessionDiagnostics() returning session state, object counts, memory usage
  - Add GetFlowsheetDiagnostics() for detailed flowsheet inspection
  - Implement RecordError() and GetRecentErrors() for error tracking
  - Purpose: C#-side diagnostics for session and flowsheet state
  - _Leverage: SessionManager, FlowsheetContext_
  - _Requirements: REQ-OBS-4, REQ-OBS-5_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create DiagnosticsCollector extracting session and flowsheet state with error history following REQ-OBS-4 and REQ-OBS-5 | Restrictions: Don't expose internal DWSIM types in DTOs, limit error history to 5 per session, handle disposed sessions safely | _Leverage: SessionManager for session access, FlowsheetContext for flowsheet state | _Requirements: REQ-OBS-4 (session diagnostics), REQ-OBS-5 (error tracking) | Success: Diagnostics include object counts, memory usage, error history, handles edge cases (closed sessions) | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 4.3 Create Pydantic models for diagnostics
  - File: `mcp_service/server/dwsim_mcp_server/models/diagnostics.py`
  - Implement ServerDiagnostics, SessionDiagnostics, ErrorSummary, DiagnosticBundle Pydantic models
  - Add validation and serialization for MCP responses
  - Purpose: Type-safe diagnostic data structures
  - _Leverage: existing Pydantic patterns in models/_
  - _Requirements: REQ-OBS-4_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with Pydantic expertise | Task: Create Pydantic models for all diagnostic data structures following REQ-OBS-4 design document | Restrictions: Follow existing model patterns, use proper field types and validators, ensure JSON serializable | _Leverage: existing models in models/ directory | _Requirements: REQ-OBS-4 (structured diagnostic output) | Success: Models validate correctly, serialize to JSON, match design document schema | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 4.4 Create get_diagnostics MCP tool
  - File: `mcp_service/server/dwsim_mcp_server/tools/diagnostics.py`
  - Implement get_diagnostics MCP tool with optional session_id parameter
  - Return ServerDiagnostics when no session_id, SessionDiagnostics when provided
  - Add proper error handling for invalid session IDs
  - Register tool in MCP server
  - Purpose: LLM-accessible diagnostic interface
  - _Leverage: DiagnosticsService from task 4.1, existing tool patterns_
  - _Requirements: REQ-OBS-4_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with MCP tool expertise | Task: Create get_diagnostics MCP tool returning server or session diagnostics based on parameters following REQ-OBS-4 | Restrictions: Follow existing tool registration patterns, return structured JSON, handle SessionNotFound error | _Leverage: DiagnosticsService, existing tool patterns in tools/*.py | _Requirements: REQ-OBS-4 (diagnostic MCP tool) | Success: Tool registered and callable, returns correct diagnostics type, error handling for invalid sessions | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

## Phase 5: Log Export and Configuration

- [ ] 5.1 Add Seq sink support
  - File: `mcp_service/server/dwsim_mcp_server/observability/logging.py` (modify existing)
  - Add Seq HTTP sink configuration
  - Support DWSIM_SEQ_URL and DWSIM_SEQ_API_KEY env vars
  - Purpose: Export logs to Seq for aggregation
  - _Leverage: existing logging.py, seqlog or custom HTTP sink_
  - _Requirements: REQ-OBS-6_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with logging expertise | Task: Add Seq HTTP sink to structlog configuration, configurable via environment variables following REQ-OBS-6 | Restrictions: Handle Seq unavailability gracefully (log warning, continue), use async HTTP to avoid blocking, buffer on failure | _Leverage: existing logging.py configuration | _Requirements: REQ-OBS-6 (Seq export) | Success: Logs appear in Seq when configured, graceful degradation when unavailable, API key optional | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 5.2 Add file sink with rotation
  - File: `mcp_service/server/dwsim_mcp_server/observability/logging.py` (modify existing)
  - Add rotating file handler for JSON logs
  - Support DWSIM_LOG_FILE env var with configurable retention
  - Purpose: Local log persistence for debugging
  - _Leverage: existing logging.py, logging.handlers.RotatingFileHandler_
  - _Requirements: REQ-OBS-6_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Add rotating file handler to structlog with configurable path and retention following REQ-OBS-6 | Restrictions: Use stdlib RotatingFileHandler, JSON format for files, handle permission errors gracefully | _Leverage: existing logging.py | _Requirements: REQ-OBS-6 (file export) | Success: Logs written to file when configured, rotation works, handles disk full gracefully | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 5.3 Create ObservabilitySettings Pydantic model
  - File: `mcp_service/server/dwsim_mcp_server/observability/settings.py`
  - Implement ObservabilitySettings with all configuration options
  - Support environment variable loading with DWSIM_ prefix
  - Add validation for settings (e.g., sample_rate 0-1)
  - Purpose: Centralized observability configuration
  - _Leverage: existing Pydantic BaseSettings patterns_
  - _Requirements: REQ-OBS-1 through REQ-OBS-7_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with Pydantic expertise | Task: Create ObservabilitySettings using Pydantic BaseSettings for all observability configuration following design document | Restrictions: Use DWSIM_ env prefix, provide sensible defaults, validate ranges | _Leverage: existing settings patterns | _Requirements: All REQ-OBS requirements (configuration) | Success: All settings loadable from env vars, validation catches invalid values, defaults allow basic operation | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

## Phase 6: Testing

- [ ] 6.1 Unit tests for Python correlation context
  - File: `mcp_service/server/tests/unit/observability/test_correlation.py`
  - Test context generation, propagation, scope cleanup
  - Test async context isolation
  - Purpose: Verify correlation context correctness
  - _Leverage: pytest, pytest-asyncio_
  - _Requirements: REQ-OBS-1_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer with Python testing expertise | Task: Write comprehensive unit tests for correlation.py covering context generation, propagation, and cleanup following REQ-OBS-1 | Restrictions: Use pytest and pytest-asyncio, test edge cases (nested scopes, missing context), no external dependencies | _Leverage: pytest fixtures | _Requirements: REQ-OBS-1 | Success: Tests cover happy path and edge cases, async context isolation verified, cleanup tested | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 6.2 Unit tests for C# CorrelationContext
  - File: `mcp_service/dwsim_worker/DwsimWorker.Tests/Observability/CorrelationContextTests.cs`
  - Test AsyncLocal propagation, scope disposal, Serilog enrichment
  - Purpose: Verify C# correlation infrastructure
  - _Leverage: xUnit, existing test patterns_
  - _Requirements: REQ-OBS-1_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with testing expertise | Task: Write unit tests for CorrelationContext and CorrelationEnricher covering AsyncLocal behavior and Serilog enrichment following REQ-OBS-1 | Restrictions: Use xUnit, test thread isolation, mock Serilog where needed | _Leverage: existing test patterns in DwsimWorker.Tests | _Requirements: REQ-OBS-1 | Success: AsyncLocal propagation verified, enricher adds correct properties, scope disposal tested | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 6.3 Unit tests for metrics collector
  - File: `mcp_service/server/tests/unit/observability/test_metrics.py`
  - Test counter increments, histogram observations, gauge settings
  - Test Prometheus exposition format output
  - Purpose: Verify metrics correctness
  - _Leverage: pytest, prometheus_client_
  - _Requirements: REQ-OBS-3_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer | Task: Write unit tests for MetricsCollector verifying counter, histogram, and gauge operations following REQ-OBS-3 | Restrictions: Reset metrics between tests, verify Prometheus format, test label combinations | _Leverage: prometheus_client test utilities | _Requirements: REQ-OBS-3 | Success: All metric types tested, Prometheus format validated, labels correct | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 6.4 Unit tests for diagnostics service
  - File: `mcp_service/server/tests/unit/service/test_diagnostics_service.py`
  - Test server diagnostics collection, session diagnostics, error bundle capture
  - Mock FlowsheetService and MetricsCollector
  - Purpose: Verify diagnostics logic
  - _Leverage: pytest, unittest.mock_
  - _Requirements: REQ-OBS-4, REQ-OBS-5_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer | Task: Write unit tests for DiagnosticsService with mocked dependencies following REQ-OBS-4 and REQ-OBS-5 | Restrictions: Mock external services, test bundle retention/pruning, test session not found error | _Leverage: pytest fixtures, unittest.mock | _Requirements: REQ-OBS-4, REQ-OBS-5 | Success: All methods tested, error scenarios covered, bundle lifecycle tested | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 6.5 Integration tests for cross-layer correlation
  - File: `mcp_service/server/tests/integration/test_observability_integration.py`
  - Test requestId appears in both Python and C# logs
  - Test end-to-end trace from tool call to DWSIM
  - Purpose: Verify cross-layer observability works
  - _Leverage: pytest, log capture, trace capture_
  - _Requirements: REQ-OBS-1, REQ-OBS-2_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Integration Test Engineer | Task: Write integration tests verifying correlation IDs and traces propagate from Python to C# layer following REQ-OBS-1 and REQ-OBS-2 | Restrictions: Capture logs from both layers, verify matching requestId, may need test fixtures for pythonnet | _Leverage: existing integration test patterns | _Requirements: REQ-OBS-1, REQ-OBS-2 | Success: Same requestId in Python and C# logs, traces span both layers, errors recorded correctly | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

## Phase 7: Documentation and Final Integration

- [ ] 7.1 Update server initialization with observability
  - File: `mcp_service/server/dwsim_mcp_server/server.py` (modify existing)
  - Initialize ObservabilitySettings at startup
  - Configure logging, tracing, and metrics based on settings
  - Add startup log with configuration summary
  - Purpose: Enable observability on server start
  - _Leverage: all observability modules from previous tasks_
  - _Requirements: All REQ-OBS_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Integrate all observability components into server startup, initializing based on ObservabilitySettings following all REQ-OBS requirements | Restrictions: Graceful degradation if components fail to initialize, log configuration summary at startup | _Leverage: ObservabilitySettings, all observability modules | _Requirements: All REQ-OBS | Success: Server starts with observability configured, logs show config summary, graceful handling of disabled features | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 7.2 Add observability to C# worker initialization
  - File: `mcp_service/dwsim_worker/DwsimWorker/Program.cs` (modify existing)
  - Initialize TracingAdapter based on configuration
  - Add DiagnosticsCollector to dependency injection (if applicable)
  - Log startup with tracing configuration
  - Purpose: Enable C# observability on worker start
  - _Leverage: TracingAdapter, DiagnosticsCollector from previous tasks_
  - _Requirements: REQ-OBS-2, REQ-OBS-4_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Integrate tracing and diagnostics into worker initialization, configurable via appsettings or env vars following REQ-OBS-2 and REQ-OBS-4 | Restrictions: Preserve existing initialization, handle disabled tracing gracefully, log configuration | _Leverage: TracingAdapter, DiagnosticsCollector | _Requirements: REQ-OBS-2, REQ-OBS-4 | Success: Tracing configured at startup, diagnostics available, configuration logged | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_

- [ ] 7.3 Create observability documentation
  - File: `docs/observability.md`
  - Document all configuration options with examples
  - Include setup guides for Jaeger, Seq, Prometheus
  - Add troubleshooting section
  - Purpose: Enable users to configure and use observability
  - _Requirements: All REQ-OBS_
  - _Prompt: Implement the task for spec mcp-observability, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Technical Writer | Task: Create comprehensive observability documentation covering configuration, setup, and troubleshooting following all REQ-OBS requirements | Restrictions: Include concrete examples, document all env vars, provide copy-paste configs for common setups | _Leverage: ObservabilitySettings for config reference | _Requirements: All REQ-OBS | Success: All features documented, examples work, troubleshooting covers common issues | After completing: Mark task [-] as in_progress before starting, use log-implementation tool with artifacts after completion, then mark [x] as complete_
