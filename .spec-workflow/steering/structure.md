# Project Structure

## Directory Organization

The DWSIM MCP Server is a polyglot project with Python (MCP server) and C# (.NET Framework worker) components. The directory structure reflects this dual-language architecture with clear separation between the MCP façade and simulation engine.

```
dwsim_interop_services/             # Project root (OntoLedgy AI services convention)
├── models/                         # Shared object models (one file per class)
│   ├── __init__.py
│   ├── cape_open/                  # CAPE-OPEN standard models
│   │   ├── __init__.py
│   │   ├── material_stream.py      # MaterialStream model (ICapeThermoMaterialObject)
│   │   ├── thermo_property_package.py  # PropertyPackage model
│   │   ├── unit_operation.py       # UnitOperation base model
│   │   ├── compound.py             # Compound model (ICapeThermoCompounds)
│   │   ├── phase.py                # Phase model (ICapeThermoPhases)
│   │   └── reaction.py             # Reaction model
│   ├── dwsim/                      # DWSIM-specific application models
│   │   ├── __init__.py
│   │   ├── flowsheet.py            # Flowsheet model
│   │   ├── session.py              # Session model
│   │   ├── simulation_result.py    # SimulationResult model
│   │   ├── property_package_config.py  # Property package configuration
│   │   └── solver_config.py        # Solver configuration
│   ├── requests/                   # Request DTOs (one per file)
│   │   ├── __init__.py
│   │   ├── create_session_request.py
│   │   ├── add_stream_request.py
│   │   ├── add_unit_request.py
│   │   ├── run_simulation_request.py
│   │   └── flash_request.py
│   ├── responses/                  # Response DTOs (one per file)
│   │   ├── __init__.py
│   │   ├── create_session_response.py
│   │   ├── simulation_result_response.py
│   │   ├── stream_properties_response.py
│   │   └── flash_result_response.py
│   └── errors/                     # Error models (one per file)
│       ├── __init__.py
│       ├── session_error.py
│       ├── simulation_error.py
│       └── validation_error.py
│
├── mcp_service/                    # MCP service (server + worker)
│   ├── server/                     # Python MCP server
│   │   ├── dwsim_mcp_server/       # Main Python package
│   │   │   ├── __init__.py
│   │   │   ├── server.py           # MCP server bootstrap
│   │   │   ├── tools/              # MCP tool implementations
│   │   │   │   ├── __init__.py
│   │   │   │   ├── session.py      # Session management tools
│   │   │   │   ├── flowsheet.py    # Flowsheet building tools
│   │   │   │   ├── simulation.py   # Simulation execution tools
│   │   │   │   ├── analysis.py     # Analysis tools
│   │   │   │   └── export.py       # Export tools
│   │   │   ├── resources/          # MCP resource providers
│   │   │   │   ├── __init__.py
│   │   │   │   ├── docs.py         # Documentation resources
│   │   │   │   ├── samples.py      # Sample cases
│   │   │   │   └── results.py      # Session result resources
│   │   │   ├── ipc/                # Inter-process communication
│   │   │   │   ├── __init__.py
│   │   │   │   ├── client.py       # JSON-RPC client for Named Pipes
│   │   │   │   └── pythonnet_bridge.py  # Optional pythonnet bridge
│   │   │   ├── converters/         # Model converters
│   │   │   │   ├── __init__.py
│   │   │   │   ├── cape_open_converter.py  # CAPE-OPEN <-> DTO
│   │   │   │   └── dwsim_converter.py      # DWSIM <-> DTO
│   │   │   ├── config/             # Configuration management
│   │   │   │   ├── __init__.py
│   │   │   │   ├── settings.py     # Server settings (Pydantic)
│   │   │   │   └── logging.py      # Logging configuration
│   │   │   └── observability/      # Observability utilities
│   │   │       ├── __init__.py
│   │   │       ├── logging.py      # Structured logging
│   │   │       ├── tracing.py      # OpenTelemetry tracing
│   │   │       └── metrics.py      # Metrics collection
│   │   ├── tests/                  # Python tests
│   │   │   ├── unit/               # Unit tests
│   │   │   ├── integration/        # Integration tests
│   │   │   └── fixtures/           # Test fixtures and data
│   │   ├── pyproject.toml          # Python package config (Poetry)
│   │   ├── requirements.txt        # Python dependencies (pip)
│   │   ├── setup.py                # Package setup script
│   │   └── README.md               # Python package documentation
│   │
│   └── dwsim_worker/               # .NET Framework engine worker
│       ├── DwsimWorker/            # Console application project
│       │   ├── Program.cs          # Entry point
│       │   ├── App.config          # Configuration and binding redirects
│       │   ├── DwsimWorker.csproj  # Project file
│       │   ├── IPC/                # IPC server components (one file per class)
│       │   │   ├── NamedPipeServer.cs  # Named Pipe server
│       │   │   ├── JsonRpcDispatcher.cs # JSON-RPC request dispatcher
│       │   │   └── MessageHandlers.cs  # Request/response handlers
│       │   ├── Engine/             # DWSIM engine hosting (one file per class)
│       │   │   ├── EngineHost.cs   # Lifecycle and STA thread management
│       │   │   ├── SessionManager.cs # Session registry
│       │   │   └── SessionContext.cs # Per-session state
│       │   ├── Adapters/           # DWSIM API adapters (one file per class)
│       │   │   ├── FlowsheetAdapter.cs # Flowsheet operations
│       │   │   ├── StreamAdapter.cs    # Material stream operations
│       │   │   ├── UnitOpAdapter.cs    # Unit operation operations
│       │   │   └── PropertyAdapter.cs  # Property calculations
│       │   ├── Converters/         # Model converters (one file per class)
│       │   │   ├── CapeOpenConverter.cs  # CAPE-OPEN <-> models
│       │   │   ├── DwsimConverter.cs     # DWSIM <-> models
│       │   │   └── JsonSerializer.cs     # JSON serialization
│       │   ├── Limits/             # Resource limits and quotas (one file per class)
│       │   │   ├── TimeoutManager.cs   # Timeout enforcement
│       │   │   ├── MemoryMonitor.cs    # Memory quota tracking
│       │   │   └── QuotaEnforcer.cs    # General quota enforcement
│       │   └── Utilities/          # Helper utilities (one file per class)
│       │       ├── Logging.cs      # Serilog configuration
│       │       └── ValidationHelper.cs # Validation utilities
│       ├── DwsimWorker.Tests/      # C# unit tests (xUnit)
│       │   ├── AdapterTests.cs
│       │   ├── SessionTests.cs
│       │   └── IntegrationTests.cs
│       ├── DwsimWorker.sln         # Visual Studio solution
│       └── README.md               # .NET worker documentation
│
├── integration-tests/              # End-to-end integration tests
│   ├── golden-cases/               # Golden DWSIM simulation files
│   ├── test_scenarios.py           # pytest scenarios
│   └── README.md
│
├── deployments/                    # Deployment configurations
│   ├── docker/                     # Docker configurations
│   │   ├── Dockerfile.mcp-server   # Python MCP server image
│   │   ├── Dockerfile.worker       # .NET worker image
│   │   └── docker-compose.yml      # Multi-container setup
│   ├── kubernetes/                 # K8s manifests (future)
│   └── systemd/                    # systemd service files (Linux)
│
├── docs/                           # Project documentation
│   ├── api/                        # API documentation
│   │   ├── mcp-tools.md            # MCP tool reference
│   │   ├── cape-open.md            # CAPE-OPEN integration guide
│   │   └── ipc-protocol.md         # IPC protocol specification
│   ├── guides/                     # User guides
│   │   ├── quickstart.md
│   │   ├── configuration.md
│   │   └── troubleshooting.md
│   └── architecture/               # Architecture documentation
│       ├── overview.md
│       ├── security.md
│       └── observability.md
│
├── .github/                        # GitHub configuration
│   ├── workflows/                  # CI/CD workflows
│   │   ├── python-ci.yml           # Python linting/testing
│   │   ├── dotnet-ci.yml           # .NET build/test
│   │   └── integration.yml         # Integration tests
│   └── ISSUE_TEMPLATE/             # Issue templates
│
├── .editorconfig                   # Editor configuration (Python + C#)
├── .gitignore                      # Git ignore rules
├── LICENSE                         # GPLv3 license
└── README.md                       # Project README
```

