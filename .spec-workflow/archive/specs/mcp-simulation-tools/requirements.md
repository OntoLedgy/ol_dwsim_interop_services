# Requirements Document

## Introduction

The Simulation Execution MCP Tools feature provides LLM agents with the ability to execute DWSIM chemical process simulations and retrieve results. This feature enables agents to run calculations on configured flowsheets, monitor simulation progress, and extract detailed property data from streams and unit operations. These tools complete the core workflow cycle: build flowsheet → run simulation → analyze results.

The feature exposes three primary MCP tools:
1. **run**: Executes simulation calculations on a session's flowsheet
2. **get_status**: Retrieves current simulation status (running, converged, failed)
3. **get_results**: Extracts stream properties and unit operation outputs in CAPE-OPEN format

This feature is critical for enabling AI agents to perform end-to-end chemical engineering workflows, from flowsheet construction through simulation to data analysis and optimization.

## Alignment with Product Vision

This feature directly supports the product vision outlined in product.md:

- **Key Feature: Simulation Tools**: Provides the core "run, get_status, get_results" tools listed in the comprehensive tool taxonomy
- **Composable Operations**: Each tool performs a discrete task (run vs. status vs. results); complex workflows composed from simple operations
- **Structured Data Exchange**: Results formatted as CAPE-OPEN standard properties for clear, typed interfaces
- **Observable by Default**: All operations logged with structured data; failures produce actionable diagnostics
- **Success Metric: Response Latency**: P95 latency < 30s for simulations aligns with stated performance targets
- **Business Objective: Enable AI-Powered Chemical Engineering**: Makes professional process simulation accessible to LLM agents through natural execution and result retrieval

The simulation execution tools are foundational to the entire MCP server's value proposition, completing the create → configure → execute → analyze workflow that chemical engineers and AI agents require.

## Requirements

### Requirement 1: Run Simulation

**User Story:** As an LLM agent, I want to execute a simulation on a configured flowsheet, so that I can compute thermodynamic properties and unit operation outputs based on the input conditions.

#### Acceptance Criteria

1. WHEN the agent calls the `run` tool with a valid sessionId THEN the system SHALL execute the DWSIM flowsheet solver for that session
2. IF the flowsheet contains no unit operations or streams THEN the system SHALL return an error indicating invalid flowsheet topology
3. WHEN the solver successfully converges THEN the system SHALL return a success status with convergence details
4. WHEN the solver fails to converge THEN the system SHALL return a failure status with diagnostic messages explaining the convergence failure
5. IF a simulation is already running for the session THEN the system SHALL return an error indicating simulation already in progress
6. WHEN the simulation completes THEN the system SHALL cache the results in the session context for subsequent retrieval via `get_results`
7. IF the simulation exceeds the configured timeout THEN the system SHALL terminate the calculation and return a timeout error
8. WHEN calculation errors occur (e.g., missing parameters, property package issues) THEN the system SHALL capture error messages from DWSIM and return them to the agent
9. WHEN the simulation runs THEN the system SHALL log simulation start, end, duration, and convergence status with structured logging
10. IF the session does not exist THEN the system SHALL return a NotFound error

### Requirement 2: Check Simulation Status

**User Story:** As an LLM agent, I want to check the current status of a simulation, so that I can determine if it's still running, has completed successfully, or has failed.

#### Acceptance Criteria

1. WHEN the agent calls `get_status` with a valid sessionId THEN the system SHALL return the current simulation state (idle, running, converged, failed, timeout)
2. IF a simulation is currently running THEN the system SHALL return "running" status with elapsed time
3. IF the most recent simulation succeeded THEN the system SHALL return "converged" status with convergence info
4. IF the most recent simulation failed THEN the system SHALL return "failed" status with error messages
5. IF no simulation has been run yet THEN the system SHALL return "idle" status
6. WHEN the simulation is running THEN the system SHALL include progress information if available (e.g., solver iteration count)
7. IF the session does not exist THEN the system SHALL return a NotFound error
8. WHEN `get_status` is called THEN the system SHALL respond within 500ms (P95) since this is a lightweight query

### Requirement 3: Retrieve Simulation Results

**User Story:** As an LLM agent, I want to extract stream properties and unit operation outputs after a successful simulation, so that I can analyze the process performance and make engineering decisions.

#### Acceptance Criteria

