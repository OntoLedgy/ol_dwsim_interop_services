# Requirements Document

## Introduction

Implement MCP tools that let LLM agents build DWSIM flowsheets end-to-end: add compounds, select property packages, create streams and unit operations, connect objects, and manage parameters with safe validation. The feature enables conversational construction of simulations without manual DWSIM UI steps.

## Alignment with Product Vision

- Supports the product goal of making chemical simulation AI-native by exposing flowsheet construction via MCP tools.
- Upholds safety and observability principles: input validation, clear error codes, and consistent logging for each tool call.
- Builds on the polyglot architecture: Python MCP façade with C# worker adapters mapped to CAPE-OPEN models.

## Requirements

### Requirement 1

**User Story:** As an LLM agent building a simulation, I want to register compounds and select a property package so that subsequent streams and units use consistent thermodynamics.

#### Acceptance Criteria

1. WHEN `add_compound` receives a known compound name THEN the tool SHALL confirm existence in the DWSIM databank and add it to the session; IF the compound is unknown THEN the tool SHALL return a validation error without mutating the session.
2. WHEN `set_property_package` is called with a supported package name (e.g., Peng-Robinson, SRK, NRTL) THEN the property package SHALL be set on the session and reported in responses; IF unsupported THEN the tool SHALL return a descriptive error and leave the previous package unchanged.
3. IF compounds or property package are missing when downstream tools require them THEN the system SHALL surface a clear error message pointing to the missing precondition.

### Requirement 2

**User Story:** As an LLM agent constructing flowsheets, I want to add streams and unit operations with validated properties so that the flowsheet is physically consistent and reproducible.

#### Acceptance Criteria

1. WHEN `add_stream` is invoked with temperature, pressure, flow, and composition inputs THEN the tool SHALL validate units/values (positive pressure/flow, composition sums within tolerance) and return a `streamId`; IF validation fails THEN no stream SHALL be created and all issues SHALL be enumerated.
2. WHEN `add_unit` is invoked with `unitType` and parameters (e.g., separator, mixer, heater, pump, valve) THEN the tool SHALL validate supported types, apply defaults, and return a `unitId`; IF parameters are incomplete THEN the tool SHALL respond with required fields and SHALL NOT create the unit.
3. WHEN objects are created THEN they SHALL be persisted in the session registry and discoverable by `list_objects` with type, name, and identifiers.

### Requirement 3

**User Story:** As an LLM agent assembling topology, I want to connect, inspect, update, and delete flowsheet objects so that I can iteratively refine the model safely.

#### Acceptance Criteria

1. WHEN `connect` is called with `sourceId`, `targetId`, and `portName` THEN the tool SHALL validate object existence and compatibility, create the connection, and return updated topology; IF incompatible THEN it SHALL return a clear reason and leave topology unchanged.
2. WHEN `set_object_parameter` receives `objectId`, `parameterName`, and `value` THEN the tool SHALL validate parameter support and type before applying; IF invalid THEN it SHALL report the offending parameter and maintain prior value.
3. WHEN `delete_object` is called THEN the tool SHALL remove the object and orphaned connections safely; IF the object is unknown THEN it SHALL return a not-found error without side effects.

## Non-Functional Requirements

### Code Architecture and Modularity
- Single-responsibility modules for each MCP tool and per-class files in Python/C# consistent with `structure.md`.
- Clear contracts and Pydantic schemas for tool inputs/outputs; DTOs remain JSON-serializable and CAPE-OPEN aligned.
- Validation and conversion logic reused from existing adapters/services; no duplicated parsing code.

### Performance
- Tool calls for metadata operations (add/list/delete/connect) SHOULD complete with P95 latency < 2s; creation of standard streams/units SHOULD target < 3s including validation.
- Input validation performs in-process without spawning external processes; no unnecessary file I/O.

### Security
- Strict input validation to prevent invalid topology or parameter injection; reject unknown compounds/property packages.
- No filesystem writes beyond sandboxed session directories; disallow arbitrary paths in any parameter.
- Enforce safe defaults and bounded numeric ranges to prevent runaway simulations at build stage.

### Reliability
- Idempotent behavior where applicable (e.g., re-adding same compound returns success without duplication).
- Atomic operations: on validation failure, no partial object creation or topology mutation.
- Structured error responses with actionable messages; all operations logged with sessionId/requestId.

### Usability
- MCP tool descriptions are concise and agent-friendly, exposing supported options and required fields.
- Responses include identifiers and summaries to enable chaining (e.g., streamId/unitId in success paths).
- Error messages guide agents to missing prerequisites (compound/package selection, required parameters).