## Naming Conventions

### Python (MCP Server)

**Files and Modules:**
- **Modules/Packages**: `snake_case` (e.g., `session_manager.py`, `cape_open/`)
- **Tools**: `{domain}.py` (e.g., `flowsheet.py`, `simulation.py`)
- **Tests**: `test_{module}.py` (e.g., `test_session_manager.py`)
- **Configuration**: Descriptive names (e.g., `settings.py`, `logging.py`)

**Code:**
- **Classes/Types**: `PascalCase` (e.g., `SessionManager`, `MaterialStreamDTO`)
  - **One file per class rule**: Each class must be in its own file for improved readability and maintainability
  - File name matches class name in snake_case (e.g., `session_manager.py` for `SessionManager`)
- **Functions/Methods**: `snake_case` (e.g., `create_session`, `run_simulation`)
- **Constants**: `UPPER_SNAKE_CASE` (e.g., `DEFAULT_TIMEOUT_MS`, `MAX_SESSIONS`)
- **Private members**: Leading underscore `_private_method`, `_internal_var`
- **Type variables**: `T`, `TRequest`, `TResponse` (PascalCase with `T` prefix)

**Pydantic Models:**
- Request DTOs: `{Action}Request` (e.g., `CreateSessionRequest`, `RunSimulationRequest`)
- Response DTOs: `{Action}Response` (e.g., `CreateSessionResponse`, `SimulationResult`)
- Domain models: `{Entity}` (e.g., `MaterialStream`, `UnitOperation`)

