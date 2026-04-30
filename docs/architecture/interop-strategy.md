<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

This file is part of the OntoLedgy Thermodynamics Architecture and is
dual-licensed:

  1. Open source under the GNU Affero General Public License v3.0 or
     later (AGPL-3.0-or-later). See the LICENSE file in the repository
     root for the full licence text and NOTICE for attribution.
  2. Commercial under a separate proprietary licence offered by
     OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

# Interop Strategy: Architectural Decision Record

**Date**: 2026-01-08
**Status**: Approved
**Decision Makers**: Architecture Review

## Context

The DWSIM MCP Server requires bridging two distinct technology stacks:
- **DWSIM Engine**: Requires .NET Framework 4.8 (Windows-only legacy runtime)
- **MCP Protocol**: Needs to expose tools/resources to LLM agents via stdio

We evaluated three architectural approaches to enable this interoperability.

## Decision

**Selected Approach: Python MCP Server with pythonnet (In-Process Interop)**

Use Python's official MCP SDK for the server façade with pythonnet for direct in-process loading of .NET Framework 4.8 assemblies.

## Alternatives Considered

### Option 1: Python MCP Server + pythonnet (SELECTED)

**Architecture:**
```
┌─────────────────────────────────────┐
│  LLM Agent (Claude, etc.)           │
└──────────────┬──────────────────────┘
               │ MCP Protocol (stdio)
┌──────────────▼──────────────────────┐
│  Python MCP Server (Python 3.11+)   │
│  ┌────────────────────────────────┐ │
│  │ MCP Tools (Official SDK)       │ │
│  │ pythonnet (.NET interop)       │ │
│  └──────────┬─────────────────────┘ │
│             │ In-process calls      │
│  ┌──────────▼─────────────────────┐ │
│  │ DwsimWorker (C# Class Library) │ │
│  │ SessionManager, Adapters       │ │
│  │ DWSIM Engine                   │ │
│  └────────────────────────────────┘ │
└─────────────────────────────────────┘
Single Process
```

**Pros:**
- ✅ **Single process**: Simplest deployment and debugging
- ✅ **Zero IPC overhead**: Direct method calls, no serialization
- ✅ **Mature MCP SDK**: Python SDK is production-ready and well-documented
- ✅ **Target audience fit**: Chemical engineers predominantly use Python
- ✅ **Simple deployment**: Only .NET Framework 4.8 runtime required
- ✅ **Extensibility**: Users can easily add custom Python tools/scripts
- ✅ **Rich ecosystem**: Access to Python AI/ML libraries (numpy, pandas, scipy)
- ✅ **Fastest development**: Thin Python layer over existing C# code
- ✅ **Performance**: No serialization, no network latency

**Cons:**
- ⚠️ **Shared crash domain**: Python or C# exception can crash entire process
- ⚠️ **pythonnet dependency**: Requires pythonnet package (mature but additional dep)
- ⚠️ **Mixed language debugging**: Requires debugging across Python/C# boundary

**Risk Mitigation:**
- Comprehensive exception handling in C# adapters
- Python try/catch wrapping all C# calls
- Graceful degradation and error reporting
- Extensive integration testing

---

### Option 2: All C# with IPC (.NET 10 MCP Server + .NET Framework 4.8 Worker)

**Architecture:**
```
┌────────────────────────────────────────┐
│  .NET 10 MCP Server (C# MCP SDK)       │
│  ┌──────────────────────────────────┐  │
│  │ MCP Tools (Attribute-based)      │  │
│  │ JSON-RPC Client                  │  │
│  └───────────┬──────────────────────┘  │
└─────────────┼──────────────────────────┘
              │ Named Pipes / JSON-RPC
┌─────────────▼──────────────────────────┐
│  .NET Framework 4.8 Worker             │
│  ┌──────────────────────────────────┐  │
│  │ SessionManager, DWSIM Engine     │  │
│  └──────────────────────────────────┘  │
└────────────────────────────────────────┘
Two Processes
```

**Pros:**
- ✅ **Full C# stack**: Single language throughout
- ✅ **Process isolation**: Worker crash doesn't affect MCP server
- ✅ **Type safety**: C# type system across entire stack
- ✅ **Official C# MCP SDK**: Attribute-based tool definitions

**Cons:**
- ❌ **IPC overhead**: Named Pipes + JSON serialization latency
- ❌ **Wrong audience**: Chemical engineers expect Python, not C#
- ❌ **Complex deployment**: Requires .NET Framework 4.8 + .NET 10 runtimes
- ❌ **Preview SDK**: C# MCP SDK is in preview (API may change)
- ❌ **Two processes**: More complex debugging and deployment
- ❌ **Limited extensibility**: Users must write C# to extend

