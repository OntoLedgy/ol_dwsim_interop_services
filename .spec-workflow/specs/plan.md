# DWSIM MCP Server - Specification Plan

This document outlines the specification plan for building the DWSIM MCP Server, organized in a fail-fast manner to validate critical assumptions early in the development process.

## Development Philosophy

The specs are ordered to:
1. **Validate core assumptions first**: Verify C# can reliably invoke DWSIM before building infrastructure
2. **Build incrementally**: Each spec builds on validated previous specs
3. **Test continuously**: Every spec includes testable deliverables
4. **Reduce rework**: Identify blockers early before investing in dependent systems

## Test Case

**Primary Test Case**: Three-Phase Separator
- Simple unit operation with clear inputs/outputs
- Tests thermodynamic calculations and phase equilibrium
- Validates property passing and result extraction
- Provides foundation for more complex operations

---

## Phase 1: Core DWSIM Validation (Critical Assumptions)

### Spec 1.1: DWSIM Assembly Loading and Basic Invocation

**Spec Name**: `dwsim-assembly-loader`

**Description**:
Create a minimal C# console application that demonstrates successful loading and invocation of DWSIM assemblies. This spec validates the most critical assumption: that we can programmatically interact with DWSIM's .NET Framework libraries.

**Key Deliverables**:
- C# project targeting .NET Framework 4.8
- Load DWSIM.Interfaces, DWSIM.Thermodynamics assemblies
- Instantiate core DWSIM objects (Flowsheet, MaterialStream)
- Verify assembly versions and dependencies
- Document any binding redirects or configuration requirements
- Basic error handling for missing assemblies

**Success Criteria**:
- DWSIM assemblies load without exceptions
- Can create instances of key DWSIM classes
- No COM registration or GUI dependencies required
- Process runs headless on Windows Server

**Validation Test**:
Simple test that creates a Flowsheet object and prints its type name.

---

### Spec 1.2: Three-Phase Separator - Property Setting

**Spec Name**: `three-phase-separator-properties`

**Description**:
Implement basic property setting on a three-phase separator unit operation. This validates that we can configure DWSIM objects programmatically with realistic engineering parameters.

**Key Deliverables**:
- Create MaterialStream objects for inlet and outlets
- Add compounds (e.g., methane, ethane, propane, water)
- Set property package (e.g., Peng-Robinson)
- Configure inlet stream properties:
  - Temperature (K)
  - Pressure (Pa)
  - Molar flow (mol/s)
  - Composition (mole fractions)
- Add three-phase separator to flowsheet
- Connect streams to separator
- Set separator operating parameters (pressure drops, etc.)

**Success Criteria**:
- All properties set without exceptions
- Property values retrievable after setting (round-trip validation)
- Stream connections established
- No unit operation initialization errors

**Validation Test**:
Create configured separator, retrieve all set properties, assert they match inputs.

---

### Spec 1.3: Three-Phase Separator - Calculation Execution

**Spec Name**: `three-phase-separator-calculation`

**Description**:
Execute thermodynamic calculations on the configured three-phase separator and extract results. This validates the core simulation capability - the most critical technical risk.

**Key Deliverables**:
- Invoke flowsheet solver with configured separator
- Handle solver convergence (success/failure)
- Extract outlet stream properties:
  - Vapor phase composition and flow
  - Liquid phase composition and flow
  - Water phase composition and flow
- Read separator performance metrics
- Capture calculation errors/warnings
- Measure calculation time

**Success Criteria**:
- Calculation completes without crashes
- Solver converges (or provides meaningful error)
- Outlet properties physically reasonable (mass balance, energy balance)
- Results extractable in structured format

**Validation Test**:
Run calculation with known input, verify mass balance closure (<1% error), validate phase splits are non-zero.

---

## Phase 2: End-to-End C# Testing Infrastructure

### Spec 2.1: Structured Test Framework for DWSIM Operations

**Spec Name**: `dwsim-test-framework`

