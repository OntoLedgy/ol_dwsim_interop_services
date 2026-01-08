# Design Document

## Overview

This design adds a Python-side resource limits layer for the in-process pythonnet bridge. It provides per-operation timeouts, session lifetime enforcement, and memory usage monitoring before invoking DWSIM operations, returning structured errors and logs when limits are exceeded.

## Steering Document Alignment

### Technical Standards (tech.md)
- Uses Python 3.11+ with asyncio for timeout enforcement.
- Uses pydantic for configuration and error payload models.
- Uses psutil for process memory monitoring.
- Uses structured logging (structlog or logging adapter) for limit breach events.

### Project Structure (structure.md)
- Adds a dedicated `dwsim_mcp_server/limits/` package with one class per file.
- Adds configuration models under `dwsim_mcp_server/config/` following the one-class-per-file rule.
- Adds error models under `models/errors/` with one file per class.

## Code Reuse Analysis

### Existing Components to Leverage
- **models/requests/create_session_request.py**: Provides per-session timeout input for session creation.
- **dwsim_mcp_server/ipc/session_client.py**: Pythonnet wrapper to wrap with limit guards.
- **dwsim_mcp_server/ipc/exceptions.py**: Existing error mapping utilities to extend for limit errors.

### Integration Points
- **MCP tools layer**: Wrap tool handlers with limit guard checks before calling pythonnet.
- **SessionClient**: Central entry point for session operations to enforce timeouts and session expiry.
- **Configuration**: Environment variables already documented in `mcp_service/server/README.md`.

## Architecture

The limits layer sits between MCP tools and the pythonnet SessionClient. It tracks session lifetimes, applies per-operation timeouts, and monitors process memory with a background task.

### Modular Design Principles
- **Single File Responsibility**: Each limit-related class or model in its own file
- **Component Isolation**: Timeouts, memory monitoring, and session lifetime tracking are separate modules
- **Service Layer Separation**: Limit guard is a thin wrapper around interop calls
- **Utility Modularity**: Shared helpers for time measurement and configuration validation

```mermaid
graph TD
    A[MCP Tool Handler] --> B[ResourceLimitGuard]
    B --> C[SessionLifetimeTracker]
    B --> D[OperationTimeoutRunner]
    B --> E[MemoryMonitor]
    B --> F[SessionClient (pythonnet)]
```

## Components and Interfaces

### ResourceLimitSettings
- **Purpose:** Central configuration for all limits.
- **Interfaces:** `load()` or BaseSettings-compatible initialization.
- **Dependencies:** pydantic.
- **Reuses:** Existing environment variable naming in README.

### SessionLifetimeTracker
- **Purpose:** Track session start time, timeout, and expiration.
- **Interfaces:** `register_session(session_id, timeout_seconds)`, `is_expired(session_id)`, `remove_session(session_id)`.
- **Dependencies:** `time.monotonic`, `ResourceLimitSettings`.
- **Reuses:** `CreateSessionRequest.timeout` value.

### OperationTimeoutRunner
- **Purpose:** Wrap blocking pythonnet calls in `asyncio.wait_for`.
- **Interfaces:** `run_with_timeout(callable, timeout_seconds, *, session_id=None)`.
- **Dependencies:** asyncio, threading or `asyncio.to_thread`.
- **Reuses:** Existing SessionClient methods.

### MemoryMonitor
- **Purpose:** Poll process memory and maintain breach state.
- **Interfaces:** `start()`, `stop()`, `is_exceeded()`, `snapshot()`.
- **Dependencies:** psutil, asyncio task management.

### ResourceLimitGuard
- **Purpose:** Orchestrate checks for memory, session expiry, and timeouts.
- **Interfaces:** `ensure_can_run(session_id)` and `run(session_id, callable, timeout_override=None)`.
- **Dependencies:** SessionLifetimeTracker, OperationTimeoutRunner, MemoryMonitor.

## Data Models

### ResourceLimitSettings
```
ResourceLimitSettings (pydantic BaseSettings)
- max_sessions: int
- session_timeout_seconds: int
- operation_timeout_seconds: int
- memory_limit_mb: int
- memory_poll_interval_seconds: float
- memory_recovery_ratio: float
```

### ResourceLimitError
```
ResourceLimitError
- code: string (RESOURCE_LIMIT_EXCEEDED, SESSION_EXPIRED, TIMEOUT)
- message: string
- session_id: optional string
- details: optional dict (limit, observed, elapsed)
```

## Error Handling

### Error Scenarios
1. **Operation Timeout**
   - **Handling:** Cancel operation via asyncio.wait_for timeout, return TIMEOUT error.
   - **User Impact:** Error message suggests increasing timeout or simplifying flowsheet.

2. **Session Expired**
   - **Handling:** Reject new operation, attempt session close, return SESSION_EXPIRED error.
   - **User Impact:** Clear message to create a new session.

3. **Memory Limit Exceeded**
   - **Handling:** Reject new operation while breach flag is set, return RESOURCE_LIMIT_EXCEEDED error.
   - **User Impact:** Message includes current usage and limit.

4. **Invalid Configuration**
   - **Handling:** Raise configuration error during startup and fail fast.
   - **User Impact:** Startup failure with clear configuration guidance.

## Testing Strategy

### Unit Testing
- Test `SessionLifetimeTracker` expiry logic with fixed times.
- Test `OperationTimeoutRunner` using a sleep to trigger timeout.
- Test `MemoryMonitor` with psutil mocked to simulate memory usage.

### Integration Testing
- Exercise a simulated tool call wrapped with `ResourceLimitGuard` and verify timeout and error payload.
- Validate session expiry path: create session, advance clock, attempt operation.

### End-to-End Testing
- Trigger timeout through an MCP tool call and verify MCP error response structure.