**Why Rejected:**
- C# MCP SDK doesn't eliminate IPC complexity (still need two processes)
- Target users (chemical engineers) are Python-native
- Adds deployment complexity (two .NET runtimes)
- Python + pythonnet achieves same goals with simpler architecture

---

### Option 3: Python MCP Server + JSON-RPC over Named Pipes (Original Plan)

**Architecture:**
```
┌───────────────────────────────────────┐
│  Python MCP Server (Python 3.11+)    │
│  ┌────────────────────────────────┐  │
│  │ MCP Tools (Official SDK)       │  │
│  │ JSON-RPC Client                │  │
│  └──────────┬─────────────────────┘  │
└─────────────┼─────────────────────────┘
              │ Named Pipes / JSON-RPC
┌─────────────▼─────────────────────────┐
│  .NET Framework 4.8 Worker (C#)       │
│  ┌────────────────────────────────┐  │
│  │ JSON-RPC Server                │  │
│  │ SessionManager, DWSIM Engine   │  │
│  └────────────────────────────────┘  │
└───────────────────────────────────────┘
Two Processes
```

**Pros:**
- ✅ **Process isolation**: Worker crash doesn't affect MCP server
- ✅ **Clear separation**: Well-defined IPC contract
- ✅ **Python for users**: Right language for chemical engineers
- ✅ **Mature Python SDK**: Production-ready

**Cons:**
- ❌ **IPC overhead**: Named Pipes + JSON serialization adds latency
- ❌ **Complex deployment**: Two processes to manage
- ❌ **More code**: JSON-RPC server/client implementation required
- ❌ **Cross-process debugging**: Harder to troubleshoot

**Why Rejected:**
- pythonnet achieves same benefits without IPC overhead
- Simpler deployment (single process)
- Better performance (no serialization)
- Process isolation not critical for trusted local agent use case

---

## Decision Rationale

### Primary Factors

1. **Target Audience**: Chemical engineers are Python-native
   - Industry standard tools (DWSIM, Aspen) have Python interfaces
   - Simulation workflows commonly use Python notebooks
   - Python is lingua franca in scientific/engineering communities

2. **Architecture Simplicity**: Single process is significantly simpler
   - No IPC layer to implement (Named Pipes, JSON-RPC)
   - No process management complexity
   - Easier debugging (single process, single debugger)
   - Simpler deployment (one runtime, one executable flow)

3. **Performance**: In-process is fastest
   - Zero IPC overhead
   - No JSON serialization/deserialization
   - Direct method invocation
   - Lower memory footprint

4. **Development Velocity**: Fastest path to MVP
   - Python MCP SDK is mature and well-documented
   - pythonnet is proven technology (used in production systems)
   - Thin Python layer over existing C# code
   - Less code to write and test

5. **MCP SDK Maturity**: Python SDK is production-ready
   - Official SDK from Anthropic
   - Extensive documentation and examples
   - Active community support
   - C# SDK is preview (API may change)

### Secondary Factors

6. **Extensibility**: Users can add custom Python tools
   - Python scripts for custom workflows
   - Integration with Python AI/ML libraries
   - Easy prototyping and experimentation

7. **Ecosystem Integration**: Python has rich AI/ML ecosystem
   - numpy, pandas, scipy for data analysis
   - LangChain, LlamaIndex for LLM orchestration
   - Docling, Unstructured for document processing
   - Integration with `artificial_intelligence_services` toolkit

8. **Deployment Simplicity**: Single .NET runtime
   - Only .NET Framework 4.8 required (pre-installed on Windows)
   - No .NET 10 runtime needed
   - Simpler dependency management

### Trade-offs Accepted

- **Shared crash domain**: Acceptable for trusted local agent use case
  - Comprehensive exception handling mitigates risk
  - Proper C# dispose patterns prevent resource leaks
  - Extensive testing validates stability

- **Mixed-language stack**: Acceptable given target audience
  - Python is user-facing (right choice for audience)
  - C# is internal implementation detail
  - Clear separation at pythonnet boundary

- **pythonnet dependency**: Acceptable mature library
  - Widely used in production systems
  - Active maintenance and community
  - Well-documented API

## Implementation Strategy

### Phase 1: C# Worker as Class Library

**Current State**: DwsimWorker is a console application (Exe)
**Required Change**: Convert to class library (Dll)

```xml
<!-- DwsimWorker.csproj -->
<PropertyGroup>
  <OutputType>Library</OutputType>  <!-- Change from Exe -->
  <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
</PropertyGroup>
```

