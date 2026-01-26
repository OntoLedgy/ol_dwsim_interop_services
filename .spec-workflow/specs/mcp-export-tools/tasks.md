# Tasks Document: MCP Export Tools and LLM Usability Improvements

## Phase 1: Compound Usability Improvements (C#)

- [x] 1. Create CompoundAliasMapper utility
  - File: `mcp_service/dwsim_worker/DwsimWorker/Utilities/CompoundAliasMapper.cs`
  - Implement static dictionary mapping common aliases to DWSIM canonical names
  - Add `TryResolveAlias(string input, out string canonicalName)` method
  - Add `GetAliasesFor(string canonicalName)` method
  - Include mappings: isobutane, isopentane, CO2, N2, H2O, H2S, nC4-nC6
  - Purpose: Enable LLM agents to use common chemical engineering terminology
  - _Leverage: None (new static utility)_
  - _Requirements: REQ-9_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer specializing in utility classes and chemical engineering domain | Task: Create CompoundAliasMapper static utility class implementing alias-to-canonical name mappings for common compound names (isobutane→Isobutane, CO2→Carbon Dioxide, etc.) | Restrictions: Must be static class with no external dependencies, case-insensitive matching, thread-safe | _Leverage: Existing CompoundAdapter.cs KnownCompounds set for canonical names | Success: All specified aliases resolve correctly, unit tests pass, no runtime exceptions | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 2. Create FuzzyMatcher utility
  - File: `mcp_service/dwsim_worker/DwsimWorker/Utilities/FuzzyMatcher.cs`
  - Implement Levenshtein distance algorithm
  - Add `FindSimilar(string input, IEnumerable<string> candidates, int maxResults, int maxDistance)` method
  - Purpose: Provide compound name suggestions when input doesn't match
  - _Leverage: None (new utility with standard algorithm)_
  - _Requirements: REQ-5_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with algorithm implementation experience | Task: Create FuzzyMatcher static utility implementing Levenshtein distance-based fuzzy matching to find similar strings from a candidate list | Restrictions: Pure algorithm, no external dependencies, efficient for ~100 candidates, configurable max distance | Success: Correctly identifies similar compound names (e.g., "methne" suggests "Methane"), performance <10ms for typical input | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 3. Extend CompoundAdapter with validation and listing
  - File: `mcp_service/dwsim_worker/DwsimWorker/Adapters/CompoundAdapter.cs`
  - Add `ValidateCompound(string compoundName)` returning validation result with suggestions
  - Add `ListCompounds(string pattern, string category, int limit, int offset)` method
  - Integrate CompoundAliasMapper for alias resolution in AddCompound
  - Make compound matching case-insensitive
  - Purpose: Enable compound validation before addition and compound discovery
  - _Leverage: CompoundAliasMapper, FuzzyMatcher, existing KnownCompounds set_
  - _Requirements: REQ-5, REQ-6, REQ-8, REQ-9_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer extending existing domain adapter | Task: Extend CompoundAdapter with ValidateCompound and ListCompounds methods, integrating alias resolution and fuzzy matching for better LLM usability | Restrictions: Must maintain backward compatibility with existing AddCompound, use dependency injection for new utilities if needed | _Leverage: Existing KnownCompounds HashSet, CompoundAliasMapper, FuzzyMatcher | Success: Validation returns suggestions for typos, aliases resolve correctly, listing supports filtering | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 4. Add unit tests for compound utilities
  - File: `mcp_service/dwsim_worker/DwsimWorker.Tests/Utilities/CompoundUtilitiesTests.cs`
  - Test CompoundAliasMapper alias resolution
  - Test FuzzyMatcher distance calculations
  - Test CompoundAdapter validation and listing
  - Purpose: Ensure compound utilities work correctly
  - _Leverage: xUnit, existing test patterns_
  - _Requirements: REQ-5, REQ-6, REQ-8, REQ-9_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer with C# xUnit testing expertise | Task: Create comprehensive unit tests for CompoundAliasMapper, FuzzyMatcher, and extended CompoundAdapter methods | Restrictions: Test both success and failure cases, use xUnit assertions, maintain test isolation | _Leverage: Existing test patterns in DwsimWorker.Tests | Success: >90% code coverage for new code, all edge cases covered | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

## Phase 2: Outlet Stream Auto-Composition (C#)

