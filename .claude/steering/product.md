# Product Overview

## Product Purpose

The DWSIM MCP Server is a Model Context Protocol (MCP) server that exposes DWSIM's powerful chemical process simulation engine to Large Language Model (LLM) agents through safe, composable tools and resources. It bridges the gap between AI agents and professional chemical engineering simulation, enabling natural language-driven process design, analysis, and optimization.

The product solves the critical challenge of making complex chemical process simulation accessible to AI agents while maintaining safety, resource control, and professional-grade capabilities. By providing a structured MCP interface over DWSIM's .NET Framework engine, it enables LLM agents to programmatically create flowsheets, configure unit operations, run simulations, and analyze results without requiring direct manipulation of DWSIM's internal APIs.

## Target Users

### Primary Users
1. **AI/LLM Developers**: Developers building AI-powered chemical engineering applications and agents that need process simulation capabilities
2. **Chemical Engineers with AI Tools**: Engineers using LLM assistants (Claude, ChatGPT, etc.) who want to leverage DWSIM through natural language interactions
3. **Process Automation Engineers**: Professionals automating simulation workflows, parameter studies, and optimization tasks using AI agents
4. **Research Scientists**: Researchers exploring AI-assisted chemical process design and optimization methodologies
5. **Educational Technology Developers**: Creators of AI-powered learning tools for chemical engineering education

### User Needs and Pain Points
- **AI-Native Interface**: Need for LLM agents to interact with DWSIM without manual GUI operation or complex COM automation
- **Safe Execution**: Requirement for sandboxed, resource-limited execution preventing runaway simulations or system compromise
- **Session Management**: Ability to manage multiple concurrent simulation sessions with proper isolation
- **Structured Data Exchange**: Clear, typed interfaces for passing simulation parameters and retrieving results
- **Observability**: Monitoring and logging of AI agent actions for debugging and auditing
- **Composability**: Breaking complex simulation tasks into discrete, chainable operations

## Key Features

1. **MCP-Compliant Interface**: Full implementation of Model Context Protocol providing standardized tools, resources, and prompts for LLM agent interaction with DWSIM

2. **Polyglot Architecture**:
   - TypeScript MCP server façade using official MCP SDK
   - .NET Framework 4.8 engine worker hosting DWSIM assemblies
   - JSON-RPC communication over Named Pipes (Windows) or TCP (cross-host)
   - Clean separation enabling independent scaling and maintenance

3. **Comprehensive Tool Taxonomy**:
   - **Session Management**: create_session, close_session, save_case, load_case
   - **Flowsheet Building**: add_compound, set_property_package, add_stream, add_unit, connect
   - **Configuration**: set_object_parameter, delete_object, list_objects
   - **Thermodynamics**: flash_tp, flash_ph, flash_ps for phase equilibrium calculations
   - **Simulation**: run, get_status, get_results for flowsheet solving
   - **Analysis**: sensitivity analysis, optimization, databank queries
   - **Export**: CSV, report generation, result extraction

4. **Resource Providers**:
   - `resource://session/{id}/results/{path}` for large result tables and plots
   - `resource://docs/{topic}` for DWSIM documentation and quick reference
   - `resource://cases/{name}` for sample flowsheets and templates

5. **Safety and Isolation**:
   - Sandboxed filesystem with allowlist-based path access
   - Execution limits: wall-clock timeouts, CPU budgets, memory caps
   - Per-session working directories with strict ACLs
   - Configurable: per-session threads or per-session processes
   - Network I/O disabled by default

6. **Session-Based Architecture**:
   - Multiple concurrent sessions with independent DWSIM contexts
   - Session state registry mapping sessionId to flowsheet context
   - Immutable inputs, explicit save/export operations
   - Clean session lifecycle with proper resource disposal

7. **Structured Error Handling**:
   - Typed error codes: BadRequest, NotFound, InvalidState, Timeout, EngineFault
   - Problem details surfaced to MCP user messages
   - Diagnostic bundles for troubleshooting

8. **Observability**:
   - Structured logging with correlation IDs (sessionId, requestId, tool name)
   - Per-request metrics: latency, success rate, engine time, memory
   - OpenTelemetry integration for tracing
   - Diagnostic snapshots on failure

## Business Objectives