**Public API Surface:**
- `SessionManager` class with public methods
- Adapter classes (StreamAdapter, CalculationAdapter, etc.)
- DTO classes (MaterialStreamDto, CalculationResult, etc.)
- Exception types

### Phase 2: Python MCP Server Development

**Structure:**
```
mcp_service/server/
├── dwsim_mcp_server/
│   ├── __init__.py
│   ├── server.py           # MCP server entry point
│   ├── tools/              # MCP tool implementations
│   │   ├── session_tools.py
│   │   ├── flowsheet_tools.py
│   │   ├── simulation_tools.py
│   │   └── thermodynamic_tools.py
│   ├── interop/            # pythonnet bridge
│   │   ├── clr_loader.py   # .NET assembly loading
│   │   └── session_client.py  # SessionManager wrapper
│   └── models/             # Pydantic models
│       └── dto_models.py
├── pyproject.toml
└── README.md
```

**Key Components:**

1. **CLR Loader** (`clr_loader.py`):
```python
import clr
import sys

def load_dwsim_worker(assembly_path: str):
    """Load DwsimWorker.dll and return SessionManager"""
    sys.path.append(assembly_path)
    clr.AddReference("DwsimWorker")
    from DwsimWorker.Engine import SessionManager
    return SessionManager()
```

2. **MCP Tools** (`session_tools.py`):
```python
from mcp.server import Server
from mcp.types import Tool

@server.call_tool()
async def create_session(name: str | None = None) -> str:
    """Create a new DWSIM simulation session"""
    session_id = session_manager.CreateSession(name or "default")
    return session_id
```

3. **Error Handling**:
```python
try:
    result = session_manager.RunCalculation(session_id)
except Exception as ex:
    # Convert C# exceptions to MCP errors
    raise McpError(f"Calculation failed: {str(ex)}")
```

### Phase 3: Integration Testing

**Test Scenarios:**
1. Load DwsimWorker.dll via pythonnet
2. Create session, configure flowsheet, run calculation
3. Extract results and verify mass balance
4. Test exception propagation from C# to Python
5. Memory leak testing (session create/dispose cycles)

### Phase 4: Optional Named Pipes Mode

**Future Enhancement**: Add Named Pipes as opt-in mode for:
- High-security scenarios requiring process isolation
- Remote worker scenarios (cross-host)
- Worker restart without MCP server restart

**Implementation**: Make interop mode configurable:
```python
# config.yaml
interop:
  mode: pythonnet  # or "named_pipes"
  named_pipes:
    pipe_name: "dwsim_worker_pipe"
```

## Migration Path from Original Plan

### Original Plan Phases → New Plan

**Phase 3: RPC Infrastructure (ELIMINATED)**
- ~~Spec 3.1: JSON-RPC Server over Named Pipes (C# Side)~~
- ~~Spec 3.2: Core DWSIM Operations as JSON-RPC Methods~~

**Phase 4: Python MCP Server (SIMPLIFIED)**
- ~~Spec 4.1: JSON-RPC Client (Python Side)~~ → **Use pythonnet instead**
- Spec 4.2: Pydantic DTOs (UNCHANGED)
- Spec 4.3: Python Service Layer (SIMPLIFIED - direct C# calls, no RPC client)

**Result:**
- **2 specs eliminated** (3.1, 3.2)
- **1 spec simplified** (4.1 → pythonnet loader)
- **Estimated time saved**: 1-2 weeks

## Success Criteria

The pythonnet approach will be considered successful if:

1. **Performance**: Tool call latency P95 < 500ms for simple operations
2. **Stability**: No memory leaks after 100+ session create/dispose cycles
3. **Exception Handling**: C# exceptions properly caught and reported to Python
4. **Resource Management**: Proper disposal of DWSIM objects on session close
5. **Testing**: >80% code coverage with integration tests
6. **User Experience**: Chemical engineers can extend with custom Python tools

## Monitoring & Validation

**Metrics to Track:**
- Tool call latency (P50, P95, P99)
- Memory usage per session
- Exception rates (C# exceptions, Python exceptions)
- Session lifecycle (create, use, dispose)

**Validation Tests:**
- Three-phase separator workflow (golden test)
- Concurrent session isolation
- Memory leak detection (profiler)
- Exception propagation scenarios

## References

- [pythonnet documentation](https://pythonnet.github.io/)
- [Python MCP SDK](https://github.com/anthropics/python-sdk)
- [DWSIM Open Source](https://github.com/DanWBR/dwsim)
- [C# MCP SDK Blog Post](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)

## Revision History

| Date | Version | Changes |
|------|---------|---------|
| 2026-01-08 | 1.0 | Initial decision: Python + pythonnet approach selected |