- [x] 5. Modify StreamAdapter for auto-composition
  - File: `mcp_service/dwsim_worker/DwsimWorker/Adapters/StreamAdapter.cs`
  - Modify `AddStream` to accept null/empty composition when `isSource=false`
  - Auto-generate equal mole fractions from session compounds when composition missing
  - Add validation: error if no compounds in session and auto-composition needed
  - Purpose: Remove requirement for LLM to provide dummy compositions for outlets
  - _Leverage: FlowsheetContext.GetCompounds(), existing AddStream logic_
  - _Requirements: REQ-7_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer modifying existing domain adapter | Task: Modify StreamAdapter.AddStream to auto-generate placeholder composition for outlet streams (isSource=false) using equal mole fractions of session compounds | Restrictions: Must not break existing behavior for feed streams, clear error if no compounds registered | _Leverage: FlowsheetContext for compound list access | Success: Outlet streams can be created without composition, auto-generated composition sums to 1.0 | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 6. Add unit tests for auto-composition
  - File: `mcp_service/dwsim_worker/DwsimWorker.Tests/Adapters/StreamAdapterAutoCompositionTests.cs`
  - Test auto-composition generation with various compound counts
  - Test error case when no compounds registered
  - Test that explicit composition still works
  - Purpose: Ensure auto-composition feature works correctly
  - _Leverage: xUnit, existing StreamAdapter tests_
  - _Requirements: REQ-7_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer testing stream creation features | Task: Create unit tests for StreamAdapter auto-composition feature covering various scenarios (1, 3, 10 compounds, no compounds error case) | Restrictions: Test in isolation with mocked context, verify composition sums to 1.0 | _Leverage: Existing StreamAdapter test patterns | Success: All auto-composition scenarios covered, error cases tested | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

## Phase 3: Export Adapter (C#)

- [x] 7. Create ExportAdapter class
  - File: `mcp_service/dwsim_worker/DwsimWorker/Adapters/ExportAdapter.cs`
  - Implement `ExportToCsv(string filePath, List<string> objectIds)` method
  - Implement `ExportToJson(string format)` method ("summary" or "full")
  - Implement `GenerateReport(string template, string filePath)` method
  - Add file path validation (sandboxing)
  - Purpose: Provide export functionality for simulation results
  - _Leverage: FlowsheetContext, Newtonsoft.Json, existing adapter patterns_
  - _Requirements: REQ-1, REQ-2, REQ-3_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer creating new domain adapter for export operations | Task: Create ExportAdapter implementing CSV, JSON, and Markdown report export for flowsheet streams and units | Restrictions: Validate file paths against allowed directories, handle partial export gracefully, follow existing adapter patterns | _Leverage: FlowsheetContext for stream/unit access, Newtonsoft.Json for serialization | Success: CSV exports readable by Excel, JSON validates against schema, reports include all key data | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 8. Fix SaveCase functionality
  - File: `mcp_service/dwsim_worker/DwsimWorker/Adapters/ExportAdapter.cs`
  - Implement `SaveCase(string filePath)` method
  - Support .dwxmz (compressed) and .dwxml (uncompressed) formats
  - Use DWSIM's native save methods
  - Purpose: Fix the broken save_case MCP tool
  - _Leverage: DWSIM.Flowsheet save methods, FlowsheetContext_
  - _Requirements: REQ-4_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with DWSIM API experience | Task: Implement SaveCase method in ExportAdapter using DWSIM's native flowsheet save functionality for .dwxmz and .dwxml formats | Restrictions: Must validate file extension, handle save errors gracefully, return absolute path on success | _Leverage: DWSIM Flowsheet object save methods | Success: Saved files can be loaded in DWSIM GUI, both formats work correctly | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 9. Add unit tests for ExportAdapter
  - File: `mcp_service/dwsim_worker/DwsimWorker.Tests/Adapters/ExportAdapterTests.cs`
  - Test CSV export format and content
  - Test JSON export (summary and full formats)
  - Test report generation
  - Test file path validation (reject traversal)
  - Purpose: Ensure export functionality works correctly
  - _Leverage: xUnit, temporary file handling, existing test patterns_
  - _Requirements: REQ-1, REQ-2, REQ-3, REQ-4_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer testing export functionality | Task: Create comprehensive unit tests for ExportAdapter covering CSV, JSON, report, and save case operations | Restrictions: Use temp directories for file tests, clean up after tests, verify file contents | _Leverage: xUnit TempFile patterns, System.IO for file verification | Success: All export operations tested, path validation tested, file cleanup works | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

## Phase 4: Python Models (MCP Layer)

