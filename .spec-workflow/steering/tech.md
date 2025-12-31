# Technology Stack

## Project Type

**MCP Server (Model Context Protocol Server)** - A polyglot client-server application consisting of:
- Python-based MCP server façade providing standardized LLM agent interface
- .NET Framework 4.8 engine worker hosting DWSIM simulation engine
- Inter-process communication layer bridging the two components

This is a **service/daemon application** designed to run as a background process, exposing MCP tools and resources to LLM agents while managing DWSIM simulation sessions.

## Core Technologies

### Primary Language(s)

**Python (MCP Server Façade)**
- **Language**: Python 3.11+ (recommended 3.12 for performance)
- **Runtime**: CPython interpreter
- **Package Manager**: pip with requirements.txt, or Poetry for dependency management
- **Type Checking**: mypy with type hints for static analysis
- **Virtual Environments**: venv or virtualenv for isolated dependencies

**C# (.NET Framework Engine Worker)**
- **Language**: C# 10+ (with .NET Framework 4.8 compatibility)
- **Runtime**: .NET Framework 4.8 (required for DWSIM compatibility)
- **Build Tool**: MSBuild via Visual Studio 2019/2022 or newer
- **Target Platform**: Windows x64 (primary), Windows x86 (legacy support)

### Key Dependencies/Libraries

**Python Stack:**
- **mcp**: Official Model Context Protocol SDK for Python (tools, resources, prompts)
- **pydantic**: Data validation and settings management with type hints
- **pythonnet (Python.NET)**: Optional direct .NET assembly interop (alternative to Named Pipes)
- **jsonrpcclient/jsonrpcserver**: JSON-RPC 2.0 implementation for IPC
- **structlog**: Structured logging with context and filtering
- **opentelemetry-api**: Distributed tracing and observability
- **asyncio**: Built-in async/await for concurrent operations
- **typer or click**: CLI framework for server configuration and management
- **comtypes**: Optional COM interop for CAPE-OPEN interface access (if needed)

**.NET Framework Stack:**
- **DWSIM Assemblies**: Core simulation engine (DWSIM.Thermodynamics, DWSIM.UnitOperations, DWSIM.FlowsheetSolver, etc.)
- **CapeOpen.dll**: CAPE-OPEN standard interface definitions
- **Newtonsoft.Json**: JSON serialization for DTO marshalling and CAPE-OPEN data exchange
- **Serilog**: Structured logging with sinks for console, file, Seq
- **System.IO.Pipes**: Named Pipe server for Windows IPC
- **System.Threading.Tasks**: Async/await support for concurrent sessions
- **xUnit or NUnit**: Unit testing framework

**Testing & Development:**
- **pytest**: Testing framework with fixtures and parametrization
- **pytest-asyncio**: Async test support
- **black**: Code formatting
- **ruff**: Fast Python linter
- **mypy**: Static type checking
- **Seq**: Log aggregation and search (development/debugging)

### Application Architecture

**Polyglot Client-Server with Inter-Process Communication (IPC)**

```
┌─────────────────────────────────────────────────────────────┐
│  LLM Agent (Claude, ChatGPT, etc.)                          │
└───────────────────────┬─────────────────────────────────────┘
                        │ MCP Protocol (stdio)
┌───────────────────────▼─────────────────────────────────────┐
│  MCP Server (Python 3.11+)                                  │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Tools Registry (create_session, add_unit, run, etc.)│   │
│  │ Resources (docs, results, sample cases)             │   │
│  │ Observability (logging, metrics, tracing)           │   │
│  └────────────────────┬────────────────────────────────┘   │
└───────────────────────┼─────────────────────────────────────┘
                        │ JSON-RPC 2.0 (Named Pipe/TCP)
                        │ OR pythonnet (direct .NET interop)
┌───────────────────────▼─────────────────────────────────────┐
│  Engine Worker (.NET Framework 4.8 Console App)             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ IPC Server (Named Pipe listener, JSON-RPC handler)  │   │
│  │ Session Manager (concurrent session registry)       │   │
│  │ EngineHost (STA thread, DWSIM lifecycle)            │   │
│  │ Adapters (typed wrappers around DWSIM APIs)         │   │
│  │ Limits (timeout, memory, quota enforcement)         │   │
│  └────────────────────┬────────────────────────────────┘   │
└───────────────────────┼─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│  DWSIM Engine (Multiple Assemblies)                         │
│  - DWSIM.Interfaces, DWSIM.Thermodynamics                   │
│  - DWSIM.UnitOperations, DWSIM.FlowsheetSolver              │
│  - DWSIM.MathOps, DWSIM.SharedClasses                       │
└─────────────────────────────────────────────────────────────┘
```

