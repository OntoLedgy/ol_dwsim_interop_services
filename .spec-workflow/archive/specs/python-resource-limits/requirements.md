# Requirements Document

## Introduction

Implement Python-side resource limits for the MCP server when using pythonnet in-process interop. This spec adds per-operation timeouts, session lifetime limits, and memory monitoring to prevent runaway simulations and ensure predictable service behavior.

## Alignment with Product Vision

This feature delivers on the "Safety First" principle from product.md by enforcing timeouts and resource caps, providing predictable behavior for LLM agents, and preventing system instability from long-running simulations.

## Requirements

### Requirement 1: Per-Operation Timeout Enforcement

**User Story:** As an MCP client, I want long-running simulation operations to be terminated after a configured timeout, so that the server remains responsive and safe.

#### Acceptance Criteria

1. WHEN a DWSIM operation is invoked THEN the system SHALL enforce a per-operation timeout using asyncio.wait_for (or equivalent).
2. IF the operation completes within the timeout THEN the system SHALL return the result without modification.
3. WHEN the timeout is exceeded THEN the system SHALL cancel the operation, log the timeout, and return a TIMEOUT error containing the configured timeout and elapsed time.

### Requirement 2: Session Lifetime Limits

**User Story:** As an MCP client, I want sessions to expire after a configurable lifetime, so that idle or forgotten sessions do not consume resources indefinitely.

#### Acceptance Criteria

1. WHEN a session is created THEN the system SHALL record its start time and configured lifetime (from request or default).
2. IF a session exceeds its lifetime THEN the system SHALL reject new operations for that session, attempt to close the session, and return a SESSION_EXPIRED error.
3. WHEN a session is closed THEN the system SHALL remove lifetime tracking data for that session.

### Requirement 3: Memory Monitoring and Limit Enforcement

**User Story:** As an operator, I want memory usage to be monitored and capped, so that a single simulation cannot exhaust server memory.

#### Acceptance Criteria

1. WHEN the server starts THEN the system SHALL begin polling process memory usage at a configurable interval.
2. IF process memory usage exceeds the configured limit THEN the system SHALL reject new operations with a RESOURCE_LIMIT_EXCEEDED error and log the breach.
3. WHEN memory usage returns below a recovery threshold THEN the system SHALL clear the breach state and log recovery.

### Requirement 4: Configurable Limits

**User Story:** As an operator, I want to configure timeouts and memory limits via environment variables or config files, so that deployments can tune safety settings.

#### Acceptance Criteria

1. WHEN the server loads configuration THEN the system SHALL read limit settings from environment variables or config files with documented defaults.
2. IF a per-session timeout is provided in create_session THEN the system SHALL validate it against min/max bounds and use it for that session.
3. IF any configured limits are invalid (e.g., negative values) THEN the system SHALL fail fast with a configuration error.

### Requirement 5: Diagnostic Logging and Error Reporting

**User Story:** As an operator or developer, I want clear logs and error details for limit violations, so that I can diagnose failures and tune limits.

#### Acceptance Criteria

1. WHEN a limit is violated THEN the system SHALL log a structured event with sessionId (if available), limit type, configured limit, and observed value.
2. WHEN a limit is violated THEN the system SHALL return a structured error payload with an error code and human-readable message.

## Non-Functional Requirements

### Code Architecture and Modularity
- **Single Responsibility Principle**: Each file should have a single, well-defined purpose
- **Modular Design**: Components, utilities, and services should be isolated and reusable
- **Dependency Management**: Minimize interdependencies between modules
- **Clear Interfaces**: Define clean contracts between components and layers

### Performance
- Limit checks SHALL add minimal overhead (<5ms per operation under typical load).
- Memory polling interval SHALL be configurable to balance accuracy and CPU cost.

### Security
- Limit enforcement SHALL not expose sensitive data in error messages or logs.
- Configuration values SHALL be validated to prevent unsafe settings.

### Reliability
- Limit enforcement SHALL be thread-safe and deterministic under concurrent requests.
- Violations SHALL return actionable errors without crashing the server process.

### Usability
- Error messages SHALL be clear and include recommended remediation (e.g., increase timeout).