- [x] 10. Create export input/output models
  - File: `models/mcp_inputs/export_inputs.py`
  - Define Pydantic models: ExportCsvInput/Output, ExportJsonInput/Output, GenerateReportInput/Output, SaveCaseInput/Output
  - Add validators for file_path extensions
  - Purpose: Type-safe input validation for export tools
  - _Leverage: Pydantic BaseModel, existing flowsheet_build.py patterns_
  - _Requirements: REQ-1, REQ-2, REQ-3, REQ-4_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer creating Pydantic models | Task: Create export tool input/output Pydantic models with proper validation (file extensions, format enums) following existing patterns in flowsheet_build.py | Restrictions: Must use Field validators, proper type hints, model_json_schema() compatible | _Leverage: Existing Pydantic patterns in models/mcp_inputs/ | Success: Models validate correctly, mypy passes, schemas generate for MCP | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 11. Create compound validation models
  - File: `models/mcp_inputs/compound_validation.py`
  - Define Pydantic models: ValidateCompoundsInput/Output, ListCompoundsInput/Output, CompoundInfo, CompoundValidationResult
  - Add validators for list lengths
  - Purpose: Type-safe input validation for compound tools
  - _Leverage: Pydantic BaseModel, existing patterns_
  - _Requirements: REQ-5, REQ-6_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer creating Pydantic models | Task: Create compound validation and listing tool input/output Pydantic models with proper validation | Restrictions: Must validate list has at least 1 item for validate_compounds, proper pagination fields | _Leverage: Existing Pydantic patterns | Success: Models validate correctly, nested models work, mypy passes | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 12. Add Python model unit tests
  - File: `mcp_service/server/tests/unit/test_export_models.py`
  - Test export model validation
  - Test compound validation model validation
  - Test error messages for invalid inputs
  - Purpose: Ensure Pydantic models validate correctly
  - _Leverage: pytest, existing model test patterns_
  - _Requirements: REQ-1 through REQ-6_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python QA Engineer with pytest experience | Task: Create unit tests for export and compound validation Pydantic models testing valid and invalid inputs | Restrictions: Test validation errors are clear, test edge cases | _Leverage: pytest, existing test patterns | Success: All validation rules tested, error messages verified | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

## Phase 5: Python Service and Tools (MCP Layer)

- [x] 13. Extend FlowsheetService with export methods
  - File: `mcp_service/server/dwsim_mcp_server/service/flowsheet_service.py`
  - Add `export_csv()`, `export_json()`, `generate_report()`, `save_case()` methods
  - Add `validate_compounds()`, `list_compounds()` methods
  - Follow existing service method patterns
  - Purpose: Bridge MCP tools to C# adapters
  - _Leverage: Existing FlowsheetService patterns, SessionClient_
  - _Requirements: REQ-1 through REQ-6_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer extending service layer | Task: Extend FlowsheetService with export and compound validation methods following existing patterns (run_session_operation, structured logging) | Restrictions: Must use existing session client, proper error handling, structured logging | _Leverage: Existing FlowsheetService patterns | Success: All methods callable, proper logging, errors propagate correctly | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 14. Create export MCP tools
  - File: `mcp_service/server/dwsim_mcp_server/tools/export.py`
  - Implement `build_export_tools()` returning list of MCP tools
  - Implement `handle_export_tool()` dispatcher
  - Register tools: export_csv, export_json, generate_report, save_case
  - Purpose: Expose export functionality to LLM agents
  - _Leverage: mcp.types, existing tool builder patterns_
  - _Requirements: REQ-1, REQ-2, REQ-3, REQ-4_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer creating MCP tools | Task: Create export MCP tools following existing build_*_tools() pattern with proper tool descriptions for LLM agents | Restrictions: Must follow MCP tool schema, include helpful descriptions, handle errors with SessionError | _Leverage: Existing tools/flowsheet.py patterns | Success: Tools appear in MCP tool list, descriptions are clear, errors handled properly | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 15. Create compound validation MCP tools
  - File: `mcp_service/server/dwsim_mcp_server/tools/compound.py`
  - Implement `build_compound_tools()` returning list of MCP tools
  - Implement `handle_compound_tool()` dispatcher
  - Register tools: validate_compounds, list_available_compounds
  - Purpose: Expose compound validation functionality to LLM agents
  - _Leverage: mcp.types, existing tool builder patterns_
  - _Requirements: REQ-5, REQ-6_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer creating MCP tools | Task: Create compound validation and listing MCP tools with helpful descriptions explaining fuzzy matching and alias support | Restrictions: Must follow MCP tool schema, clear descriptions | _Leverage: Existing tool patterns | Success: Tools work correctly, suggestions returned for invalid compounds | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 16. Register new tools in server
  - File: `mcp_service/server/dwsim_mcp_server/server.py`
  - Import and register export tools
  - Import and register compound tools
  - Add to tool dispatcher routing
  - Purpose: Make new tools available to MCP clients
  - _Leverage: Existing tool registration patterns_
  - _Requirements: All_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer modifying MCP server | Task: Register export and compound tools in the MCP server, adding to tool list and dispatcher routing | Restrictions: Must not break existing tools, maintain tool ordering | _Leverage: Existing server.py tool registration | Success: New tools appear in list_tools response, dispatch works correctly | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 17. Add Python tool unit tests
  - File: `mcp_service/server/tests/unit/test_export_tools.py`
  - Test tool handler dispatch
  - Test error handling
  - Mock service layer
  - Purpose: Ensure MCP tools work correctly
  - _Leverage: pytest, existing tool test patterns_
  - _Requirements: All_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python QA Engineer testing MCP tools | Task: Create unit tests for export and compound MCP tools with mocked service layer | Restrictions: Mock all service calls, test error code generation | _Leverage: Existing tool test patterns | Success: All tools tested, error handling verified | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