1. WHEN the agent calls `get_results` with a sessionId and no objectId THEN the system SHALL return summary results for all streams and unit operations
2. WHEN the agent calls `get_results` with a sessionId and a specific objectId THEN the system SHALL return detailed properties for that object only
3. IF no simulation has been run or the last simulation failed THEN the system SHALL return an error indicating no results available
4. WHEN returning stream properties THEN the system SHALL format data according to CAPE-OPEN standard property names (temperature, pressure, molarflow, molefraction, etc.)
5. WHEN returning unit operation results THEN the system SHALL include calculated outputs (e.g., separator phase splits, heater duty, pump power)
6. IF the objectId does not exist in the session THEN the system SHALL return a NotFound error
7. WHEN results are large (>100 KB) THEN the system SHALL consider pagination or resource URIs for data streaming
8. WHEN `get_results` is called THEN the system SHALL return cached results from the most recent simulation (no recalculation)
9. IF the session does not exist THEN the system SHALL return a NotFound error
10. WHEN results are retrieved THEN the system SHALL include SI units for all physical properties

### Requirement 4: Error Handling and Diagnostics

**User Story:** As an LLM agent, I want actionable error messages when simulations fail, so that I can understand what went wrong and adjust the flowsheet accordingly.

#### Acceptance Criteria

1. WHEN a simulation fails due to missing stream properties THEN the system SHALL identify which streams lack required properties
2. WHEN convergence fails THEN the system SHALL include DWSIM's diagnostic messages in the error response
3. WHEN a property package calculation fails THEN the system SHALL indicate which thermodynamic calculation failed and why
4. WHEN topology errors exist (e.g., unconnected ports) THEN the system SHALL list the invalid connections
5. IF a timeout occurs THEN the system SHALL indicate which unit operation was being solved when the timeout triggered
6. WHEN any error occurs THEN the system SHALL suggest potential remediation steps (e.g., "Check inlet stream temperature is specified")
7. WHEN an error response is returned THEN the system SHALL use typed error codes (InvalidState, Timeout, EngineFault, BadRequest)
8. WHEN errors occur THEN the system SHALL log full diagnostic information including session context and flowsheet state

### Requirement 5: Result Caching and Performance

**User Story:** As an LLM agent, I want fast access to simulation results without re-running calculations, so that I can query different properties efficiently.

#### Acceptance Criteria

1. WHEN a simulation completes successfully THEN the system SHALL cache all results in the session context
2. WHEN `get_results` is called multiple times THEN the system SHALL return cached data without re-executing the solver
3. IF the flowsheet is modified after a simulation THEN the system SHALL invalidate the cached results
4. WHEN `run` is called again THEN the system SHALL overwrite the cached results with new simulation output
5. WHEN a session is closed THEN the system SHALL clear all cached results for that session
6. WHEN results are cached THEN the system SHALL include a timestamp indicating when the simulation was executed

## Non-Functional Requirements

### Code Architecture and Modularity
- **Single Responsibility Principle**: Separate Python MCP tools for `run`, `get_status`, and `get_results` in `dwsim_mcp_server/tools/simulation.py`
- **Modular Design**: C# adapters encapsulate DWSIM solver invocation and result extraction in `DwsimWorker/Adapters/CalculationAdapter.cs`
- **Dependency Management**: Simulation tools depend on IPC client and CAPE-OPEN converters; no direct DWSIM dependencies in Python layer
- **Clear Interfaces**: Well-defined request/response DTOs for simulation operations, aligned with CAPE-OPEN domain model

### Performance
- **Latency**: `run` tool P95 latency < 30s for typical flowsheets (3-5 unit operations)
- **Latency**: `get_status` tool P95 latency < 500ms (lightweight query)
- **Latency**: `get_results` tool P95 latency < 2s for cached results (includes serialization)
- **Throughput**: Support concurrent simulations across different sessions (one simulation per session maximum)
- **Memory**: Cached results should not exceed 50 MB per session; large result sets should use resource URIs

### Security
- **Sandboxing**: Simulations execute within per-session working directories with no filesystem access outside sandbox
- **Timeouts**: All simulations subject to configurable timeout (default 120s) to prevent resource exhaustion
- **Resource Limits**: Memory limits enforced per session (default 2 GB); simulations terminated if exceeded
- **Input Validation**: All tool inputs validated via Pydantic models before execution

### Reliability
- **Error Isolation**: Simulation failures in one session SHALL NOT affect other sessions
- **Graceful Degradation**: If a simulation crashes DWSIM, the C# worker SHALL catch the exception and return a structured error (not crash the server)
- **Logging**: All simulation events (start, success, failure, timeout) logged with correlation IDs for debugging
- **Idempotency**: Calling `run` multiple times SHALL overwrite previous results (last-write-wins semantics)

### Usability
- **LLM-Friendly Descriptions**: All MCP tools SHALL include detailed descriptions explaining inputs, outputs, and error conditions
- **CAPE-OPEN Vocabulary**: Results SHALL use standard CAPE-OPEN property names for consistency with industry tools
- **Actionable Errors**: Error messages SHALL include specific remediation steps (not just "simulation failed")
- **Clear Status Reporting**: `get_status` SHALL provide human-readable status messages in addition to status codes
