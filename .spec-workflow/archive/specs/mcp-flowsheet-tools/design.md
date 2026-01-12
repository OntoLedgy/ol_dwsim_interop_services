# Design Document

## Overview

Implement MCP tools that let agents construct DWSIM flowsheets programmatically: add compounds, select property packages, create streams and unit operations, connect topology, list objects, update parameters, and delete objects with strong validation and CAPE-OPEN-aligned DTOs. The Python MCP façade will expose these tools, calling into pythonnet-backed C# adapters that manage session-scoped DWSIM objects.

## Steering Document Alignment

### Technical Standards (tech.md)
- Python 3.11+ MCP server with pythonnet interop to .NET Framework 4.8 worker.
- Structured logging (structlog/Serilog), typed DTOs, CAPE-OPEN naming for properties.
- Safety: sandboxed session directories, resource limits enforced at Python layer.

### Project Structure (structure.md)
- One class/file per responsibility in both Python and C#.
- Tool modules in `mcp_service/server/dwsim_mcp_server/tools/`; DTOs in `models/`.
- Adapters in C# under `DwsimWorker/Adapters` and converters under `Converters`.

## Code Reuse Analysis
- **pythonnet bridge**: reuse existing `session_client.py`/bridge helpers for loading `DwsimWorker.dll`.
- **SessionManager**: reuse session lifecycle to obtain per-session handles for flowsheet operations.
- **Converters**: reuse CAPE-OPEN converters for MaterialStream, PropertyPackage, UnitOperation to keep DTO parity.
- **Validation utilities**: reuse Pydantic validators and unit conversion helpers (SI enforcement).
- **Logging**: reuse structured logging wrappers to tag sessionId/requestId.

### Existing Components to Leverage
- `DwsimService` / session client for calls into C# worker.
- CAPE-OPEN DTOs and conversion helpers for streams/units.
- Input validation mixins (composition sum, positive pressure/flow) if already present; otherwise extend shared validators module.

### Integration Points
- Python MCP tools -> Python service layer -> pythonnet bridge -> C# adapters -> DWSIM engine.
- Shared models between Python and C# stay JSON-serializable for MCP responses.

## Architecture

- **MCP Tool Layer (Python)**: One tool function per operation: `add_compound`, `set_property_package`, `add_stream`, `add_unit`, `connect`, `list_objects`, `set_object_parameter`, `delete_object`.
- **Service Layer (Python)**: `FlowsheetService` encapsulates calls to pythonnet SessionManager/adapters, handles validation, and mapping between Pydantic DTOs and C# DTOs.
- **Interop Layer (pythonnet)**: Bridge that resolves assembly paths, loads `DwsimWorker.dll`, and exposes adapters for compounds, property packages, streams, units, topology connections, and parameter updates.
- **C# Layer**: Adapters per domain (StreamAdapter, UnitOpAdapter, FlowsheetAdapter) exposing strongly-typed methods; converters to/from CAPE-OPEN DTOs to keep parity with Python models.
- **Data Flow**: MCP request → Pydantic model validation → service call → pythonnet proxy → C# adapter → DWSIM; response DTO → Python conversion → MCP response.

## Components and Interfaces

### MCP Tool Modules (Python)
- **tools/flowsheet.py** (or dedicated files per tool):
  - `add_compound(input: AddCompoundInput) -> AddCompoundOutput`
  - `set_property_package(input: SetPropertyPackageInput) -> SetPropertyPackageOutput`
  - `add_stream(input: AddStreamInput) -> AddStreamOutput`
  - `add_unit(input: AddUnitInput) -> AddUnitOutput`
  - `connect(input: ConnectInput) -> ConnectOutput`
  - `list_objects(input: ListObjectsInput) -> ListObjectsOutput`
  - `set_object_parameter(input: SetObjectParameterInput) -> SetObjectParameterOutput`
  - `delete_object(input: DeleteObjectInput) -> DeleteObjectOutput`
  - Responsibilities: register tools with MCP SDK, validate inputs (Pydantic), call service, format responses, structured logging.

