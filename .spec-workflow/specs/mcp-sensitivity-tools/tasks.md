# Tasks Document

## Phase 1: Pydantic Models

- [ ] 1. Create sensitivity input models
  - File: `models/mcp_inputs/sensitivity_inputs.py`
  - Define `VariableSpec`, `RangeSpec`, `OutputSpec` base models
  - Define `SensitivityAnalysisRequest` with session_id, variable, range, steps, outputs
  - Define `ParameterSweepRequest` with multiple variables and max_combinations limit
  - Add validators: steps between 2-100, range min < max
  - Purpose: Provide validated input schemas for sensitivity MCP tools
  - _Leverage: `models/mcp_inputs/flash_inputs.py` for Pydantic patterns_
  - _Requirements: 1.1, 1.2, 2.1, 2.2_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in Pydantic models | Task: Create Pydantic input models for sensitivity analysis and parameter sweep tools following requirements 1.1, 1.2, 2.1, 2.2. Models must validate step counts (2-100), ensure range min < max, and support multiple variables for sweeps. Reference existing flash_inputs.py for patterns. | Restrictions: Do not modify existing models, use Field constraints for validation, maintain snake_case naming | Success: All models pass mypy, validators reject invalid inputs (steps=0, min>max), models serialize to JSON schema correctly | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 2. Create optimization input models
  - File: `models/mcp_inputs/sensitivity_inputs.py` (append)
  - Define `ObjectiveSpec` with object_id, property_name, direction (minimize/maximize)
  - Define `VariableWithBounds` with object_id, property_name, lower, upper, initial
  - Define `ConstraintSpec` for inequality constraints
  - Define `OptimizationRequest` with objective, variables, constraints, max_iterations
  - Purpose: Provide validated input schema for optimization MCP tool
  - _Leverage: Existing patterns in sensitivity_inputs.py from task 1_
  - _Requirements: 3.1, 3.2, 3.3, 3.4_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in Pydantic models | Task: Create Pydantic input models for optimization tool following requirements 3.1-3.4. Include ObjectiveSpec with direction enum, VariableWithBounds with bound validation, ConstraintSpec for constraints, and OptimizationRequest aggregating them. | Restrictions: Append to existing sensitivity_inputs.py, use Literal for direction enum, validate lower < upper for bounds | Success: Models validate correctly, direction accepts only "minimize"/"maximize", bounds reject lower >= upper | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 3. Create sensitivity result models
  - File: `models/responses/sensitivity_results.py`
  - Define `ResultRow` with inputs dict, outputs dict, converged bool, error message
  - Define `SensitivityStudyResult` with study_id, status, rows, completed/total steps, elapsed time
  - Define `StudyStatus` for progress reporting
  - Define `OptimizationResult` with optimal_values, objective_value, converged status
  - Purpose: Provide structured output models for sensitivity/optimization tools
  - _Leverage: `models/responses/flash_results.py` for response patterns_
  - _Requirements: 1.4, 2.3, 3.2, 4.1_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in Pydantic models | Task: Create Pydantic response models for sensitivity studies following requirements 1.4, 2.3, 3.2, 4.1. Include ResultRow for individual data points, SensitivityStudyResult for full study results with partial failure support, StudyStatus for progress, OptimizationResult for optimization outcomes. | Restrictions: Use Literal for status enums, support null outputs for failed steps, include cancelled flag | Success: Models serialize to clean JSON, partial results representable, status field accepts defined values only | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 4. Export models in package __init__.py
  - File: `models/mcp_inputs/__init__.py` (modify)
  - File: `models/responses/__init__.py` (modify)
  - Export all new sensitivity models from package init files
  - Purpose: Make models importable from package root
  - _Leverage: Existing export patterns in __init__.py files_
  - _Requirements: All_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Update package __init__.py files to export new sensitivity models. Add imports for SensitivityAnalysisRequest, ParameterSweepRequest, OptimizationRequest to mcp_inputs/__init__.py. Add imports for SensitivityStudyResult, OptimizationResult, StudyStatus to responses/__init__.py. | Restrictions: Follow existing import patterns, maintain alphabetical ordering if present | Success: `from models.mcp_inputs import SensitivityAnalysisRequest` works, all new models importable | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

## Phase 2: Sensitivity Service