### C# (.NET Worker)

**Files and Namespaces:**
- **Namespaces**: `DwsimWorker.{Area}` (e.g., `DwsimWorker.Engine`, `DwsimWorker.Adapters`)
- **Files**: `PascalCase.cs` (e.g., `SessionManager.cs`, `FlowsheetAdapter.cs`)
- **Tests**: `{Class}Tests.cs` (e.g., `SessionManagerTests.cs`)
- **Interfaces**: `I{Name}.cs` (e.g., `ISessionManager.cs`, `IAdapter.cs`)

**Code:**
- **Classes/Interfaces**: `PascalCase` (e.g., `SessionManager`, `IEngineHost`)
  - **One file per class rule**: Each class or interface must be in its own file for improved readability and maintainability
  - File name matches class/interface name exactly (e.g., `SessionManager.cs`, `IEngineHost.cs`)
- **Methods/Properties**: `PascalCase` (e.g., `CreateSession`, `SessionId`)
- **Fields**: `_camelCase` with underscore prefix (e.g., `_sessions`, `_dwsimEngine`)
- **Constants**: `PascalCase` or `UPPER_SNAKE_CASE` (e.g., `DefaultTimeout`, `MAX_RETRIES`)
- **Local variables**: `camelCase` (e.g., `sessionId`, `flowsheet`)

**DTOs and Contracts:**
- Request classes: `{Action}Request` (e.g., `CreateSessionRequest`)
- Response classes: `{Action}Response` (e.g., `CreateSessionResponse`)
- Error classes: `{Error}Error` (e.g., `TimeoutError`, `NotFoundError`)

### CAPE-OPEN Naming