**Key Architectural Patterns:**
- **Façade Pattern**: Python layer presents clean MCP interface hiding IPC/interop complexity
- **Session-Based Isolation**: Each simulation session is independent with isolated state
- **STA Threading**: DWSIM hosted on Single-Threaded Apartment threads (COM compatibility)
- **Adapter Pattern**: Strongly-typed wrappers around DWSIM's legacy APIs
- **Event-Driven**: Long-running operations emit progress events and support cancellation
- **Type Safety**: Pydantic models for runtime validation, mypy for static checking
- **CAPE-OPEN Domain Model**: Use CAPE-OPEN standard interfaces as canonical data structure for simulation objects

### CAPE-OPEN as Domain Model and Interoperability Standard

**Purpose and Rationale:**

CAPE-OPEN (Computer Aided Process Engineering - Open Simulation Environment) provides a standardized set of interfaces for process simulation software interoperability. The DWSIM MCP Server adopts CAPE-OPEN as its **primary domain model** for several strategic reasons:

1. **Industry Standard**: CAPE-OPEN is the de facto standard for chemical engineering simulation interoperability
2. **Vendor Neutrality**: Enables integration with multiple simulation environments (Aspen Plus, HYSYS, PRO/II, etc.)
3. **Rich Domain Vocabulary**: Provides well-defined object models for streams, unit operations, thermodynamics, reactions
4. **LLM-Friendly**: Structured, documented interfaces that LLMs can reason about effectively
5. **Future-Proof**: Allows swapping DWSIM engine for other CAPE-OPEN compliant simulators

**CAPE-OPEN Interface Coverage in DWSIM:**

DWSIM implements both CAPE-OPEN 1.0 and 1.1 specifications:

**CAPE-OPEN 1.0 Interfaces:**
- `ICapeIdentification`: Object identification and naming
- `ICapeThermoMaterialObject`: Material stream properties and phases
- `ICapeThermoCalculationRoutine`: Thermodynamic property calculations
- `ICapeThermoEquilibriumServer`: Phase equilibrium calculations
- `ICapeThermoPropertyPackage`: Property package selection and configuration

**CAPE-OPEN 1.1 Interfaces:**
- `ICapeThermoMaterial`: Enhanced material object with compound/phase management
- `ICapeThermoCompounds`: Compound database access
- `ICapeThermoPhases`: Phase property access
- `ICapeThermoUniversalConstant`: Physical constants
- `ICapeThermoPropertyRoutine`: Property calculation methods
- `ICapeThermoEquilibriumRoutine`: Flash calculation routines

**CAPE-OPEN PME/COSE Interfaces (Flowsheet level):**
- `ICapeCOSEUtilities`: Flowsheet utilities and simulation context
- `ICapeMaterialTemplateSystem`: Material stream templates
- `ICapeFlowsheetMonitoring`: Flowsheet state monitoring
- `ICapeSimulationContext`: Simulation execution context

**Integration Strategy:**

1. **Data Transfer Objects (DTOs) Based on CAPE-OPEN**:
   - MCP tool inputs/outputs map directly to CAPE-OPEN interface methods
   - Example: `add_stream` tool creates `ICapeThermoMaterialObject`
   - Example: `set_property_package` configures `ICapeThermoPropertyPackage`