**Description**:
Build a reusable C# test framework (xUnit or NUnit) that encapsulates common DWSIM operations into testable units. This provides infrastructure for all future testing and validates patterns for the worker implementation.

**Key Deliverables**:
- xUnit test project referencing DWSIM assemblies
- Test fixtures for:
  - Flowsheet creation and disposal
  - Compound addition
  - Property package configuration
  - Stream creation
  - Unit operation instantiation
- Assertion helpers for:
  - Property value comparison (with tolerance)
  - Mass/energy balance validation
  - Convergence status checking
- Golden test data (known input/output pairs)
- Test cleanup and resource disposal

**Success Criteria**:
- Tests run reliably in CI environment
- Clear pass/fail criteria for each operation
- Tests isolated (no shared state between tests)
- Execution time < 5s per test

**Validation Test**:
Run full test suite with multiple unit operations, achieve 100% pass rate.

---

### Spec 2.2: Session Lifecycle Management (C# Side)

**Spec Name**: `csharp-session-manager`

**Description**:
Implement session management in C#, enabling multiple concurrent flowsheet contexts with isolation. This tests the multi-session architecture before adding RPC complexity.

**Key Deliverables**:
- SessionManager class managing concurrent sessions
- Session creation/disposal with unique IDs
- Per-session flowsheet context (no shared state)
- Resource cleanup on session close
- Session registry (dictionary mapping sessionId → context)
- Timeout/quota enforcement skeleton
- Thread-safety for concurrent sessions (consider STA requirements)

**Success Criteria**:
- Can create 10+ concurrent sessions
- Sessions fully isolated (no cross-session interference)
- Proper disposal releases DWSIM resources
- No memory leaks after session close (verify with profiler)

**Validation Test**:
Create 20 sessions concurrently, run different calculations in each, verify results are independent, close all sessions, verify memory released.

---

### Spec 2.3: CAPE-OPEN Data Model Mapping

**Spec Name**: `cape-open-data-mapping`

**Description**:
Implement bidirectional mapping between DWSIM's CAPE-OPEN interfaces and structured DTOs. This validates the data interchange format before RPC serialization.

**Key Deliverables**:
- C# DTO classes for:
  - MaterialStream (ICapeThermoMaterialObject)
  - PropertyPackage (ICapeThermoPropertyPackage)
  - UnitOperation (base properties)
  - FlashResult (phase equilibrium)
- Converter classes:
  - DWSIM objects → DTOs (extraction)
  - DTOs → DWSIM objects (construction/update)
- CAPE-OPEN standard property names mapping
- Unit conversion utilities (SI units enforced)
- Validation logic for DTO fields

**Success Criteria**:
- Round-trip conversion preserves all properties
- CAPE-OPEN interface methods correctly invoked
- DTOs are JSON-serializable (Newtonsoft.Json compatible)
- Clear error messages for unsupported properties

**Validation Test**:
Create DWSIM MaterialStream, convert to DTO, serialize to JSON, deserialize, convert back to DWSIM object, verify properties match.

---

## Phase 3: Python MCP Server with pythonnet Integration (SIMPLIFIED)

### Spec 3.1: Convert DwsimWorker to Class Library

**Spec Name**: `dwsim-worker-class-library`

**Description**:
Convert DwsimWorker from console application (Exe) to class library (Dll) so it can be loaded by Python via pythonnet. This is a simple refactoring of the existing code.

**Key Deliverables**:
- Change `<OutputType>` from `Exe` to `Library` in DwsimWorker.csproj
- Remove `Program.cs` (no longer needed)
- Ensure SessionManager and all adapters are public
- Verify all DTOs are public and serializable
- Add XML documentation comments for public API
- Build DwsimWorker.dll successfully

**Success Criteria**:
- DwsimWorker.dll builds without errors
- All existing unit tests pass
- SessionManager is publicly accessible
- No breaking changes to existing C# API

