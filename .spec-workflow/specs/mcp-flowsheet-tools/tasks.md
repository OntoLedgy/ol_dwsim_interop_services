# Tasks Document

- [x] 1. Define Pydantic input/output models for flowsheet MCP tools
  - File: models/mcp_inputs/flowsheet_build.py
  - Create typed request/response models for add_compound, set_property_package, add_stream, add_unit, connect, list_objects, set_object_parameter, delete_object; enforce SI units, composition sum tolerance, and supported enums.
  - Purpose: Provide validated contracts for MCP tools and service layer.
  - _Leverage: models/cape_open/material_stream.py, models/cape_open/thermo_property_package.py, existing validation helpers if present_
  - _Requirements: 1, 2, 3_
  - _Prompt: Implement the task for spec mcp-flowsheet-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python developer with expertise in Pydantic schemas for engineering data | Task: Add request/response models for flowsheet MCP tools with SI validation and CAPE-OPEN-friendly fields | Restrictions: Do not modify existing base models; keep DTOs JSON-serializable; enforce non-negative/positive numeric constraints and composition tolerance | _Leverage: models/cape_open material and property package models, any shared validators | _Requirements: 1,2,3 | Success: All models validate required fields, reject bad values, and integrate without breaking existing imports; mypy/lint pass on new models; tasks.md updated after completion per workflow instructions_

- [x] 2. Implement FlowsheetService to bridge MCP inputs to pythonnet worker calls
  - File: mcp_service/server/dwsim_mcp_server/service/flowsheet_service.py
  - Add service methods for compound add, property package set, stream/unit creation, connect, list_objects, set parameter, delete with atomic validation and idempotency for add_compound.
  - Purpose: Encapsulate business logic and sequencing before hitting C# adapters.
  - _Leverage: existing session/bridge client (e.g., session_client.py), DwsimService patterns, converters, logging utilities_
  - _Requirements: 1, 2, 3_
  - _Prompt: Implement the task for spec mcp-flowsheet-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python service engineer specializing in pythonnet interop | Task: Build FlowsheetService methods that validate inputs, enforce prerequisites, and call C# adapters for flowsheet construction | Restrictions: No direct MCP SDK calls here; keep operations atomic (no partial mutations on failure); reuse converters/logging; propagate structured errors | _Leverage: session_client/pythonnet bridge, existing service patterns, CAPE-OPEN converters | _Requirements: 1,2,3 | Success: Service methods callable from tools; validation/errors match requirements; idempotent add_compound; unit/stream creation respects defaults and validation_

- [x] 3. Extend MCP tool layer to expose flowsheet-building tools
  - File: mcp_service/server/dwsim_mcp_server/tools/flowsheet.py
  - Register MCP tools for add_compound, set_property_package, add_stream, add_unit, connect, list_objects, set_object_parameter, delete_object; wire to FlowsheetService and Pydantic models; return agent-friendly responses.
  - Purpose: Make flowsheet operations available to MCP clients.
  - _Leverage: existing MCP server bootstrap, other tool patterns (session tools), new FlowsheetService and models_
  - _Requirements: 1, 2, 3_
  - _Prompt: Implement the task for spec mcp-flowsheet-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: MCP tool developer | Task: Add flowsheet MCP tools wired to FlowsheetService with validated inputs/outputs and descriptive metadata | Restrictions: Follow existing tool registration patterns; do not bypass validation; ensure structured logging with sessionId/requestId | _Leverage: existing tool modules, MCP SDK setup, FlowsheetService | _Requirements: 1,2,3 | Success: Tools are discoverable via MCP list_tools, execute happy-path flows, and return structured errors without partial mutations_

- [x] 4. Add or extend C# adapters for flowsheet operations
  - Files: mcp_service/dwsim_worker/DwsimWorker/Adapters/StreamAdapter.cs; mcp_service/dwsim_worker/DwsimWorker/Adapters/UnitOpAdapter.cs; mcp_service/dwsim_worker/DwsimWorker/Adapters/FlowsheetAdapter.cs; mcp_service/dwsim_worker/DwsimWorker/Converters/CapeOpenConverter.cs
  - Ensure adapters support compound add, property package set, stream creation, unit creation (separator/mixer/heater/pump/valve), connect/list/set parameter/delete with validation and DTO parity; update converters for needed DTO fields.
  - Purpose: Provide worker-side capabilities for flowsheet building invoked from pythonnet.
  - _Leverage: existing adapters/converters, CAPE-OPEN DTOs, validation helpers_
  - _Requirements: 1, 2, 3_
  - _Prompt: Implement the task for spec mcp-flowsheet-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# DWSIM adapter engineer | Task: Add/extend adapter methods to support flowsheet CRUD/topology with validation and CAPE-OPEN-aligned DTO mapping | Restrictions: Preserve one-class-per-file and existing patterns; no breaking API changes; keep operations atomic on failure; maintain SI units | _Leverage: current Adapters and Converters, DWSIM API, validation utilities | _Requirements: 1,2,3 | Success: Adapter methods callable from pythonnet; validation errors are descriptive; DTO round-trips succeed; unit tests/build pass_

- [ ] 5. Add Python unit tests for models, service, and tool wiring
  - Files: mcp_service/server/tests/test_flowsheet_models.py; mcp_service/server/tests/test_flowsheet_service.py; mcp_service/server/tests/test_flowsheet_tools.py
  - Cover validation edge cases, service behavior (success/failure, idempotent add_compound), and tool integration with mocked service.
  - Purpose: Prevent regressions and enforce acceptance criteria at Python layer.
  - _Leverage: tests/helpers/testUtils.py, fixtures, mock patterns from existing tool tests_
  - _Requirements: 1, 2, 3_
  - _Prompt: Implement the task for spec mcp-flowsheet-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA engineer for Python services | Task: Write unit tests for flowsheet models/service/tools covering validation, happy paths, and error cases | Restrictions: Use mocks for pythonnet/worker calls; no external resources; ensure deterministic assertions | _Leverage: existing test helpers and fixtures | _Requirements: 1,2,3 | Success: Tests fail on invalid inputs, pass on valid flows, and run in CI locally; coverage includes error paths_

- [ ] 6. Add integration test for agent-style flowsheet build
  - File: integration-tests/test_flowsheet_tools.py
  - Run add_compound → set_property_package → add_stream → add_unit (separator) → connect → list_objects → set_object_parameter → delete_object against pythonnet-backed worker (or simulated if fixture available); assert IDs, topology, and non-destructive errors.
  - Purpose: Validate end-to-end flowsheet building via MCP tools.
  - _Leverage: integration test harness, golden three-phase separator case, existing session setup utilities_
  - _Requirements: 1, 2, 3_
  - _Prompt: Implement the task for spec mcp-flowsheet-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Integration engineer | Task: Create end-to-end test covering the full flowsheet tool chain with realistic inputs | Restrictions: Reuse fixtures/utilities; clean up sessions; guard against environment assumptions; skip/xfail if DWSIM assets absent with clear reason | _Leverage: integration harness, existing golden cases, session helpers | _Requirements: 1,2,3 | Success: Test exercises full chain, verifies topology/IDs, and surfaces clear diagnostics on failure; passes (or xfails with rationale) in CI/local runs_