2. **CAPE-OPEN as Common Exchange Format**:
   - Simulation results serialized to CAPE-OPEN-compliant JSON structures
   - Enables interoperability: DWSIM → JSON → Aspen Plus (or any CAPE-OPEN tool)
   - LLM agents work with standardized CAPE-OPEN vocabulary, not proprietary formats

3. **Cross-Simulator Compatibility**:
   - MCP server abstracts specific simulator; CAPE-OPEN interfaces are simulator-agnostic
   - Future: Support multiple backends (DWSIM, Aspen Custom Modeler, PRO/II) via same MCP tools
   - Agent workflows portable across simulation environments

4. **Domain-Specific Language for LLMs**:
   - CAPE-OPEN provides canonical terminology (e.g., "flash_tp" not "two-phase equilibrium calc")
   - LLM prompts reference CAPE-OPEN interfaces: "Create an ICapeThermoMaterialObject with..."
   - Reduces ambiguity, improves agent reasoning about simulation tasks

**Example CAPE-OPEN Data Flow:**

```
LLM Agent Request:
  "Create a material stream with 50% methane, 50% ethane at 300K, 1 bar"

MCP Tool (create_stream):
  ↓
Python MCP Server:
  - Validates input (pydantic model)
  - Maps to CAPE-OPEN method: ICapeThermoMaterialObject.SetProp("temperature", 300)
  ↓
.NET Worker:
  - Instantiates DWSIM MaterialStream (implements ICapeThermoMaterialObject)
  - Sets CAPE-OPEN properties via interface methods
  ↓
DWSIM Engine:
  - Performs calculations using property package (ICapeThermoPropertyPackage)
  - Returns results via CAPE-OPEN interfaces
  ↓
MCP Server Response:
  - Serializes CAPE-OPEN properties to JSON
  - Returns to LLM in standardized format
```

**Benefits for Multi-Simulator Ecosystem:**

- **Consistent API**: Same MCP tools work with different simulation engines
- **Data Portability**: Export DWSIM case as CAPE-OPEN JSON, import into Aspen Plus
- **Third-Party Tools**: CAPE-OPEN-compliant unit ops can be integrated seamlessly
- **Vendor Lock-In Avoidance**: Not tied to DWSIM-specific formats or APIs

### Data Storage

**Primary Storage: File System + In-Memory Session State**
- **Session Context**: In-memory registry mapping `sessionId` to flowsheet state
- **Working Directories**: Per-session temp directories for intermediate files (sandboxed)
- **Simulation Files**: DWSIM native format (.dwxmz, .dwxml) for save/load operations
- **Results Export**: CSV, JSON, XML files written to sandboxed session directories

**Data Formats:**
- **IPC Protocol**: JSON-RPC 2.0 messages over Named Pipes
- **DTOs**: JSON-serializable Data Transfer Objects for requests/responses
- **DWSIM Files**: XML-based simulation files (proprietary format)
- **Results**: JSON (inline small results), CSV (bulk data), XML (structured reports)

**Caching:**
- **None initially**: Stateless tool calls; sessions are the unit of persistence
- **Future**: Compiled flowsheet caching for faster session warm-start

### External Integrations

**DWSIM Engine (Internal Integration)**
- **Protocol**: In-process .NET assembly loading and method invocation
- **Threading**: Cross-thread marshalling for STA compatibility
- **Lifetime**: Session-scoped; explicit dispose on session close
- **CAPE-OPEN Integration**: DWSIM implements CAPE-OPEN 1.0 and 1.1 interfaces for standardized process simulation interoperability

**MCP Clients (LLM Agents)**
- **Protocol**: MCP over stdio (standard input/output)
- **Authentication**: None (local trust model); future: API keys
- **Transport**: JSON messages via stdio pipes

**Artificial Intelligence Services Integration**
- **Common Services**: Integration with bclearer/artificial_intelligence_services Python toolkit
- **LLM Services**: Shared LLM client factory, model management, and text generation
- **Document Processing**: Docling/Unstructured parsers for simulation documentation extraction
- **Graph RAG**: Knowledge graph construction from simulation case studies and technical docs
- **Embeddings**: Semantic search across DWSIM documentation and historical simulation results
- **Backend API**: FastAPI endpoints for document extraction, workspace management, and RAG