- [ ] 5. Create SensitivityService skeleton
  - File: `mcp_service/server/dwsim_mcp_server/services/sensitivity_service.py`
  - Create `SensitivityService` class with constructor accepting `LimitedSessionClient`
  - Add stub methods: `run_sensitivity_analysis`, `run_parameter_sweep`, `run_optimization`
  - Add stub methods: `get_study_status`, `cancel_study`, `export_results`
  - Add internal `_active_studies: dict[str, StudyState]` for tracking
  - Purpose: Establish service structure before implementing logic
  - _Leverage: `services/thermodynamics_service.py` for service patterns_
  - _Requirements: 1.1, 4.1, 4.2_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in service architecture | Task: Create SensitivityService class skeleton following patterns from thermodynamics_service.py. Include constructor with LimitedSessionClient injection, stub async methods for all operations, internal study tracking dict. | Restrictions: All methods async, use type hints, follow existing service naming conventions | Success: Class instantiates without error, methods have correct signatures, type hints pass mypy | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 6. Implement single-variable sensitivity analysis
  - File: `mcp_service/server/dwsim_mcp_server/services/sensitivity_service.py`
  - Implement `run_sensitivity_analysis` method:
    - Generate study_id (UUID)
    - Calculate step values from range and steps count
    - Loop: set parameter → run simulation → collect outputs
    - Handle step failures (record null, continue)
    - Return `SensitivityStudyResult` with all rows
  - Purpose: Core sensitivity analysis logic
  - _Leverage: `session_client.run_calculation` for simulation, `flowsheet_service.set_object_parameter`_
  - _Requirements: 1.1, 1.2, 1.3, 1.4_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with simulation experience | Task: Implement run_sensitivity_analysis method following requirements 1.1-1.4. Generate evenly-spaced parameter values, iterate through each step setting parameter and running simulation, collect requested outputs, handle convergence failures gracefully by recording null and continuing. Use existing set_object_parameter and run_calculation. | Restrictions: Must handle partial failures, do not abort on single step failure, track elapsed time | Success: 5-step sensitivity completes, failed steps recorded with null outputs, result has all 5 rows | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 7. Implement parameter sweep (multi-variable)
  - File: `mcp_service/server/dwsim_mcp_server/services/sensitivity_service.py`
  - Implement `run_parameter_sweep` method:
    - Validate total combinations ≤ max_combinations
    - Generate Cartesian product of all variable ranges
    - Execute each combination (set all params → run → collect)
    - Handle failures gracefully
  - Purpose: Multi-dimensional parameter study capability
  - _Leverage: `itertools.product` for combinations, existing sensitivity logic_
  - _Requirements: 2.1, 2.2, 2.3_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Implement run_parameter_sweep method following requirements 2.1-2.3. Use itertools.product to generate parameter combinations, validate count against limit before execution, reuse step execution pattern from sensitivity_analysis. | Restrictions: Reject if combinations > max_combinations, use same result format as sensitivity, set all variables before each run | Success: 3×3 sweep produces 9 rows, limit enforcement rejects 11×11 sweep, partial failures handled | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 8. Implement optimization
  - File: `mcp_service/server/dwsim_mcp_server/services/sensitivity_service.py`
  - Implement `run_optimization` method:
    - Define objective function that sets params and runs simulation
    - Use scipy.optimize.minimize (or DWSIM's optimizer if available)
    - Handle constraints via penalty or scipy constraints
    - Return `OptimizationResult` with optimal values
  - Purpose: Find optimal operating conditions
  - _Leverage: `scipy.optimize.minimize`, existing simulation runner_
  - _Requirements: 3.1, 3.2, 3.3, 3.4_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with optimization experience | Task: Implement run_optimization method following requirements 3.1-3.4. Create objective function wrapper that sets variables, runs simulation, extracts objective property. Use scipy.optimize.minimize with bounds. Handle max_iterations limit and non-convergence by returning best-so-far. | Restrictions: Use scipy for optimization, respect variable bounds, return partial result on max iterations | Success: Simple optimization converges, respects bounds, returns valid OptimizationResult | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 9. Implement progress tracking and cancellation
  - File: `mcp_service/server/dwsim_mcp_server/services/sensitivity_service.py`
  - Implement `get_study_status` method returning `StudyStatus`
  - Implement `cancel_study` method:
    - Set cancellation flag on study state
    - Study loop checks flag and exits early
    - Return partial results collected so far
  - Track study state in `_active_studies` dict
  - Purpose: Enable monitoring and early termination of long studies
  - _Leverage: asyncio.Event for cancellation signaling_
  - _Requirements: 4.1, 4.2, 4.3_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with async experience | Task: Implement get_study_status and cancel_study methods following requirements 4.1-4.3. Use internal dict to track active studies with progress and cancellation flag. Study loops should check cancellation flag between steps. Return partial results on cancellation. | Restrictions: Thread-safe access to study state, cancelled studies return collected data, estimate remaining time based on average step duration | Success: Status shows correct progress, cancellation stops study within one step, partial results returned | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 10. Implement result export
  - File: `mcp_service/server/dwsim_mcp_server/services/sensitivity_service.py`
  - Implement `export_results` method:
    - Validate file path using `resolve_case_path`
    - Export to CSV or JSON based on file extension
    - CSV: header row + data rows
    - JSON: full result object serialized
  - Purpose: Persist study results for external analysis
  - _Leverage: `resolve_case_path` from utils, csv module, json module_
  - _Requirements: 5.1, 5.2, 5.3_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Implement export_results method following requirements 5.1-5.3. Validate path with resolve_case_path, detect format from extension (.csv or .json), write CSV with headers or JSON with full structure. | Restrictions: Reject invalid paths, use existing path validation, include all columns in CSV header | Success: CSV readable by pandas, JSON round-trips correctly, invalid paths rejected | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

## Phase 3: MCP Tools

- [ ] 11. Create sensitivity tools module
  - File: `mcp_service/server/dwsim_mcp_server/tools/sensitivity.py`
  - Implement `build_sensitivity_tools() → list[types.Tool]`:
    - `sensitivity_analysis` tool with input/output schemas
    - `parameter_sweep` tool with input/output schemas
    - `optimize` tool with input/output schemas
    - `get_study_status` tool
    - `cancel_study` tool
    - `export_study_results` tool
  - Include clear descriptions for LLM discoverability
  - Purpose: Define MCP tool interfaces for sensitivity operations
  - _Leverage: `tools/analysis.py` for tool definition patterns_
  - _Requirements: 1.1, 2.1, 3.1, 4.1, 5.1_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in MCP | Task: Create sensitivity.py with build_sensitivity_tools function returning Tool definitions following patterns from analysis.py. Each tool needs name, title, description (LLM-friendly), inputSchema, outputSchema. | Restrictions: Use Pydantic model_json_schema() for schemas, descriptions should explain what each tool does for LLM agents | Success: All 6 tools defined with valid schemas, descriptions explain purpose clearly | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 12. Implement tool handler dispatch
  - File: `mcp_service/server/dwsim_mcp_server/tools/sensitivity.py`
  - Implement `handle_sensitivity_tool(tool_name, arguments, dependencies)`:
    - Dispatch to appropriate SensitivityService method
    - Validate inputs with Pydantic models
    - Format results for MCP response
    - Handle errors with proper CallToolResult
  - Purpose: Route MCP tool calls to service layer
  - _Leverage: `handle_analysis_tool` patterns, error handling utilities_
  - _Requirements: All_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in MCP | Task: Implement handle_sensitivity_tool dispatcher following patterns from handle_analysis_tool. Validate arguments with Pydantic models, call appropriate service method, return model_dump() on success, return CallToolResult with isError on failure. | Restrictions: Follow existing error handling patterns, use ValidationError catch for input errors, log exceptions | Success: Valid calls return dict results, invalid inputs return VALIDATION_ERROR, service errors propagate correctly | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 13. Register tools in registry
  - File: `mcp_service/server/dwsim_mcp_server/tools/registry.py` (modify)
  - Import `build_sensitivity_tools`, `handle_sensitivity_tool`
  - Add sensitivity tools to tool list
  - Add sensitivity tool names to dispatch logic in `call_tool`
  - Purpose: Make sensitivity tools available via MCP server
  - _Leverage: Existing registration pattern for analysis_tools_
  - _Requirements: All_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Update registry.py to include sensitivity tools. Import build_sensitivity_tools and handle_sensitivity_tool, add to tool collection, add dispatch case in call_tool handler. | Restrictions: Follow existing pattern exactly, maintain tool name sets, add to tool_by_name dict | Success: sensitivity_analysis appears in list_tools output, tool calls route correctly | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 14. Inject SensitivityService in dependencies
  - File: `mcp_service/server/dwsim_mcp_server/server.py` (modify)
  - Import `SensitivityService`
  - Add `sensitivity_service` to `ServerDependencies` class
  - Initialize service with session_client in constructor
  - Purpose: Make service available to tool handlers
  - _Leverage: Existing ThermodynamicsService injection pattern_
  - _Requirements: All_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Update server.py to inject SensitivityService into ServerDependencies. Import service class, add as attribute, initialize in constructor with session_client dependency. | Restrictions: Follow ThermodynamicsService pattern exactly, maintain existing dependencies | Success: ServerDependencies has sensitivity_service attribute, service instantiated on server start | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

## Phase 4: Unit Tests

- [ ] 15. Test sensitivity input models
  - File: `mcp_service/server/tests/unit/test_sensitivity_models.py`
  - Test `SensitivityAnalysisRequest` validation (steps bounds, range validation)
  - Test `ParameterSweepRequest` validation
  - Test `OptimizationRequest` validation (bounds, direction)
  - Test serialization to JSON schema
  - Purpose: Ensure input validation catches invalid requests
  - _Leverage: `tests/unit/test_flash_models.py` for test patterns_
  - _Requirements: 1.1, 2.1, 3.1_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer with pytest expertise | Task: Create unit tests for sensitivity input models. Test valid inputs pass, invalid steps (0, 101) rejected, range min >= max rejected, optimization direction only accepts minimize/maximize. | Restrictions: Use pytest, test both valid and invalid cases, test boundary conditions | Success: All validation rules have test coverage, tests pass, edge cases covered | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 16. Test sensitivity tools dispatch
  - File: `mcp_service/server/tests/unit/test_sensitivity_tools.py`
  - Mock `SensitivityService` methods
  - Test each tool dispatches to correct service method
  - Test validation errors return proper CallToolResult
  - Test missing service returns SERVICE_UNAVAILABLE
  - Purpose: Verify tool layer correctly routes to service
  - _Leverage: `tests/unit/test_flowsheet_tools.py` for mock patterns_
  - _Requirements: All_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer with pytest expertise | Task: Create unit tests for sensitivity tools dispatch following patterns from test_flowsheet_tools.py. Mock SensitivityService, verify each tool calls correct method, test validation error handling, test missing service error. | Restrictions: Use SimpleNamespace for mocks, test all 6 tools, verify error codes | Success: All tools tested for happy path and error cases, mocks verify correct method calls | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 17. Test SensitivityService logic
  - File: `mcp_service/server/tests/unit/test_sensitivity_service.py`
  - Mock `LimitedSessionClient` and simulation runner
  - Test sensitivity analysis generates correct number of steps
  - Test parameter sweep generates correct combinations
  - Test step failure handling (partial results)
  - Test cancellation returns partial results
  - Purpose: Verify service orchestration logic
  - _Leverage: Mock patterns from existing service tests_
  - _Requirements: 1.2, 1.3, 2.2, 4.2, 4.3_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer with pytest expertise | Task: Create unit tests for SensitivityService. Mock session client, test 5-step sensitivity produces 5 rows, test 3×3 sweep produces 9 rows, test one failed step still returns other results, test cancellation mid-study returns partial data. | Restrictions: Use async test support, mock all external calls, verify result structure | Success: All service methods tested, partial failure handling verified, cancellation tested | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

## Phase 5: Integration Tests

- [ ] 18. Integration test: sensitivity analysis end-to-end
  - File: `mcp_service/server/tests/integration/test_sensitivity_integration.py`
  - Create session with three-phase separator
  - Run 5-point pressure sensitivity (vary inlet pressure)
  - Verify 5 result rows returned
  - Verify outputs change as expected with pressure
  - Purpose: Validate full sensitivity workflow with real DWSIM
  - _Leverage: Existing integration test fixtures_
  - _Requirements: 1.1, 1.2, 1.3, 1.4_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Integration Test Engineer | Task: Create end-to-end integration test for sensitivity analysis. Set up three-phase separator session, run 5-point pressure sensitivity, verify all 5 rows have outputs, verify output values differ across rows (sensitivity detected). | Restrictions: Requires DWSIM assemblies, may be slow (mark appropriately), clean up session after test | Success: Test passes with real DWSIM, results show pressure sensitivity, no resource leaks | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 19. Integration test: optimization end-to-end
  - File: `mcp_service/server/tests/integration/test_sensitivity_integration.py` (append)
  - Create session with configurable separator
  - Optimize: maximize vapor fraction by varying temperature
  - Verify optimization converges
  - Verify optimal temperature within bounds
  - Purpose: Validate optimization workflow with real DWSIM
  - _Leverage: Existing integration test setup_
  - _Requirements: 3.1, 3.2, 3.3_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Integration Test Engineer | Task: Create end-to-end integration test for optimization. Set up separator session, optimize inlet temperature to maximize vapor fraction within bounds. Verify optimization converges, optimal value within bounds, objective improved from initial. | Restrictions: May require multiple iterations (allow reasonable timeout), verify bounds respected | Success: Optimization converges, result within bounds, vapor fraction higher than at bounds | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_

- [ ] 20. Integration test: export functionality
  - File: `mcp_service/server/tests/integration/test_sensitivity_integration.py` (append)
  - Run sensitivity study
  - Export to CSV, verify file created and readable
  - Export to JSON, verify structure correct
  - Test invalid path rejection
  - Purpose: Validate export functionality end-to-end
  - _Leverage: tempfile for test directories_
  - _Requirements: 5.1, 5.2, 5.3_
  - _Prompt: Implement the task for spec mcp-sensitivity-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Integration Test Engineer | Task: Create integration tests for result export. Run sensitivity study, export to temp CSV file, verify headers and row count with pandas. Export to JSON, verify structure matches model. Test that path outside allowed directories is rejected. | Restrictions: Use tempfile for safe file creation, clean up files after test | Success: CSV readable by pandas with correct columns, JSON deserializes to model, invalid path rejected | After completing: Mark task in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark task complete in tasks.md_