- **Enable AI-Powered Chemical Engineering**: Make professional process simulation accessible to LLM agents, unlocking new workflows and capabilities
- **Ensure Safe AI Integration**: Provide secure, resource-controlled access preventing abuse while maintaining full DWSIM functionality
- **Support Developer Adoption**: Create clear, well-documented APIs enabling rapid integration into AI applications
- **Maintain DWSIM Compatibility**: Leverage DWSIM's mature engine without forking or modifying core simulation logic
- **Foster Innovation**: Enable new use cases like conversational process design, automated optimization, and AI-assisted troubleshooting
- **Comply with Licensing**: Ensure GPLv3 compliance for DWSIM while providing commercial-friendly MCP server licensing options

## Success Metrics

- **API Stability**: Low breaking change rate; semantic versioning adherence
- **Response Latency**: P95 latency < 2s for simple operations, < 30s for simulations
- **Success Rate**: >95% of valid tool calls complete successfully
- **Session Throughput**: Support 10+ concurrent sessions per server instance
- **Resource Safety**: Zero OOM crashes, zero timeout-induced hangs
- **Developer Satisfaction**: Positive feedback on API clarity and documentation
- **Adoption**: Number of downstream applications integrating the MCP server
- **Test Coverage**: >80% code coverage with golden-case validation

## Product Principles

1. **Safety First**: All exposed operations are bounded by timeouts, resource limits, and sandboxing to prevent system compromise or runaway simulations

2. **Clean Architecture**: Strict separation between MCP façade (TypeScript) and DWSIM engine (-.NET Framework) enables independent evolution and testing

3. **Explicit Over Implicit**: Session management, state transitions, and operations are explicit; no hidden side effects or ambient contexts

4. **Composable Operations**: Each tool performs a single, well-defined task; complex workflows are composed from simple operations

5. **Observable by Default**: All operations are logged with structured data; failures produce actionable diagnostics

6. **DWSIM-Native**: Leverage DWSIM's existing APIs and conventions rather than creating parallel abstractions; thin adapter layer only

7. **Standards-Based**: Adhere to MCP specification and JSON-RPC 2.0; no proprietary extensions

## Monitoring & Visibility

- **Dashboard Type**: Server-side structured logging and metrics (no end-user GUI)
- **Real-time Updates**: JSON-RPC progress events during long-running simulations; cancellation support
- **Key Metrics Displayed**:
  - Active session count and resource utilization per session
  - Tool call latency distribution (P50, P95, P99)
  - Success/failure rates by tool type
  - Engine worker health and thread status
  - Memory and CPU usage per session
  - Error codes and failure modes
- **Sharing Capabilities**:
  - Structured logs exportable to Seq, Elasticsearch, or file
  - OpenTelemetry traces for distributed debugging
  - Metrics exposed via Prometheus endpoint
  - Diagnostic bundles (logs + session snapshot) on request

## Future Vision

### Potential Enhancements

- **Streaming Results**: WebSocket or SSE for real-time simulation progress and convergence updates pushed to clients

- **Advanced Session Features**:
  - Session persistence and resume after server restart
  - Session snapshots and rollback for experimentation
  - Session forking for parallel what-if analysis
  - Collaborative sessions with multi-agent access

- **Extended Tool Coverage**:
  - Dynamic simulation controls (start, pause, step)
  - Custom script execution (sandboxed Python/IronPython)
  - Advanced optimization (multi-objective, constrained)
  - Data regression and parameter fitting

- **Cross-Platform Support**:
  - Linux server support via .NET Core/DWSIM.Core
  - Docker containerization for easy deployment
  - Kubernetes operator for autoscaling
  - Cloud-native deployments (Azure, AWS, GCP)

- **Performance Optimization**:
  - Worker process pooling for faster session startup
  - Compiled flowsheet caching
  - Incremental simulation (only recalc changed units)
  - GPU acceleration for thermodynamic calculations

- **Enterprise Features**:
  - Multi-tenancy with quotas per tenant
  - Authentication and authorization (API keys, OAuth)
  - Audit logging for compliance
  - Rate limiting and backpressure management
  - High availability and failover

- **Developer Experience**:
  - Interactive playground for testing tool calls
  - Code generation for common workflows (TypeScript, Python)
  - Visual flowsheet builder integrated with MCP tools
  - Comprehensive example library and tutorials