**Observability Stack**
- **Logging**: Serilog sinks to console, file, and Seq (HTTP ingestion); compatible with AI services logging
- **Tracing**: OpenTelemetry exporters to Jaeger, Zipkin, or OTLP collectors
- **Metrics**: Future Prometheus /metrics endpoint

### Monitoring & Dashboard Technologies

**Not Applicable (No User-Facing Dashboard)**
- This is a headless server; monitoring is via structured logs and traces
- Operational visibility through Seq (log search) and Jaeger (trace visualization)
- Metrics exposed programmatically (not web dashboard)

## Development Environment

### Build & Development Tools

**Python:**
- **Build System**: setuptools or poetry for package building
- **Package Management**: pip (requirements.txt) or Poetry (pyproject.toml)
- **Development Workflow**: Direct execution with `python -m mcp_server`; watchdog for file watching
- **Linting**: ruff (fast, comprehensive) or flake8
- **Formatting**: black (opinionated) with isort for import sorting
- **Type Checking**: mypy with strict mode for static analysis

**.NET Framework:**
- **Build System**: MSBuild (Visual Studio project files)
- **Package Management**: NuGet for dependencies; local references for DWSIM assemblies
- **Development Workflow**: Visual Studio 2019/2022 with hot reload
- **Project Type**: Console Application (.exe) targeting .NET Framework 4.8

**Unified:**
- **EditorConfig**: Shared code style across Python and C#
- **Makefile or shell scripts**: High-level build orchestration for both stacks
- **pre-commit hooks**: Automated formatting and linting before commits

### Code Quality Tools

**Python:**
- **Static Analysis**: ruff (fast linter), pylint (comprehensive), or bandit (security)
- **Formatting**: black + isort (enforced via pre-commit hooks or CI)
- **Testing Framework**: pytest with fixtures for unit and integration tests
- **Type Checking**: mypy with `--strict` flag in CI
- **Code Coverage**: pytest-cov with coverage reports

**.NET Framework:**
- **Static Analysis**: Roslyn analyzers, Code Analysis rules in .csproj
- **Formatting**: EditorConfig + Visual Studio formatter
- **Testing Framework**: xUnit (preferred) or NUnit for unit and integration tests
- **Code Coverage**: Coverlet or OpenCover with coverage reports

**Golden-Case Testing:**
- **Approach**: Run canonical DWSIM simulation files, compare results within tolerance
- **Tools**: pytest fixtures loading sample cases with parametrization

### Version Control & Collaboration

- **VCS**: Git (GitHub or GitLab)
- **Branching Strategy**: GitHub Flow or Trunk-Based Development
  - `main` branch always deployable
  - Feature branches merged via pull requests
  - Semantic versioning tags for releases
- **Code Review Process**:
  - Mandatory PR reviews before merge
  - CI checks (build, lint, test) must pass
  - Automated security scanning (e.g., Dependabot)

### Dashboard Development

**Not Applicable** (No web dashboard; server-only application)

## Deployment & Distribution

### Target Platform(s)

**Primary: Windows 11 / Windows Server 2022**
- .NET Framework 4.8 runtime required (pre-installed on modern Windows)
- Python 3.11+ installed separately
- DWSIM assemblies bundled or referenced from installed DWSIM

**Future: Cross-Platform (Stretch Goal)**
- Linux/macOS via .NET Core + DWSIM.Core (if/when available)
- Containerized deployment (Docker)

### Distribution Method

**Initial Release:**
- **GitHub Releases**: Downloadable archives (.zip) or wheel files containing:
  - Python MCP server package (source or wheel)
  - .NET Framework engine worker (.exe + DLLs)
  - Configuration templates and documentation
- **Manual Installation**: User installs via pip, extracts worker, configures, runs

