# Requirements Document

## Introduction

This specification defines the MCP session management tools for the DWSIM MCP Server. It covers `create_session`, `close_session`, `save_case`, and `load_case` tools so LLM agents can manage simulation lifecycles safely, with clear validation and error handling.

## Alignment with Product Vision

Session tools are foundational to the product goals of safe, composable, and observable simulation workflows. They provide explicit lifecycle control, enforce resource limits, and enable persistence via save/load so agents can build repeatable engineering workflows without direct access to DWSIM internals.

## Requirements

### Requirement 1: Create Session Tool

**User Story:** As an LLM agent, I want to create a simulation session, so that I can build and run a flowsheet in an isolated context.

#### Acceptance Criteria

1. WHEN `create_session` is called with an optional name THEN the system SHALL create a new session and return a response containing a unique `sessionId`.
2. IF the session limit is reached THEN the system SHALL return an error with a clear code and message indicating the limit was exceeded.
3. WHEN the optional name is omitted THEN the system SHALL still create a session and return the generated `sessionId`.

### Requirement 2: Close Session Tool

**User Story:** As an LLM agent, I want to close a session, so that I can release resources after completing a simulation.

#### Acceptance Criteria

1. WHEN `close_session` is called with a valid session ID THEN the system SHALL close the session and return `success: true`.
2. IF the session ID does not exist THEN the system SHALL return a NotFound-style error with a clear message.
3. WHEN a session closes successfully THEN the system SHALL remove it from lifetime tracking.

### Requirement 3: Save Case Tool

**User Story:** As an LLM agent, I want to save the current flowsheet to disk, so that I can persist or share the case.

#### Acceptance Criteria

1. WHEN `save_case` is called with a valid session ID and an allowed file path THEN the system SHALL save the case and return `success: true`.
2. IF the file path is outside the allowed storage roots THEN the system SHALL reject the request with an InvalidPath-style error.
3. IF saving fails for any reason THEN the system SHALL return a failure response with an actionable error message.

### Requirement 4: Load Case Tool

**User Story:** As an LLM agent, I want to load a flowsheet case, so that I can resume or inspect an existing simulation.

#### Acceptance Criteria

1. WHEN `load_case` is called with a valid session ID and an allowed file path THEN the system SHALL load the case into the session and return the `sessionId`.
2. IF the file path is outside the allowed storage roots THEN the system SHALL reject the request with an InvalidPath-style error.
3. IF the session ID does not exist THEN the system SHALL return a NotFound-style error with a clear message.

### Requirement 5: MCP Tool Schema and Error Mapping

**User Story:** As an LLM agent, I want consistent inputs and outputs, so that I can compose tools reliably and handle failures.

#### Acceptance Criteria

1. WHEN session tools are registered THEN the system SHALL expose clear MCP tool descriptions and schemas based on Pydantic models.
2. IF a domain error occurs (session not found, invalid path, engine fault) THEN the system SHALL return a structured error with a code and message.
3. WHEN any session tool succeeds THEN the system SHALL return a minimal, machine-readable result that includes required fields (`sessionId` or `success`).

## Non-Functional Requirements

### Code Architecture and Modularity
- **Single Responsibility Principle**: Each tool handler and model file has one clear responsibility.
- **Modular Design**: Session tool handlers, validation utilities, and models are isolated.
- **Dependency Management**: Tools depend on injected clients rather than globals.
- **Clear Interfaces**: Session tools expose consistent request/response models.

### Performance
- `create_session` and `close_session` SHOULD complete within 500ms under normal load.
- `save_case` and `load_case` SHOULD complete within 2s for small cases.

### Security
- File paths MUST be validated against a configured allowlist of directories.
- Path traversal (e.g., `..`) MUST be rejected.
- Errors MUST avoid leaking sensitive filesystem details.

### Reliability
- Session lifetime tracking MUST be updated on create/close.
- Tool failures MUST return deterministic error codes/messages.
- Resource cleanup MUST be performed on close operations.

### Usability
- Error messages SHOULD be concise and actionable for LLM agents.
- Tool descriptions SHOULD clearly state required inputs and outputs.
