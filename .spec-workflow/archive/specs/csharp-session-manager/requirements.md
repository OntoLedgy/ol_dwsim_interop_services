# Requirements Document

## Introduction

The C# Session Manager enables concurrent, isolated flowsheet simulation sessions within the DWSIM Worker. This component provides the multi-session architecture layer that sits above the single-session `FlowsheetContext`, allowing multiple independent calculations to run concurrently without interference. This is essential for supporting the MCP server's goal of handling multiple AI agents or users simultaneously.

**Current State**: The `FlowsheetContext` class (from Spec 1.1-1.3) already provides complete single-session functionality including flowsheet creation, compound/property package configuration, stream management, unit operations, calculations, and proper disposal. The `FlowsheetContext` is explicitly documented as "NOT thread-safe - use one instance per thread/session."

**What This Spec Adds**: A `SessionManager` class that creates and manages multiple `FlowsheetContext` instances, providing session isolation, concurrent access patterns, unique session identification, and coordinated resource cleanup.

## Alignment with Product Vision

From `product.md`:
- **Vision**: "Enable AI systems to perform chemical process engineering through natural language"
- **Target Users**: AI/LLM developers building process engineering applications
- **Key Feature**: "Session-based isolation for concurrent operations"

The SessionManager directly supports these goals by:
1. Enabling multiple AI agents to work on different simulations concurrently
2. Providing isolation guarantees so one agent's work doesn't affect another's
3. Managing resources automatically to prevent memory leaks and resource exhaustion
4. Supporting the scalability needed for production AI applications

From `tech.md`:
- **Architecture**: Polyglot system with .NET Framework 4.8 worker
- **Constraint**: STA (Single-Threaded Apartment) threading for COM compatibility with DWSIM
- **Pattern**: Session-based isolation with no shared state between sessions

## Requirements

### Requirement 1: Session Creation and Identification

**User Story:** As an MCP server developer, I want to create multiple independent simulation sessions with unique identifiers, so that I can support concurrent AI agents working on different flowsheets simultaneously.

#### Acceptance Criteria

1. WHEN a session is created THEN the system SHALL return a unique session ID (GUID format)
2. WHEN a session is created THEN the system SHALL instantiate a new isolated `FlowsheetContext` instance
3. WHEN a session is created with optional configuration THEN the system SHALL pass that configuration to the `FlowsheetContext`
4. IF session creation fails THEN the system SHALL return a structured error with failure reason
5. WHEN multiple sessions are created concurrently THEN each SHALL receive a unique session ID
6. WHEN a session is created THEN it SHALL NOT share any state with other sessions

### Requirement 2: Session Retrieval and Registry

**User Story:** As an MCP server, I want to retrieve active sessions by their ID, so that I can route flowsheet operations to the correct session context.

#### Acceptance Criteria

1. WHEN a valid session ID is provided THEN the system SHALL return the associated `FlowsheetContext` instance
2. IF an invalid or non-existent session ID is provided THEN the system SHALL return a descriptive error
3. WHEN listing all sessions THEN the system SHALL return all active session IDs with metadata (creation time, flowsheet name)
4. WHEN checking if a session exists THEN the system SHALL return true/false without throwing exceptions
5. WHEN multiple threads retrieve the same session THEN the system SHALL return the same `FlowsheetContext` instance

### Requirement 3: Session Disposal and Resource Cleanup

**User Story:** As a system administrator, I want sessions to properly release DWSIM resources when closed, so that the server doesn't leak memory or file handles during long-running operation.

#### Acceptance Criteria

1. WHEN a session is closed by ID THEN the system SHALL call `Dispose()` on the associated `FlowsheetContext`
2. WHEN a session is closed THEN the system SHALL remove it from the session registry
3. WHEN a session is closed THEN subsequent retrieval attempts SHALL return "session not found" error
4. IF `FlowsheetContext.Dispose()` throws an exception THEN the system SHALL log the error and still remove the session from registry
5. WHEN the `SessionManager` itself is disposed THEN all active sessions SHALL be closed
6. WHEN all sessions are closed THEN memory profiling SHALL show no retained DWSIM objects
7. IF a session is already closed THEN attempting to close it again SHALL be idempotent (no error)

### Requirement 4: Session Isolation and Concurrency

**User Story:** As an AI application developer, I want sessions to be fully isolated from each other, so that calculations in one session don't affect or interfere with calculations in another session.

#### Acceptance Criteria