**Future Options:**
- **PyPI Package**: `dwsim-mcp-server` installable via `pip install dwsim-mcp-server`
- **NuGet Package**: .NET worker distributed as standalone tool
- **Docker Image**: Pre-configured containerized deployment
- **Installer**: MSI or setup.exe for Windows (all-in-one)

### Installation Requirements

**Prerequisites:**
- Windows 10 1809+ or Windows Server 2019+ (x64)
- .NET Framework 4.8 Developer Pack (build) or Runtime (run)
- Python 3.11+ (CPython recommended)
- DWSIM installed or assemblies available (bundled or system-wide)

**Optional:**
- Visual Studio 2019/2022 (development only)
- Seq server (logging visualization)
- pythonnet for direct .NET interop (alternative to Named Pipes)

### Update Mechanism

**Manual (Initial)**
- User downloads new release and replaces binaries
- Configuration files preserved between versions

**Future: Automated**
- `pip install --upgrade dwsim-mcp-server`
- Version check on startup with update prompt
- In-place update mechanism with migration scripts

## Technical Requirements & Constraints

### Performance Requirements

- **Tool Call Latency**:
  - Simple operations (list_objects, get_status): P95 < 500ms
  - Flowsheet modifications (add_unit, connect): P95 < 2s
  - Simulation runs (run): P95 < 30s for small cases, minutes for complex
- **Session Throughput**: Support 10+ concurrent sessions per server instance
- **Memory Footprint**:
  - Base: <100MB per session
  - Simulation: <500MB per session (typical), <2GB (max configurable)
- **Startup Time**: Server ready to accept connections within 5 seconds

### Compatibility Requirements

**Platform Support:**
- **Primary**: Windows 10 1809+ / Windows Server 2019+ (x64)
- **Future**: Windows 11, Linux (via .NET Core), macOS

**Dependency Versions:**
- **.NET Framework**: Minimum 4.8 (no earlier versions)
- **Python**: 3.11+ (recommended 3.12 for performance and modern features)
- **DWSIM**: Compatible with DWSIM 6.x+ assemblies

**Standards Compliance:**
- **MCP Specification**: Full compliance with Model Context Protocol
- **JSON-RPC 2.0**: Strict adherence to specification
- **CAPE-OPEN 1.0/1.1**: Full support for CAPE-OPEN interfaces as domain model and interop standard
- **DWSIM GPLv3**: Server distributed under GPLv3 or compatible license

### Security & Compliance

**Security Requirements:**
- **Sandboxed Execution**: Per-session working directories with allowlist-based path access
- **Resource Limits**: Configurable timeouts, memory caps, CPU quotas per session
- **No Network I/O**: DWSIM engine worker cannot make outbound network connections by default
- **Process Isolation**: Optional per-session worker processes for untrusted agents
- **Low-Privilege Execution**: Engine worker runs as low-privileged Windows user

**Threat Model:**
- **Malicious Flowsheets**: Sanitize loaded simulation files; validate structure
- **Path Traversal**: Prevent directory traversal attacks via strict allowlists
- **Resource Exhaustion**: Enforce quotas to prevent OOM or CPU starvation
- **Code Injection**: No dynamic code execution from user inputs

**Compliance Standards:**
- **GPLv3 Compliance**: Proper attribution, source distribution, license propagation
- **No PII Logging**: Structured logs sanitized to exclude sensitive data

### Scalability & Reliability

**Expected Load:**
- **Users**: Single-tenant (one MCP client per server instance initially)
- **Sessions**: 1-20 concurrent sessions per instance
- **Requests**: 10-100 tool calls per minute per session

**Availability Requirements:**
- **Uptime**: Best-effort (not HA-critical initially)
- **Graceful Degradation**: Session isolation ensures one session failure doesn't crash others
- **Crash Recovery**: Session state not persisted; user must reload cases after restart

**Growth Projections:**
- **Multi-Tenancy**: Future support for multiple MCP clients with isolated session pools
- **Horizontal Scaling**: Future load balancer distributing sessions across worker instances

## Technical Decisions & Rationale

### Decision Log