## Phase 6: Integration Testing

- [x] 18. Create integration tests for compound workflow
  - File: `mcp_service/server/tests/integration/test_compound_usability.py`
  - Test alias resolution end-to-end
  - Test fuzzy matching suggestions
  - Test case-insensitive matching
  - Purpose: Verify compound usability improvements work together
  - _Leverage: pytest, real DWSIM worker_
  - _Requirements: REQ-5, REQ-6, REQ-8, REQ-9_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Integration Test Engineer | Task: Create end-to-end tests for compound validation workflow testing aliases, fuzzy matching, and case-insensitivity | Restrictions: Use real DWSIM worker (no mocks), clean up sessions after tests | _Leverage: Existing integration test patterns | Success: Full workflow tested, aliases work, suggestions returned | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 19. Create integration tests for export workflow
  - File: `mcp_service/server/tests/integration/test_export_workflow.py`
  - Test CSV export with real simulation
  - Test JSON export formats
  - Test report generation
  - Test save/load case round-trip
  - Purpose: Verify export functionality works end-to-end
  - _Leverage: pytest, real DWSIM worker, temp files_
  - _Requirements: REQ-1, REQ-2, REQ-3, REQ-4_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Integration Test Engineer | Task: Create end-to-end tests for export workflow including CSV, JSON, report, and save/load operations | Restrictions: Use temp directories, verify file contents, clean up | _Leverage: Existing integration test patterns | Success: All export operations verified, files contain correct data | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 20. Create integration test for auto-composition
  - File: `mcp_service/server/tests/integration/test_auto_composition.py`
  - Test outlet stream creation without composition
  - Test simulation runs with auto-composed outlets
  - Test error when no compounds registered
  - Purpose: Verify auto-composition feature works in real simulations
  - _Leverage: pytest, real DWSIM worker_
  - _Requirements: REQ-7_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Integration Test Engineer | Task: Create end-to-end tests for auto-composition feature verifying outlet streams work without explicit composition | Restrictions: Test with real simulation, verify calculated values override placeholders | _Leverage: Existing integration test patterns | Success: Auto-composition works, simulation converges, results correct | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

## Phase 7: Documentation

- [x] 21. Update MCP tool documentation
  - File: `docs/api/mcp-tools.md` (create or update)
  - Document all new tools with examples
  - Document compound alias mappings
  - Add troubleshooting section
  - Purpose: Help LLM agents and users understand new capabilities
  - _Leverage: Existing documentation patterns_
  - _Requirements: All_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Technical Writer | Task: Create comprehensive documentation for new MCP tools including examples, alias mappings, and troubleshooting | Restrictions: Follow existing doc format, include code examples | _Leverage: Existing docs structure | Success: All tools documented, examples work, clear troubleshooting | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_

- [x] 22. Update tool descriptions for LLM clarity
  - Files: All tool definition files
  - Review and improve tool descriptions for LLM comprehension
  - Add usage hints and common patterns
  - Ensure descriptions mention alias support
  - Purpose: Make tools self-documenting for LLM agents
  - _Leverage: Existing tool descriptions_
  - _Requirements: All_
  - _Prompt: Implement the task for spec mcp-export-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: UX Writer specializing in AI/LLM interfaces | Task: Review and improve all MCP tool descriptions for clarity, adding usage hints and mentioning alias support | Restrictions: Keep descriptions concise but informative | Success: LLM agents can use tools correctly from descriptions alone | Instructions: Mark task [-] in tasks.md when starting, use log-implementation tool after completion, mark [x] when complete_