1. WHEN two sessions run calculations simultaneously THEN each SHALL produce independent, correct results
2. WHEN one session modifies a compound list THEN other sessions SHALL NOT see those changes
3. WHEN one session throws an exception THEN other sessions SHALL continue operating normally
4. WHEN 10+ sessions are created concurrently THEN all SHALL be fully functional
5. WHEN session operations occur on different threads THEN the system SHALL handle concurrent access safely
6. IF DWSIM requires STA threading THEN each session SHALL execute operations on appropriate thread model
7. WHEN one session disposes THEN other sessions SHALL remain unaffected

### Requirement 5: Session Lifecycle Hooks and Extensibility

**User Story:** As a future developer adding quota enforcement, I want hooks into session creation and disposal, so that I can add timeout monitoring, usage tracking, or resource limits without modifying core logic.

#### Acceptance Criteria

1. WHEN a session is created THEN the system SHALL record creation timestamp
2. WHEN a session is closed THEN the system SHALL record closure timestamp
3. WHEN querying session metadata THEN the system SHALL return creation time, flowsheet name, and initialization status
4. IF session timeouts are implemented in future THEN the architecture SHALL support adding timeout enforcement without breaking changes
5. IF session quotas are implemented in future THEN the architecture SHALL support adding quota checks without breaking changes

## Non-Functional Requirements

### Code Architecture and Modularity

- **Single Responsibility Principle**: `SessionManager` manages session lifecycle only; `FlowsheetContext` handles DWSIM operations
- **Modular Design**: SessionManager should NOT depend on adapter classes (CompoundAdapter, StreamAdapter, etc.)
- **Dependency Management**: Only depends on `FlowsheetContext`, `FlowsheetContextConfig`, and logging
- **Clear Interfaces**: Provide clean API for session CRUD operations
- **File Organization**: SessionManager in `DwsimWorker/Engine/SessionManager.cs` per structure.md
- **No Breaking Changes**: Must not modify existing `FlowsheetContext` or adapter APIs

### Performance

- **Session Creation Time**: < 500ms per session (limited by DWSIM assembly loading)
- **Session Retrieval Time**: < 1ms (dictionary lookup)
- **Session Disposal Time**: < 100ms (limited by DWSIM resource cleanup)
- **Concurrent Sessions**: Support minimum 10 concurrent sessions, target 20+
- **Memory Overhead**: < 10MB overhead per session beyond `FlowsheetContext` size
- **Registry Lookup**: O(1) time complexity for session retrieval

### Security

- **Session ID Uniqueness**: Use GUIDs to prevent session ID collisions or guessing
- **Session Isolation**: One session MUST NOT access another session's data
- **Error Messages**: Do not leak session IDs or internal state in error messages to unauthorized callers
- **Disposal Safety**: Ensure disposed sessions cannot be retrieved or reused

### Reliability

- **Exception Safety**: Session creation failures must not leave partially-initialized sessions in registry
- **Cleanup Guarantee**: Session disposal must always remove from registry even if `FlowsheetContext.Dispose()` fails
- **Idempotent Operations**: Closing an already-closed session is safe (no-op)
- **Thread Safety**: SessionManager methods must be thread-safe for concurrent access
- **Resource Limits**: Graceful handling when system resources exhausted (clear error, no crash)

### Usability

- **Clear Error Messages**: Descriptive errors for "session not found", "session already exists", etc.
- **Logging**: Structured logging (Serilog) for session lifecycle events (created, retrieved, closed)
- **Diagnostic Support**: Ability to list all active sessions for debugging and monitoring
- **Consistent API**: Follow patterns established by `FlowsheetContext` and adapter classes

### Thread Safety and STA Requirements

- **COM Compatibility**: Must work with DWSIM's COM interop and STA threading requirements
- **Concurrent Access**: Multiple threads can create/retrieve/close sessions simultaneously
- **Context Affinity**: Each `FlowsheetContext` should be accessed from appropriate thread model
- **Lock Granularity**: Minimize lock contention for session registry operations

## Success Metrics

1. **Isolation Test**: Create 20 sessions concurrently, run different calculations in each, verify results are independent
2. **Memory Test**: Create 10 sessions, close all, verify memory released (no leaks)
3. **Performance Test**: Session creation < 500ms, retrieval < 1ms, disposal < 100ms
4. **Stress Test**: Create and dispose 100 sessions sequentially, verify no resource exhaustion
5. **Concurrency Test**: 5 threads creating/using/closing sessions concurrently, verify no race conditions or deadlocks