1. **Polyglot Architecture (Python + .NET Framework) over Pure C# or TypeScript**
   - **Rationale**:
     - **Python selected over TypeScript** because:
       - Target users (chemical engineers) far more familiar with Python than TypeScript/Node.js
       - Python is lingua franca in scientific/engineering communities
       - Easier extensibility: users can add custom tools, scripts, integrations
       - Better .NET interop options: pythonnet enables direct assembly access without IPC overhead
       - Sufficient I/O performance for MCP façade (not I/O-bound use case)
     - **Python selected over pure C#** because:
       - Official Python MCP SDK well-supported and idiomatic
       - C# MCP over stdio more complex (stream handling, async patterns)
       - Python async model (asyncio) simpler for MCP request-response patterns
     - .NET Framework 4.8 required for DWSIM compatibility (not .NET Core)
     - Clean separation of concerns: MCP logic vs. simulation engine
   - **Alternatives Considered**:
     - TypeScript/Node.js (rejected: unfamiliar to target users, unnecessary complexity)
     - Pure C# MCP server (rejected: immature MCP libraries, stdio complexity)
     - In-process Python.NET hosting (considered: may be viable alternative to separate worker)
   - **Trade-offs**:
     - Python slower than Node.js for extreme I/O concurrency (not relevant here)
     - Additional runtime dependency (Python + .NET vs just .NET)
     - More straightforward for users vs. potential performance edge

2. **Interop Strategy: Named Pipes/JSON-RPC with pythonnet as Alternative**
   - **Rationale**:
     - **Primary: JSON-RPC 2.0 over Named Pipes**:
       - Structured, versioned protocol with clear request/response semantics
       - Process isolation: separate .NET worker process with independent crash domain
       - Named Pipes: low-latency, secure Windows-native IPC
       - TCP fallback enables future cross-host deployment
     - **Alternative: pythonnet (Python.NET) for direct interop**:
       - Can load .NET assemblies directly into Python process
       - Zero-copy data transfer, lower latency
       - Simpler deployment (no separate worker process)
       - Trade-off: shared crash domain, harder to isolate/sandbox
   - **Alternatives Considered**:
     - gRPC (rejected: overkill for single-binary communication)
     - Raw TCP sockets (rejected: no standard framing/error handling)
     - IronPython (rejected: outdated, Python 2.7 compatibility only)
   - **Trade-offs**:
     - Named Pipes: IPC overhead, separate process management vs. isolation benefits
     - pythonnet: Performance gain, simpler deployment vs. reduced isolation
   - **Decision**: Start with Named Pipes for safety, provide pythonnet mode as option

3. **Session-Based State Model (No Persistent Sessions)**
   - **Rationale**:
     - Explicit session lifecycle prevents state leaks and resource exhaustion
     - Stateless server simplifies deployment and restarts
     - User responsible for save/load operations
   - **Alternatives Considered**:
     - Persistent sessions across restarts (rejected: complexity, stale state risks)
     - Implicit sessions (rejected: hard to manage, quota enforcement)
   - **Trade-offs**: User must explicitly load cases; no auto-recovery

4. **STA Threading for DWSIM Hosting**
   - **Rationale**:
     - DWSIM may have COM dependencies requiring Single-Threaded Apartment
     - Dedicated threads per session prevent cross-session contamination
   - **Alternatives Considered**:
     - MTA (rejected: potential COM failures)
     - Single global thread (rejected: serializes all operations)
   - **Trade-offs**: More threads = higher memory overhead

5. **Tool-Based API (Not REPL/Scripting)**
   - **Rationale**:
     - MCP tools provide typed, composable interface for agents
     - Each tool is a discrete, testable operation
     - Better error handling and observability than script execution
     - Pydantic models provide runtime validation for tool inputs
   - **Alternatives Considered**:
     - Python REPL exposed to agents (rejected: security, arbitrary code execution)
     - IronPython scripting (rejected: outdated, Python 2.7 only)
     - DWSIM GUI automation (rejected: fragile, headless incompatibility)
   - **Trade-offs**: More tools to implement vs. flexible scripting