### Service Layer (Python)
- **FlowsheetService**
  - Dependencies: pythonnet bridge, converters, logger.
  - Methods mirror tools; enforce sequencing (package/compound preconditions), atomicity (no partial writes), and idempotency for `add_compound`.
  - Emits structured errors for unsupported unit types/parameters, missing prerequisites, or topology conflicts.

### Interop Layer (Python)
- **Bridge/Client**
  - Provides handles to C# adapters: `CompoundAdapter`, `PropertyPackageAdapter`, `StreamAdapter`, `UnitAdapter`, `TopologyAdapter`.
  - Manages lifetime and disposal; propagates C# exceptions to Python domain errors.

### C# Layer (Worker)
- **Adapters**:
  - `CompoundAdapter`: validate and add compounds using DWSIM databank.
  - `PropertyPackageAdapter`: set and confirm property packages.
  - `StreamAdapter`: create streams with validated thermodynamic state and composition.
  - `UnitOpAdapter`: create units (separator, mixer, heater, pump, valve, etc.) with parameter defaults and validation.
  - `TopologyAdapter`: connect objects, list objects, set parameters, delete objects with connection cleanup.
- **Converters**:
  - CAPE-OPEN DTO ↔ DWSIM objects; enforce SI units and parameter name normalization.

## Data Models

### Model 1: AddCompoundInput (Pydantic)
- sessionId: str
- compoundName: str

### Model 2: SetPropertyPackageInput
- sessionId: str
- packageName: Literal["peng-robinson","srk","nrtl",...]

### Model 3: AddStreamInput
- sessionId: str
- name: str
- temperature: float (K)
- pressure: float (Pa)
- flow: float (mol/s or mass/s with unit flag)
- composition: Dict[str, float] (mole fractions)
- phaseHint: Optional[str]

### Model 4: AddUnitInput
- sessionId: str
- unitType: Literal["separator","mixer","heater","pump","valve",...]
- name: str
- parameters: Dict[str, Any] (validated per unit schema)

### Model 5: ConnectInput
- sessionId: str
- sourceId: str
- targetId: str
- portName: str

### Model 6: SetObjectParameterInput
- sessionId: str
- objectId: str
- parameterName: str
- value: Any (validated against schema/expected type)

### Model 7: DeleteObjectInput
- sessionId: str
- objectId: str

Outputs include identifiers (streamId, unitId), echoed names, and summaries for chaining; `list_objects` returns collections keyed by type.

## Error Handling

### Error Scenarios
1. **Unknown compound or property package**
   - Handling: Validation error with supported options; no mutation.
   - User Impact: Clear message to pick a supported value.
2. **Invalid thermodynamic state or composition**
   - Handling: Aggregate validation errors (pressure/flow positivity, composition sum tolerance, required fields).
   - User Impact: Enumerated issues; no stream created.
3. **Unsupported unit type or missing parameters**
   - Handling: Error listing supported unit types/required parameters; no unit created.
4. **Topology conflicts (connect)**
   - Handling: Validate source/target existence and port compatibility; return descriptive conflict; no partial connection.
5. **Parameter update invalid**
   - Handling: Reject with expected type/range; keep previous value.

## Testing Strategy

### Unit Testing
- Python: Tool-level tests using Pydantic models for validation paths; service tests with pythonnet mocks to assert call contracts and error propagation.
- C#: Adapter tests for compound/package selection, stream creation validation, unit creation, parameter updates, and connection logic.

### Integration Testing
- Python ↔ C# in-process (pythonnet): build a flowsheet (compounds, package, stream, separator), connect, list objects, update parameter, delete object; verify consistency and idempotency.
- Composition/units validation edge cases (bad composition sum, negative pressure/flow, unsupported unitType).

### End-to-End Testing
- Agent-style sequence via MCP: add_compound → set_property_package → add_stream → add_unit (separator) → connect → list_objects; assert returned IDs and topology correctness; ensure errors are structured and non-destructive on bad inputs.