**Validation Test**:
Build DwsimWorker.dll, reference from test project, verify SessionManager can be instantiated.

---

### Spec 3.2: pythonnet Bridge and Assembly Loading

**Spec Name**: `pythonnet-bridge`

**Description**:
Implement Python module to load DwsimWorker.dll via pythonnet and provide clean Python API for interop. This establishes the in-process bridge.

**Key Deliverables**:
- `clr_loader.py`: Load .NET Framework 4.8 assemblies via pythonnet
- `session_client.py`: Python wrapper around C# SessionManager
- Error handling: Convert C# exceptions to Python exceptions
- Type conversion: Map C# types to Python types (str, int, float, dict)
- Resource management: Ensure C# IDisposable objects are properly disposed
- Configuration: Assembly path resolution
- Logging: Python logging integrated with C# Serilog output

**Success Criteria**:
- Can load DwsimWorker.dll from Python
- Can instantiate SessionManager from Python
- Can call SessionManager methods (CreateSession, CloseSession)
- C# exceptions propagate to Python correctly
- No memory leaks after repeated calls

**Validation Test**:
Python script loads DwsimWorker.dll, creates session, runs three-phase separator calculation, extracts results, verifies same results as C# tests.

---

### Spec 3.3: Resource Limits and Timeout Enforcement (Python Side)

**Spec Name**: `python-resource-limits`

**Description**:
Implement safety mechanisms in Python: timeouts, memory monitoring, and quota enforcement. Since pythonnet runs in-process, implement safeguards at Python layer.

**Key Deliverables**:
- Per-request timeout using Python's asyncio.wait_for
- Memory monitoring using psutil (process memory usage)
- Calculation timeout with thread interruption
- Session lifetime limits (max duration tracking)
- Configurable limits via environment variables or config file
- Limit violation error responses to MCP client
- Diagnostic logging on limit breach

**Success Criteria**:
- Long-running calculation terminates on timeout
- Memory usage tracked and reported
- Limits configurable per deployment environment
- Graceful error reporting to MCP client

**Validation Test**:
Trigger timeout condition, verify Python catches it and returns proper MCP error response.

---

## Phase 4: Python MCP Server Implementation (SIMPLIFIED)

### Spec 4.1: Pydantic DTOs and Python Model Classes

**Spec Name**: `python-dto-models`

**Description**:
Create Pydantic models for validating MCP tool inputs and converting between Python and C# types. This provides type safety and validation.

**Key Deliverables**:
- Pydantic models in `models/` directory:
  - `cape_open/material_stream.py`
  - `cape_open/thermo_property_package.py`
  - `cape_open/unit_operation.py`
  - `mcp_inputs/session_inputs.py` (create_session, close_session)
  - `mcp_inputs/flowsheet_inputs.py` (add_stream, add_unit, connect)
  - `mcp_inputs/simulation_inputs.py` (run, get_results)
- Type hints for all fields
- Default values where applicable
- Custom validators (e.g., positive pressure)
- Conversion helpers: Python dict ↔ C# DTO objects