6. **GPLv3 Licensing for Combined Work**
   - **Rationale**:
     - DWSIM is GPLv3; any distributed work linking DWSIM must be GPLv3
     - Ensures compliance and avoids license violations
     - Python code can be distributed as source or bytecode under GPLv3
   - **Alternatives Considered**:
     - Proprietary license (rejected: GPL violation)
     - AGPL (rejected: overkill for non-network service)
   - **Trade-offs**: Limits commercial licensing options for server code

7. **CAPE-OPEN as Primary Domain Model**
   - **Rationale**:
     - Industry-standard interfaces for simulation interoperability
     - Vendor-neutral: enables multi-simulator support (DWSIM, Aspen Plus, HYSYS, etc.)
     - Well-documented, structured vocabulary that LLMs can reason about effectively
     - Future-proof: allows backend simulator swapping without changing MCP tools
     - Data portability: export to CAPE-OPEN JSON, import into any compliant tool
   - **Alternatives Considered**:
     - DWSIM-specific proprietary format (rejected: vendor lock-in, no interop)
     - Generic simulation schema (rejected: reinventing the wheel, no standards body)
   - **Trade-offs**:
     - Some DWSIM features may not map perfectly to CAPE-OPEN interfaces
     - Initial complexity in mapping DTOs to CAPE-OPEN, but pays off in flexibility

8. **Integration with Artificial Intelligence Services**
   - **Rationale**:
     - Reuse mature Python AI toolkit (LLM clients, document processing, RAG, embeddings)
     - Consistent logging, configuration, and deployment patterns
     - Shared infrastructure for LLM orchestration, knowledge graphs, semantic search
     - Leverage existing FastAPI backend for API Gateway pattern
     - Common workspace management for simulation files and extracted knowledge
   - **Integration Points**:
     - LLM Client Factory: Shared OpenAI/Ollama client management
     - Document Processing: Extract simulation documentation via Docling/Unstructured
     - Graph RAG: Build knowledge graphs from DWSIM case studies and technical docs
     - Embeddings: Semantic search across DWSIM documentation and simulation results
     - Backend API: FastAPI endpoints for document extraction, workspace management
   - **Alternatives Considered**:
     - Standalone MCP server (rejected: duplicate LLM client code, no reuse)
     - Different AI toolkit (rejected: artificial_intelligence_services already deployed)
   - **Trade-offs**:
     - Additional dependency on artificial_intelligence_services package
     - More complex deployment (two Python packages) vs. single self-contained server
     - Benefit: Dramatically faster development, battle-tested components

## Known Limitations

### Current Limitations

1. **Windows-Only Deployment**
   - **Impact**: Cannot run on Linux/macOS natively
   - **Reason**: DWSIM requires .NET Framework 4.8 (Windows-only)
   - **Future Solution**: Port to .NET Core when DWSIM.Core matures

2. **No Session Persistence Across Restarts**
   - **Impact**: User loses session state if server crashes/restarts
   - **Reason**: Complexity of serializing full DWSIM flowsheet state
   - **Future Solution**: Snapshot mechanism or auto-save to disk

3. **Single-Process Worker (Initial)**
   - **Impact**: One crashed session can affect others in same process
   - **Reason**: Simpler implementation; per-session processes add overhead
   - **Future Solution**: Opt-in per-session worker mode for isolation

4. **Limited DWSIM API Coverage (MVP)**
   - **Impact**: Not all DWSIM features exposed via MCP tools initially
   - **Reason**: Staged rollout; focus on core workflows first
   - **Future Solution**: Expand tool set; add escape hatch for advanced users

5. **No Built-In Authentication**
   - **Impact**: Anyone with access to server process can issue commands
   - **Reason**: Local trust model assumed (single-user machine)
   - **Future Solution**: API key or token-based auth for multi-user scenarios

6. **Synchronous Tool Calls (No Streaming)**
   - **Impact**: Long-running simulations block until complete
   - **Reason**: MCP tool model is request-response
   - **Future Solution**: WebSocket or SSE for progress streaming