Follow CAPE-OPEN interface conventions:
- Interface names: `ICape{Domain}{Type}` (e.g., `ICapeThermoMaterialObject`)
- Method names: `PascalCase` per COM conventions (e.g., `SetProp`, `GetProp`)
- Property names: Standard CAPE-OPEN names (e.g., `"temperature"`, `"pressure"`, `"phase"`)

## Import Patterns

### Python Import Order

1. **Standard library imports** (sorted alphabetically)
2. **Third-party imports** (sorted alphabetically)
3. **Local application imports** (sorted alphabetically)
4. **Relative imports** (if needed, discouraged)

**Example:**
```python
# Standard library
import asyncio
import logging
from pathlib import Path
from typing import Any, Dict, Optional

# Third-party
from pydantic import BaseModel, Field
import structlog

# Shared models (from models/)
from models.cape_open.material_stream import MaterialStream
from models.requests.create_session_request import CreateSessionRequest
from models.responses.create_session_response import CreateSessionResponse

# Local application (from mcp_service/server/)
from dwsim_mcp_server.ipc.client import IPCClient
from dwsim_mcp_server.config.settings import Settings
```

**Import Style:**
- Use absolute imports from package root: `from dwsim_mcp_server.tools import session`
- Avoid wildcard imports: Never `from module import *`
- Import specific items: `from module import Foo, Bar` (not `import module` everywhere)
- Type-only imports: Use `from typing import TYPE_CHECKING` for circular dependencies

### C# Using Directives Order

1. **System namespaces** (sorted)
2. **Third-party namespaces** (sorted)
3. **DWSIM namespaces** (sorted)
4. **Local project namespaces** (sorted)

**Example:**
```csharp
// System namespaces
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

// Third-party
using Newtonsoft.Json;
using Serilog;

// DWSIM
using DWSIM.Interfaces;
using DWSIM.Thermodynamics;

// Local project
using DwsimWorker.Contracts;
using DwsimWorker.Engine;
```

## Code Structure Patterns

### Python Module Organization

Standard ordering within Python files:

1. **Module docstring** ("""Description""")
2. **Imports** (standard, third-party, local)
3. **Module-level constants**
4. **Type aliases and type variables**
5. **Pydantic models / dataclasses**
6. **Exception classes**
7. **Main implementation classes**
8. **Module-level functions**
9. **Private helper functions**
10. **Entry point** (`if __name__ == "__main__"` if applicable)

**Example Structure:**
```python
"""Session management tools for DWSIM MCP Server."""

import logging
from typing import Optional
from pydantic import BaseModel

# Module constants
DEFAULT_TEMP_DIR = "/tmp/dwsim_sessions"
MAX_SESSION_LIFETIME_HOURS = 24

# Pydantic models
class CreateSessionRequest(BaseModel):
    name: Optional[str] = None
    temp_dir: Optional[str] = None

# Main implementation
class SessionManager:
    def __init__(self, ipc_client: IPCClient):
        self._ipc = ipc_client
        self._logger = logging.getLogger(__name__)

    async def create_session(self, request: CreateSessionRequest) -> str:
        """Create a new DWSIM simulation session."""
        # Implementation
        pass

# Helper functions
def _validate_temp_dir(path: str) -> bool:
    """Validate temporary directory path."""
    pass
```

### C# Class Organization

Standard ordering within C# files:

1. **File header comments** (if required)
2. **Using directives**
3. **Namespace declaration**
4. **Class documentation** (/// XML comments)
5. **Constants and static readonly fields**
6. **Private fields**
7. **Constructors**
8. **Properties**
9. **Public methods**
10. **Protected methods**
11. **Private methods**
12. **Nested types**

**Example Structure:**
```csharp
using System;
using DwsimWorker.Contracts;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Manages DWSIM simulation sessions with lifecycle control.
    /// </summary>
    public sealed class SessionManager
    {
        // Constants
        private const int MaxConcurrentSessions = 20;

        // Fields
        private readonly ConcurrentDictionary<string, Session> _sessions;
        private readonly ILogger _logger;

        // Constructor
        public SessionManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessions = new ConcurrentDictionary<string, Session>();
        }

        // Properties
        public int ActiveSessionCount => _sessions.Count;

        // Public methods
        public string CreateSession(string? name = null)
        {
            // Implementation
        }

        // Private methods
        private void CleanupExpiredSessions()
        {
            // Implementation
        }
    }
}
```

## Shared Models Folder

The `models/` folder at the project root contains all shared object models that facilitate interoperability between:
1. **Python MCP server** and **C# engine worker** (via JSON serialization)
2. **LLM agents** (via MCP tool schemas and CAPE-OPEN vocabulary)
3. **External systems** (via CAPE-OPEN standard interfaces)

### Models Organization Principles

**One File Per Class Rule:**
- Each model class must be in its own dedicated file
- File names in snake_case matching the class name (Python convention)
- Improves code readability, maintainability, and git history tracking

**Model Categories:**

1. **CAPE-OPEN Models** (`models/cape_open/`):
   - Standard CAPE-OPEN interface implementations
   - Vendor-neutral, interoperable across simulation tools
   - Examples: `material_stream.py`, `thermo_property_package.py`, `compound.py`

2. **DWSIM Application Models** (`models/dwsim/`):
   - DWSIM-specific models not covered by CAPE-OPEN
   - Examples: `flowsheet.py`, `session.py`, `simulation_result.py`

3. **Request/Response DTOs** (`models/requests/`, `models/responses/`):
   - Pydantic models for MCP tool inputs/outputs
   - Validated, typed, JSON-serializable
   - Examples: `create_session_request.py`, `simulation_result_response.py`

4. **Error Models** (`models/errors/`):
   - Structured error types with codes and messages
   - Examples: `session_error.py`, `simulation_error.py`

### Benefits of Centralized Models

- **Single Source of Truth**: Models defined once, used everywhere
- **Type Safety**: Pydantic validation in Python, DataAnnotations in C#
- **Discoverability**: LLMs can explore `models/` to understand domain vocabulary
- **Versioning**: Models folder can be versioned independently
- **Testing**: Shared test fixtures for models in `models/tests/`

## Code Organization Principles

### 1. Single Responsibility Principle

- **One concern per file**: Each module/class has one clear, well-defined responsibility
- **One class per file**: Enforced across all models, services, and adapters
- **Tool files**: One MCP tool per file (e.g., `session.py` only has session management tools)
- **Adapter files**: One DWSIM domain per adapter (e.g., `StreamAdapter.cs` only handles streams)
- **Model files**: One model class per file in `models/` folder

### 2. Dependency Inversion

- **Depend on abstractions**: Python tools depend on `IPCClient` interface, not concrete implementation
- **Inject dependencies**: Pass `IPCClient`, loggers, settings via constructor/parameters
- **Avoid global state**: No module-level mutable state; everything passed explicitly

### 3. Layered Architecture

**Python MCP Server Layers:**
```
┌─────────────────────────────────────┐
│  MCP Protocol Layer (server.py)    │  ← Entry point, stdio handling
├─────────────────────────────────────┤
│  Tools Layer (tools/*.py)           │  ← MCP tool implementations
├─────────────────────────────────────┤
│  Domain Layer (cape_open/*.py)      │  ← CAPE-OPEN domain models
├─────────────────────────────────────┤
│  IPC Layer (ipc/*.py)               │  ← Communication with worker
└─────────────────────────────────────┘
```

**C# Engine Worker Layers:**
```
┌─────────────────────────────────────┐
│  IPC Layer (IPC/*.cs)               │  ← Named Pipe server, JSON-RPC
├─────────────────────────────────────┤
│  Adapter Layer (Adapters/*.cs)      │  ← DWSIM API wrappers
├─────────────────────────────────────┤
│  Domain Layer (CapeOpen/*.cs)       │  ← CAPE-OPEN mappers
├─────────────────────────────────────┤
│  Engine Layer (Engine/*.cs)         │  ← DWSIM engine hosting
└─────────────────────────────────────┘
```

### 4. Testability

- **Pure functions**: Prefer pure functions with no side effects where possible
- **Mockable dependencies**: All external dependencies (IPC, logging) passed as interfaces
- **Test fixtures**: Shared test data in `tests/fixtures/`
- **Integration tests**: Separate `integration-tests/` directory for end-to-end scenarios

## Module Boundaries

### Python MCP Server Boundaries

**Public API (Entry Points):**
- `server.py`: Main MCP server entry point
- `tools/*`: MCP tool implementations (registered with MCP SDK)
- `resources/*`: MCP resource providers

**Internal Modules:**
- `ipc/`: IPC client implementation (private)
- `cape_open/`: Domain models (internal, DTO converters only)
- `config/`: Configuration management (private)
- `observability/`: Logging, tracing (private)

**Dependency Rules:**
- Tools may depend on: IPC client, domain models, config, observability
- IPC may depend on: Domain models (DTOs), config
- Domain models: No dependencies (pure data models with Pydantic validation)
- Server: Depends on tools, resources, config, observability

### C# Engine Worker Boundaries

**Public API (IPC Interface):**
- `IPC/JsonRpcDispatcher.cs`: Handles all incoming requests
- `Contracts/`: Defines request/response DTOs (shared with Python via JSON schema)

**Internal Modules:**
- `Engine/`: DWSIM engine hosting (private)
- `Adapters/`: DWSIM API wrappers (private)
- `CapeOpen/`: CAPE-OPEN mappers (private)
- `Limits/`: Resource enforcement (private)

**Dependency Rules:**
- IPC layer may depend on: Contracts, Engine, Adapters
- Adapters may depend on: DWSIM assemblies, CapeOpen mappers, Contracts
- Engine may depend on: DWSIM assemblies, Limits
- CapeOpen: Depends on DWSIM assemblies, Contracts
- Contracts: No dependencies (pure DTOs)

### Cross-Boundary Communication

**Python ↔ C# Communication:**
- **Protocol**: JSON-RPC 2.0 over Named Pipes (or pythonnet for direct interop)
- **DTOs**: Shared JSON schema defined in `Contracts/` (C#) and `ipc/dto.py` (Python)
- **Validation**: Pydantic in Python, DataAnnotations in C#
- **Error handling**: Structured error codes and messages (both sides)

## Code Size Guidelines

### File Size

- **Python modules**: Aim for < 500 lines; split if exceeding 800 lines
- **C# classes**: Aim for < 400 lines; split if exceeding 600 lines
- **Test files**: Can be larger; group related tests together

### Function/Method Size

- **Python functions**: Aim for < 30 lines; split if exceeding 50 lines
- **C# methods**: Aim for < 25 lines; split if exceeding 40 lines
- **Exception**: Constructors and property accessors can be minimal

### Complexity

- **Cyclomatic complexity**: Aim for < 10 per function/method
- **Nesting depth**: Maximum 3 levels (avoid deep nesting with early returns)
- **Parameters**: Maximum 5 parameters (use DTOs/models for more)

### When to Split

**Split a module when:**
- File exceeds size guidelines
- Module has multiple unrelated responsibilities
- Testing becomes difficult due to complexity
- Code review comments suggest splitting

**Extraction strategies:**
- Extract helper functions to separate `_helpers.py` or `Utilities.cs`
- Extract related classes to dedicated modules
- Extract domain models to `models/` subdirectory
- Extract test fixtures to `conftest.py` or `TestHelpers.cs`

## Documentation Standards

### Python Documentation

**Module docstrings:**
```python
"""
Brief one-line description.

More detailed multi-paragraph description if needed. Explain purpose,
key concepts, and usage examples.

Example:
    >>> from dwsim_mcp_server.tools import session
    >>> manager = SessionManager(ipc_client)
    >>> session_id = await manager.create_session()
"""
```

**Function/Method docstrings (Google style):**
```python
def create_session(self, name: Optional[str] = None) -> str:
    """Create a new DWSIM simulation session.

    Args:
        name: Optional session name for identification.

    Returns:
        Unique session ID string.

    Raises:
        MaxSessionsError: If maximum concurrent sessions exceeded.
        WorkerConnectionError: If engine worker is unavailable.
    """
```

**Type hints required:**
- All public functions/methods must have type hints
- Use `Optional[T]` for nullable types
- Use `Union[A, B]` for alternatives
- Use `TypeVar` for generics

### C# Documentation

**XML documentation comments:**
```csharp
/// <summary>
/// Creates a new DWSIM simulation session with isolated state.
/// </summary>
/// <param name="name">Optional session name for identification.</param>
/// <returns>Unique session ID as a string.</returns>
/// <exception cref="MaxSessionsException">
/// Thrown when maximum concurrent sessions exceeded.
/// </exception>
public string CreateSession(string? name = null)
{
    // Implementation
}
```

**Class documentation:**
```csharp
/// <summary>
/// Manages DWSIM simulation sessions with lifecycle control and isolation.
/// </summary>
/// <remarks>
/// Each session is independent with its own working directory and DWSIM flowsheet context.
/// Sessions must be explicitly closed to release resources.
/// </remarks>
public sealed class SessionManager
{
    // Implementation
}
```

### Documentation Requirements

**Must document:**
- All public classes, methods, functions
- All MCP tools (detailed descriptions for LLM agents)
- All CAPE-OPEN mappers (interface method mapping)
- All configuration options
- All error codes and their meanings

**Should document:**
- Complex algorithms or business logic
- Non-obvious design decisions
- Workarounds for known issues
- Performance considerations

**Avoid:**
- Obvious comments ("// Increment counter")
- Outdated comments (update or remove)
- Commented-out code (use version control)
- TODO comments without issue tracking

### README Files

**Required README.md in:**
- Project root: Overview, installation, quick start
- `mcp_service/server/`: Python package setup and usage
- `mcp_service/dwsim_worker/`: .NET worker setup and building
- `integration-tests/`: How to run integration tests
- `docs/`: Documentation structure overview

**README structure:**
```markdown
# Component Name

Brief description.

## Installation

Step-by-step installation instructions.

## Usage

Basic usage examples with code snippets.

## Configuration

Configuration options and environment variables.

## Testing

How to run tests.

## Troubleshooting

Common issues and solutions.
```

## Additional Standards

### Error Handling

**Python:**
- Catch specific exceptions, not bare `except:`
- Use custom exception classes for domain errors
- Log errors before re-raising
- Return `Result[T, Error]` types for expected failures

**C#:**
- Catch specific exceptions
- Use custom exception types
- Structured error responses with error codes
- Never swallow exceptions silently

### Logging

**Python (structlog):**
```python
logger.info("session_created", session_id=session_id, name=name)
logger.error("simulation_failed", session_id=session_id, error=str(e))
```

**C# (Serilog):**
```csharp
_logger.Information("Session created: {SessionId} {Name}", sessionId, name);
_logger.Error(e, "Simulation failed for session {SessionId}", sessionId);
```

**Guidelines:**
- Use structured logging (not string formatting)
- Include correlation IDs (sessionId, requestId)
- Log at appropriate levels (Debug, Info, Warning, Error)
- Never log sensitive data (credentials, proprietary simulation data)

### Version Control

- **Commit messages**: Conventional Commits style (`feat:`, `fix:`, `refactor:`, `docs:`)
- **Branch naming**: `feature/`, `bugfix/`, `hotfix/`, `release/`
- **Pull requests**: Must include tests and documentation updates
- **Code review**: All changes require approval before merge