**Success Criteria**:
- All models validate MCP tool inputs correctly
- Validation catches invalid inputs before calling C#
- Type conversion works seamlessly (Python → C# → Python)
- mypy type checking passes

**Validation Test**:
Create Python model, convert to C# DTO via pythonnet, convert back, verify equality.

---

### Spec 4.2: Python Service Layer (High-Level DWSIM Operations)

**Spec Name**: `python-dwsim-service`

**Description**:
Build Python service layer providing high-level DWSIM operations using pythonnet bridge. This abstracts C# interop details from MCP tools.

**Key Deliverables**:
- `DwsimService` class wrapping pythonnet SessionManager
- High-level methods:
  - `create_session(name: Optional[str]) → str`
  - `close_session(session_id: str) → None`
  - `add_compound(session_id, compound_name) → None`
  - `set_property_package(session_id, package_name) → None`
  - `add_stream(session_id, name, properties) → str`
  - `add_unit(session_id, type, name, params) → str`
  - `connect_streams(session_id, source, target, port) → None`
  - `run_simulation(session_id) → SimulationResult`
  - `get_stream_properties(session_id, stream_id) → MaterialStreamDTO`
- Error mapping (C# exceptions → Python exceptions)
- Type conversion helpers
- Structured logging
- Type hints and docstrings

**Success Criteria**:
- Three-phase separator workflow implementable in ~20 lines of Python
- Clear exceptions for all error cases
- Methods can be called synchronously or wrapped in async
- Service testable with mock SessionManager

**Validation Test**:
Python script using DwsimService to run three-phase separator, verify results match C# direct tests.

---

## Phase 5: MCP Server Framework and Tools

### Spec 5.1: MCP Server Bootstrap and Configuration

**Spec Name**: `mcp-server-bootstrap`

**Description**:
Set up MCP server using official Python SDK, handling stdio transport, configuration, and lifecycle. This creates the LLM-facing entry point.

**Key Deliverables**:
- `server.py` as main entry point
- MCP SDK integration (import mcp)
- Configuration management (Pydantic settings)
- Environment variable support
- Logging configuration (structlog)
- Server lifecycle (startup, shutdown)
- DwsimService dependency injection
- Health check mechanism

**Success Criteria**:
- Server starts and responds to MCP protocol messages
- Can list available tools (even if empty initially)
- Graceful shutdown on SIGTERM
- Configuration loaded from file/env vars
- Logs structured JSON to stdout/file

**Validation Test**:
Start server, send MCP list_tools request via stdio, verify response format, test shutdown.

---

### Spec 5.2: Session Management MCP Tools

**Spec Name**: `mcp-session-tools`

**Description**:
Implement MCP tools for session lifecycle management. These are foundational tools required by all other operations.

**Key Deliverables**:
- MCP tool: `create_session`
  - Input schema: `{ name?: string }`
  - Output: `{ sessionId: string }`
  - Description for LLM agents
- MCP tool: `close_session`
  - Input: `{ sessionId: string }`
  - Output: `{ success: boolean }`
- MCP tool: `save_case`
  - Input: `{ sessionId: string, filePath: string }`
  - Output: `{ success: boolean }`
- MCP tool: `load_case`
  - Input: `{ sessionId: string, filePath: string }`
  - Output: `{ sessionId: string }`
- Error handling and user-friendly messages
- Tool result formatting for MCP

**Success Criteria**:
- All tools callable from MCP client (e.g., Claude Desktop)
- Input validation via Pydantic
- Clear error messages returned to agent
- Tools composable (create → use → close workflow)

**Validation Test**:
MCP client test: create session, verify sessionId returned, close session, verify cleanup.

---

### Spec 5.3: Flowsheet Building MCP Tools

**Spec Name**: `mcp-flowsheet-tools`

**Description**:
Implement MCP tools for constructing chemical process flowsheets. These enable agents to build simulations programmatically.

**Key Deliverables**:
- MCP tool: `add_compound`
  - Input: `{ sessionId, compoundName }`
  - Validates compound exists in DWSIM database
- MCP tool: `set_property_package`
  - Input: `{ sessionId, packageName }`
  - Options: Peng-Robinson, SRK, NRTL, etc.
- MCP tool: `add_stream`
  - Input: `{ sessionId, name, temperature?, pressure?, flow?, composition? }`
  - Returns: `{ streamId }`
- MCP tool: `add_unit`
  - Input: `{ sessionId, unitType, name, parameters? }`
  - Unit types: separator, mixer, heater, pump, valve, etc.
  - Returns: `{ unitId }`
- MCP tool: `connect`
  - Input: `{ sessionId, sourceId, targetId, portName }`
- MCP tool: `list_objects`
  - Input: `{ sessionId }`
  - Returns: All streams, units, connections
- MCP tool: `set_object_parameter`
  - Input: `{ sessionId, objectId, parameterName, value }`
- MCP tool: `delete_object`
  - Input: `{ sessionId, objectId }`

**Success Criteria**:
- Three-phase separator buildable through tools
- Tools discoverable by LLM (good descriptions)
- Validation prevents invalid flowsheets
- Tools idempotent where possible

**Validation Test**:
LLM agent builds three-phase separator using only MCP tools, verifies flowsheet structure.

---

### Spec 5.4: Simulation Execution MCP Tools

**Spec Name**: `mcp-simulation-tools`

**Description**:
Implement MCP tools for running simulations and retrieving results. These execute calculations and provide data to agents.

**Key Deliverables**:
- MCP tool: `run`
  - Input: `{ sessionId }`
  - Output: `{ status, convergence, messages }`
  - Handles long-running calculations (progress reporting future)
- MCP tool: `get_status`
  - Input: `{ sessionId }`
  - Output: `{ running, progress?, errors? }`
- MCP tool: `get_results`
  - Input: `{ sessionId, objectId? }`
  - Returns: Stream properties, unit outputs
  - Format: CAPE-OPEN standard properties
- Error handling:
  - Convergence failures
  - Invalid flowsheet topology
  - Missing parameters
- Result caching (optional)

**Success Criteria**:
- Simulation runs without blocking MCP server
- Results formatted for easy LLM parsing
- Errors actionable (suggest fixes)
- Large result sets handled gracefully

**Validation Test**:
Agent runs three-phase separator simulation, extracts all outlet stream properties, verifies mass balance.

---

## Phase 6: Thermodynamic and Analysis Tools

### Spec 6.1: Thermodynamic Flash Calculation Tools

**Spec Name**: `mcp-flash-tools`

**Description**:
Expose standalone thermodynamic flash calculations (phase equilibrium) as MCP tools. These enable property calculations without full flowsheet simulation.

**Key Deliverables**:
- MCP tool: `flash_tp` (Temperature-Pressure flash)
  - Input: `{ sessionId, compounds, composition, temperature, pressure }`
  - Output: `{ phases, phaseFractions, phaseCompositions, properties }`
- MCP tool: `flash_ph` (Pressure-Enthalpy flash)
  - Input: `{ sessionId, compounds, composition, pressure, enthalpy }`
- MCP tool: `flash_ps` (Pressure-Entropy flash)
  - Input: `{ sessionId, compounds, composition, pressure, entropy }`
- Property calculations:
  - Density, viscosity, thermal conductivity
  - Vapor pressure, surface tension
  - Enthalpy, entropy, Gibbs energy
- CAPE-OPEN ICapeThermoEquilibriumServer integration

**Success Criteria**:
- Flash calculations converge for typical mixtures
- Results match DWSIM GUI calculations
- Performance: < 1s for simple mixtures
- Clear error messages for non-converging cases

**Validation Test**:
Run flash_tp for methane-ethane mixture at known conditions, compare with literature data or DWSIM GUI.

---

### Spec 6.2: Sensitivity Analysis and Parameter Studies

**Spec Name**: `mcp-sensitivity-tools`

**Description**:
Implement tools for automated parameter studies and sensitivity analysis. This enables optimization workflows.

**Key Deliverables**:
- MCP tool: `sensitivity_analysis`
  - Input: `{ sessionId, variable, range, steps, outputs }`
  - Runs simulation multiple times with varying parameter
  - Returns: Table of inputs vs outputs
- MCP tool: `optimize`
  - Input: `{ sessionId, objective, variables, constraints }`
  - Uses DWSIM's optimization engine
  - Returns: Optimal parameters and objective value
- Progress reporting for long-running studies
- Result export to CSV/JSON

**Success Criteria**:
- Can run 10-point sensitivity study
- Results structured for easy plotting
- Optimization converges for test cases
- Handles failed simulations gracefully (partial results)

**Validation Test**:
Sensitivity study on separator pressure (5 points), verify all simulations run, results tabulated correctly.

---

## Phase 7: Resources, Documentation, and Advanced Features

### Spec 7.1: MCP Resource Providers

**Spec Name**: `mcp-resource-providers`

**Description**:
Implement MCP resource providers for documentation, sample cases, and large result sets. Resources enable agents to access data beyond tool responses.

**Key Deliverables**:
- Resource: `resource://session/{sessionId}/results/{objectId}`
  - Provides detailed results (too large for tool response)
  - Format: JSON, CSV, or raw data
- Resource: `resource://docs/{topic}`
  - DWSIM documentation excerpts
  - Unit operation guides
  - Thermodynamic model references
- Resource: `resource://cases/{caseName}`
  - Sample flowsheets (three-phase separator, distillation, etc.)
  - Loadable via load_case tool
- Resource listing and discovery

**Success Criteria**:
- Resources accessible via MCP resource protocol
- Large result sets (>100 KB) served efficiently
- Documentation searchable by topic
- Sample cases loadable and runnable

**Validation Test**:
Agent requests resource://docs/separator, receives markdown documentation, uses info to configure separator.

---

### Spec 7.2: Export and Reporting Tools

**Spec Name**: `mcp-export-tools`

**Description**:
Implement tools for exporting simulation results and generating reports. This enables agents to persist and share results.

**Key Deliverables**:
- MCP tool: `export_csv`
  - Input: `{ sessionId, objectIds?, filePath }`
  - Exports stream/unit properties to CSV
- MCP tool: `export_json`
  - Input: `{ sessionId, format? }` (format: full, summary)
  - Returns JSON representation of flowsheet
- MCP tool: `generate_report`
  - Input: `{ sessionId, template?, filePath }`
  - Generates human-readable report (Markdown or HTML)
  - Includes flowsheet diagram (optional), property tables, convergence info
- File path sandboxing (security)

**Success Criteria**:
- Exports readable by Excel/Python pandas
- JSON export/import round-trip (save/load via JSON)
- Reports include all key information
- Paths validated (no directory traversal)

**Validation Test**:
Export separator results to CSV, load in pandas, verify data structure and values.

---

### Spec 7.3: Observability and Debugging Tools

**Spec Name**: `mcp-observability`

**Description**:
Implement comprehensive logging, tracing, and diagnostic tools. This enables debugging and performance monitoring.

**Key Deliverables**:
- Structured logging (JSON) with correlation IDs
  - sessionId, requestId, toolName, duration
- OpenTelemetry integration
  - Trace requests from MCP tool → Python service → C# worker → DWSIM
  - Span timing for each layer
- Metrics collection:
  - Tool call count, success rate, latency (P50, P95, P99)
  - Active session count
  - Memory usage per session
- MCP tool: `get_diagnostics`
  - Input: `{ sessionId? }`
  - Returns: Session state, last errors, resource usage
- Log export to Seq, Elasticsearch, or file

**Success Criteria**:
- Every request fully traceable
- Performance bottlenecks identifiable
- Logs searchable by sessionId
- Diagnostic tool helps troubleshoot failures

**Validation Test**:
Run simulation with tracing enabled, verify spans recorded, visualize in Jaeger/Zipkin.

---

### Spec 7.4: Alternative Interop Mode (pythonnet)

**Spec Name**: `pythonnet-interop-mode`

**Description**:
Implement alternative interop mode using pythonnet for direct .NET assembly loading. This provides lower-latency option vs Named Pipes.

**Key Deliverables**:
- pythonnet-based bridge loading DWSIM assemblies in-process
- Same DwsimService interface as JSON-RPC client
- Configuration flag to choose interop mode (Named Pipes vs pythonnet)
- Performance comparison tests
- Documentation on trade-offs:
  - pythonnet: Lower latency, shared crash domain
  - Named Pipes: Isolation, separate processes

**Success Criteria**:
- pythonnet mode functional equivalence to Named Pipes mode
- Latency reduction measurable (expect 5-10x faster)
- Graceful fallback if pythonnet unavailable

**Validation Test**:
Run all tests in both modes, verify identical results, measure latency difference.

---

## Phase 8: Integration, Deployment, and Documentation

### Spec 8.1: End-to-End Integration Tests

**Spec Name**: `e2e-integration-tests`

**Description**:
Comprehensive integration tests covering full workflows from MCP client to DWSIM engine. This validates the entire system.

**Key Deliverables**:
- Golden test cases:
  - Three-phase separator (reference case)
  - Distillation column
  - Heat exchanger network
  - Complex flowsheet (10+ units)
- Test fixtures and data
- CI/CD pipeline integration (GitHub Actions)
- Performance benchmarks:
  - Tool call latency
  - Simulation execution time
  - Memory usage
- Regression test suite
- Test coverage reporting (>80% target)

**Success Criteria**:
- All golden tests pass consistently
- Tests run in CI within 10 minutes
- No flaky tests (100% reproducible)
- Clear failure diagnostics

**Validation Test**:
Run full test suite, achieve >80% code coverage, all tests green.

---

### Spec 8.2: Deployment Packaging and Distribution

**Spec Name**: `deployment-packaging`

**Description**:
Package the MCP server for easy deployment. Provide installation instructions and deployment configurations.

**Key Deliverables**:
- Python package (wheel or sdist)
  - pyproject.toml with dependencies
  - Entry point script
- C# worker executable
  - Self-contained or framework-dependent
  - Config file template
- Installation guide:
  - Prerequisites (.NET Framework 4.8, Python 3.11+)
  - pip install instructions
  - DWSIM assembly setup
  - Configuration options
- Docker images (optional):
  - Multi-stage build (Python + .NET)
  - docker-compose for orchestration
- systemd service files (Linux/WSL)
- Windows Service wrapper (optional)

**Success Criteria**:
- One-command installation (pip install)
- Minimal manual configuration required
- Works on clean Windows Server 2022 install
- Clear error messages for missing dependencies

**Validation Test**:
Fresh Windows VM, follow installation guide, run test case, verify success.

---

### Spec 8.3: Documentation and User Guides

**Spec Name**: `comprehensive-documentation`

**Description**:
Complete user-facing documentation covering all aspects of the MCP server. Enables users to adopt and integrate the server.

**Key Deliverables**:
- README.md:
  - Project overview
  - Quick start guide
  - Architecture diagram
- API Reference (docs/api/):
  - All MCP tools with examples
  - Request/response schemas
  - Error codes reference
  - CAPE-OPEN mapping guide
- User Guides (docs/guides/):
  - Configuration options
  - Troubleshooting common issues
  - Performance tuning
  - Security best practices
- Architecture Documentation (docs/architecture/):
  - System design overview
  - Interop strategy rationale
  - Session management
  - Observability
- Example workflows:
  - Three-phase separator walkthrough
  - Optimization case study
  - Custom compound addition
- LLM-friendly prompts:
  - Prompt templates for common tasks
  - System prompt suggestions

**Success Criteria**:
- All tools documented with examples
- Troubleshooting guide addresses >90% of common issues
- Documentation searchable and well-organized
- LLM agents can understand and use tools from docs

**Validation Test**:
New user follows quick start guide, successfully runs first simulation within 15 minutes.

---

## Phase 9: Advanced Features and Optimization (Future)

### Spec 9.1: Dynamic Simulation Support

**Spec Name**: `dynamic-simulation-tools`

**Description**:
Add support for dynamic (time-based) simulation capabilities. This extends beyond steady-state calculations.

**Key Deliverables**:
- Tools for time-dependent problems
- Integration with DWSIM.DynamicsManager
- Time-series result export

**Success Criteria**:
- Can run transient startup simulation
- Results include time-series data

---

### Spec 9.2: Custom Unit Operation Integration

**Spec Name**: `custom-unit-operations`

**Description**:
Enable users to add custom unit operations via Python or IronPython scripts. This extends DWSIM's built-in capabilities.

**Key Deliverables**:
- Sandboxed script execution
- CAPE-OPEN interface implementation
- Script validation and error handling

**Success Criteria**:
- Can add simple custom unit (e.g., custom separator logic)
- Script errors don't crash worker

---

### Spec 9.3: Multi-Tenancy and Authentication

**Spec Name**: `multi-tenant-auth`

**Description**:
Add authentication, authorization, and multi-tenancy support for shared server deployments.

**Key Deliverables**:
- API key-based authentication
- Per-tenant session quotas
- Audit logging
- Rate limiting

**Success Criteria**:
- Multiple users can use server concurrently
- Sessions isolated by tenant
- No cross-tenant data leakage

---

## Summary

This plan provides a structured, incremental approach to building the DWSIM MCP Server. Each spec builds on validated assumptions, reducing risk and rework.

### Architectural Decision Update (2026-01-08)

**Selected Approach**: Python MCP Server with pythonnet (in-process interop)

**Key Changes from Original Plan**:
- **Phase 3 Simplified**: Eliminated JSON-RPC over Named Pipes
  - ~~Spec 3.1: JSON-RPC Server (C# Side)~~ → **REMOVED**
  - ~~Spec 3.2: JSON-RPC Methods~~ → **REMOVED**
  - **New Spec 3.1**: Convert DwsimWorker to Class Library (simple refactoring)
  - **New Spec 3.2**: pythonnet Bridge (simpler than JSON-RPC)
  - Spec 3.3: Resource Limits moved to Python side

- **Phase 4 Simplified**: No JSON-RPC client needed
  - ~~Spec 4.1: JSON-RPC Client~~ → **REMOVED**
  - **New Spec 4.1**: Pydantic DTOs (simplified for MCP inputs)
  - **New Spec 4.2**: DwsimService (direct C# calls via pythonnet)

**Benefits of New Approach**:
- ✅ **Single process** (simpler deployment, debugging)
- ✅ **Zero IPC overhead** (better performance)
- ✅ **Less code** (no JSON-RPC server/client)
- ✅ **Faster development** (2 weeks saved)
- ✅ **Right language for users** (Python-native)

**Specs Eliminated**: 3 (3.1, 3.2, 4.1 JSON-RPC components)
**Specs Simplified**: 2 (3.3, 4.2 no longer need IPC)
**New Specs Added**: 2 (3.1 class library, 3.2 pythonnet bridge)

**Total Specs**: 23 (reduced from 26)

**Updated Timeline**:
- Phase 1-2: 1-2 weeks (critical path) - **UNCHANGED**
- Phase 3-4: **1 week** (reduced from 1-2 weeks due to simpler architecture)
- Phase 5-6: 2-3 weeks - **UNCHANGED**
- Phase 7-8: 1-2 weeks - **UNCHANGED**
- **Total MVP**: **4-8 weeks** (reduced from 5-9 weeks)

**Current Status** (as of 2026-01-08):
- ✅ Phase 1: Complete (Specs 1.1, 1.2, 1.3)
- ✅ Phase 2: Complete (Specs 2.1, 2.2, 2.3)
- 🔄 Phase 3: Ready to start with new pythonnet approach

**Next Steps**:
1. ✅ Architectural decision documented (see docs/architecture/interop-strategy.md)
2. **Begin Spec 3.1**: Convert DwsimWorker.csproj from Exe to Library
3. **Then Spec 3.2**: Implement pythonnet bridge (clr_loader.py, session_client.py)
4. **Then Spec 3.3**: Add Python-side resource limits
5. Continue with Phase 4: Python MCP Server implementation

**References**:
- [Architectural Decision](docs/architecture/interop-strategy.md)
- [Updated Tech Stack](.spec-workflow/steering/tech.md)
