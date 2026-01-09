# Requirements Document

## Introduction

This spec defines the Pydantic-based data models used by the Python MCP server to validate tool inputs and represent CAPE-OPEN domain objects. It also defines conversion helpers that map validated Python models to C# DTOs (and back) for pythonnet interop.

## Alignment with Product Vision

This feature enables a safe, typed, and composable interface for LLM agents, supporting the product principles of explicit operations, CAPE-OPEN domain modeling, and robust error handling.

## Requirements

### Requirement 1: CAPE-OPEN Domain Models

**User Story:** As an MCP tool developer, I want typed CAPE-OPEN domain models, so that I can validate and serialize streams and unit operations consistently.

#### Acceptance Criteria

1. WHEN a CAPE-OPEN model is instantiated THEN the system SHALL validate required fields and enforce type constraints.
2. WHEN a model includes physical properties THEN the system SHALL enforce unit-safe validation rules (e.g., non-negative pressure).
3. IF a model is serialized to JSON THEN the system SHALL include field metadata and examples for MCP schema generation.

### Requirement 2: MCP Input Models for Session, Flowsheet, and Simulation Tools

**User Story:** As an MCP tool consumer, I want clear, validated request schemas for tool inputs, so that invalid requests are rejected before reaching the C# worker.

#### Acceptance Criteria

1. WHEN a request payload is provided to a tool THEN the system SHALL validate it using Pydantic models for that tool.
2. IF a required field is missing or invalid THEN the system SHALL return a structured validation error.
3. WHEN optional fields are omitted THEN the system SHALL apply documented defaults.

### Requirement 3: Python to C# DTO Conversion Helpers

**User Story:** As a platform developer, I want conversion helpers between Python models/dicts and C# DTOs, so that pythonnet interop is reliable and consistent.

#### Acceptance Criteria

1. WHEN converting a Pydantic model to a C# DTO THEN the system SHALL map all supported fields and preserve data fidelity.
2. IF a field cannot be mapped THEN the system SHALL raise a clear, actionable error describing the missing mapping.
3. WHEN converting a C# DTO back to Python THEN the system SHALL produce an equivalent Pydantic model or dict.

### Requirement 4: Type Safety and Documentation

**User Story:** As a maintainer, I want models and helpers to be type-safe and well-documented, so that future tools can reuse them safely.

#### Acceptance Criteria

1. WHEN models and helpers are defined THEN they SHALL include type hints and docstrings for public APIs.
2. WHEN running static analysis THEN the system SHALL pass mypy checks for the new modules.

## Non-Functional Requirements

### Code Architecture and Modularity
- **Single Responsibility Principle**: Each model and conversion helper lives in its own file with one clear purpose.
- **Modular Design**: CAPE-OPEN models, MCP request models, and converters remain isolated by module.
- **Dependency Management**: Converters depend on models, not the other way around.
- **Clear Interfaces**: Each converter exposes explicit functions for supported DTO types.

### Performance
- Validation and conversion SHALL add minimal overhead relative to tool execution time.

### Security
- Inputs SHALL be validated before any interop calls are made to the C# worker.
- File path or command inputs SHALL be treated as untrusted and validated where applicable.

### Reliability
- Validation errors SHALL be deterministic and include actionable messages.
- Conversions SHALL be reversible for supported DTOs.

### Usability
- Schemas SHALL include examples and field descriptions to aid LLM tool usage.
