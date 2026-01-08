# Requirements Document

## Introduction

Implement the Python-side pythonnet bridge that loads the DwsimWorker .NET Framework class library and exposes a clean, typed Python API for session operations and future flowsheet actions. This feature enables in-process interop without IPC, providing low-latency access to the DWSIM engine.

## Alignment with Product Vision

This feature delivers the AI-native interface and clean architecture promised in `product.md` by providing a Python faÇõade over the .NET engine, ensuring composable operations, structured error handling, and observability while minimizing deployment complexity through a single-process design.

## Requirements

### Requirement 1: Assembly Loading and Configuration

**User Story:** As an MCP server developer, I want to load the DwsimWorker class library from Python, so that I can call .NET APIs via pythonnet.

#### Acceptance Criteria

1. WHEN the bridge initializes THEN the system SHALL resolve the DwsimWorker assembly path using configuration and sensible defaults.
2. IF the assembly path is invalid or the DLL is missing THEN the system SHALL raise a clear Python exception describing the path failure.
3. WHEN pythonnet is unavailable or fails to load THEN the system SHALL raise a clear Python exception with remediation guidance.

### Requirement 2: Session Manager Wrapper

**User Story:** As a Python service developer, I want a thin client around `SessionManager`, so that I can create and close sessions from Python.

#### Acceptance Criteria

1. WHEN a client calls `create_session` THEN the system SHALL instantiate and return a new session ID from `SessionManager`.
2. WHEN a client calls `close_session` with a valid session ID THEN the system SHALL dispose of the session and return a success indicator.
3. IF `SessionManager` construction fails THEN the system SHALL surface the underlying exception as a Python error with context.

### Requirement 3: Error and Exception Mapping

**User Story:** As a Python API consumer, I want .NET exceptions translated into Python exceptions, so that failures are actionable and consistent.

#### Acceptance Criteria

1. WHEN a .NET exception is raised during interop THEN the system SHALL convert it into a Python exception type with the original message preserved.
2. IF the .NET exception contains nested inner exceptions THEN the system SHALL include a summarized causal chain in the Python error message.
3. WHEN known error categories are detected (e.g., invalid session, invalid inputs) THEN the system SHALL map them to specific Python exception classes.

### Requirement 4: Type Conversion Helpers

**User Story:** As a Python service developer, I want conversions between Python values and .NET DTOs, so that calls are ergonomic and type-safe.

#### Acceptance Criteria

1. WHEN Python primitives or dicts are passed to interop methods THEN the system SHALL convert them into the expected .NET DTO types where applicable.
2. WHEN .NET DTOs are returned THEN the system SHALL provide Python-native representations (dicts or typed models) for downstream use.
3. IF a type conversion fails THEN the system SHALL raise a Python exception describing the offending field and expected type.

### Requirement 5: Resource Management

**User Story:** As a platform engineer, I want interop objects disposed deterministically, so that repeated calls do not leak resources.

#### Acceptance Criteria

1. WHEN the Python client is closed or disposed THEN the system SHALL release any underlying .NET IDisposable resources.
2. IF a call creates temporary .NET objects THEN the system SHALL ensure they are disposed or released when no longer needed.
3. WHEN repeated sessions are created and closed THEN the system SHALL not show unbounded memory growth in a short soak test.

### Requirement 6: Logging Integration

**User Story:** As an operator, I want Python-side logging that aligns with C# logs, so that troubleshooting interop issues is straightforward.

#### Acceptance Criteria

1. WHEN interop operations occur THEN the system SHALL emit structured Python logs with session IDs and operation names.
2. IF the C# layer emits errors THEN the system SHALL log them with matching context on the Python side.
3. WHEN logging is disabled or minimized THEN the system SHALL not raise additional exceptions.

## Non-Functional Requirements

### Code Architecture and Modularity
- **Single Responsibility Principle**: Separate assembly loading, session client logic, and type conversion into dedicated modules.
- **Modular Design**: Keep pythonnet bridge isolated from MCP tool implementations and expose a minimal public API.
- **Dependency Management**: Avoid new heavy dependencies beyond pythonnet and existing logging utilities.
- **Clear Interfaces**: Provide a stable, documented Python surface for future services (e.g., `DwsimService`).

### Performance
- Bridge initialization SHOULD complete within 2 seconds on a developer workstation.
- Session creation and closure SHOULD add minimal overhead (<100 ms excluding DWSIM work).

### Security
- Assembly path resolution SHALL prevent traversal outside configured roots.
- Errors SHALL avoid leaking sensitive filesystem information beyond necessary paths.

### Reliability
- The bridge SHALL handle repeated initialization attempts without crashing the host process.
- Errors SHALL be deterministic and actionable when dependencies are missing.

### Usability
- Exceptions SHALL include clear remediation guidance (install pythonnet, build DwsimWorker.dll, etc.).
- The bridge API SHOULD be discoverable via type hints and docstrings.
