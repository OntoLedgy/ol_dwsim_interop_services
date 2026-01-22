# Tasks Document: MCP Resource Providers

## Overview

This document breaks down the MCP Resource Providers feature into atomic implementation tasks. Each task targets 1-3 files and includes a prompt for implementation guidance.

---

## Phase 1: Foundation and Models

- [x] 1. Create resource data models
  - Files: `models/resources/__init__.py`, `models/resources/resource_metadata.py`, `models/resources/sample_case_info.py`, `models/resources/documentation_topic.py`
  - Define Pydantic models for resource metadata, sample case info, and documentation topics
  - Purpose: Establish type-safe data structures for resource providers
  - _Leverage: `models/` existing patterns, Pydantic BaseModel_
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer specializing in Pydantic models and data validation
    Task: Create resource data models in models/resources/ following existing patterns. Create ResourceMetadata (uri, name, description, mime_type), SampleCaseInfo (name, description, compounds, unit_operations, complexity, file_path), and DocumentationTopic (topic, title, description, sections) models.
    Restrictions: Follow existing model patterns in models/, use Pydantic v2 syntax, do not modify existing models
    _Leverage: models/cape_open/, models/dwsim/ for patterns
    _Requirements: Requirements 5.1-5.5 (Resource Content Formatting)
    Success: All models pass mypy type checking, have proper field validators, and follow project conventions. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

- [x] 2. Create session result resource model
  - File: `models/resources/session_result_resource.py`
  - Define SessionResultResource model with session_id, object_id, object_type, properties, and units
  - Purpose: Structure for detailed simulation results
  - _Leverage: `models/responses/` for result patterns_
  - _Requirements: 1.1, 1.2, 1.6_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with DWSIM domain knowledge
    Task: Create SessionResultResource Pydantic model with session_id (str), object_id (Optional[str]), object_type (Literal["stream", "unit", "flowsheet"]), properties (Dict[str, Any]), and units (Dict[str, str]) for SI unit labels.
    Restrictions: Follow existing response model patterns, ensure JSON serializable, use proper type hints
    _Leverage: models/responses/simulation_result_response.py
    _Requirements: Requirements 1.1, 1.2, 1.6 (Session Results Resource)
    Success: Model validates correctly, handles optional object_id, has CAPE-OPEN property compatibility. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

---

## Phase 2: Resource Provider Protocol and Base

- [x] 3. Create ResourceProvider protocol and base class
  - Files: `mcp_service/server/dwsim_mcp_server/resources/base.py`
  - Define Protocol for resource providers with get_resource_templates, list_resources, read_resource methods
  - Purpose: Establish common interface for all resource providers
  - _Leverage: `mcp.types` for Resource and ResourceTemplate types_
  - _Requirements: 4.1, 4.2_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer specializing in protocols and abstract patterns
    Task: Create ResourceProvider Protocol in resources/base.py defining: get_resource_templates() -> List[types.ResourceTemplate], list_resources() -> List[types.Resource], read_resource(uri: str) -> types.ResourceContents. Include a BaseResourceProvider abstract class with common utilities (URI parsing, error handling).
    Restrictions: Use typing.Protocol for interface, async methods where needed, follow MCP SDK patterns
    _Leverage: mcp.types module, existing tools/registry.py patterns
    _Requirements: Requirements 4.1, 4.2 (Resource Discovery and Listing)
    Success: Protocol is well-defined, base class provides reusable utilities, follows MCP SDK conventions. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

---

## Phase 3: Documentation Resource Provider

- [x] 4. Create documentation resource provider
  - File: `mcp_service/server/dwsim_mcp_server/resources/docs.py`
  - Implement DocsProvider class with topic listing and content retrieval
  - Purpose: Serve DWSIM documentation to LLM agents
  - _Leverage: `resources/base.py`, `ServerSettings`_
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with file I/O and caching expertise
    Task: Create DocsProvider class implementing ResourceProvider protocol. Support resource://docs (list topics) and resource://docs/{topic} (get content). Read markdown files from configurable docs_path. Cache content after first read. Return NotFound error with available topics list if topic missing.
    Restrictions: Use async file I/O, implement caching, return markdown mime_type, handle missing files gracefully
    _Leverage: resources/base.py, config/server_settings.py
    _Requirements: Requirements 2.1-2.7 (Documentation Resource Provider)
    Success: Lists all available topics, retrieves content by topic, caches results, returns proper errors. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

- [x] 5. Create documentation content files
  - Files: `docs/resources/index.md`, `docs/resources/unit-operations.md`, `docs/resources/property-packages.md`, `docs/resources/compounds.md`
  - Write initial documentation content for LLM consumption
  - Purpose: Provide reference materials for agents
  - _Leverage: DWSIM documentation, CAPE-OPEN standards_
  - _Requirements: 2.3, 2.4, 2.5_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Technical Writer with chemical engineering domain knowledge
    Task: Create markdown documentation files in docs/resources/: index.md (overview and topic list), unit-operations.md (separator, mixer, heater, pump descriptions and parameters), property-packages.md (Peng-Robinson, SRK, NRTL applicability), compounds.md (database structure, common compounds). Format for LLM consumption with clear headings and examples.
    Restrictions: Use proper markdown formatting, include code examples where helpful, focus on practical guidance
    _Leverage: DWSIM documentation, existing docs/ content
    _Requirements: Requirements 2.3, 2.4, 2.5 (unit-operations, property-packages, compounds docs)
    Success: Documentation is comprehensive, well-structured, and useful for LLM agents configuring simulations. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

---

## Phase 4: Sample Cases Resource Provider

- [x] 6. Create sample cases resource provider
  - File: `mcp_service/server/dwsim_mcp_server/resources/samples.py`
  - Implement SamplesProvider class with case listing, metadata, and flowsheet structure
  - Purpose: Provide sample flowsheets for agents to reference and load
  - _Leverage: `resources/base.py`, `ServerSettings.case_storage_roots`_
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with file system and XML parsing expertise
    Task: Create SamplesProvider class implementing ResourceProvider protocol. Support resource://cases (list cases with descriptions), resource://cases/{name} (metadata), resource://cases/{name}/flowsheet (topology). Scan case_storage_roots for .dwxmz files, extract metadata. Return NotFound with available cases list if case missing.
    Restrictions: Use resolve_case_path for path validation, parse DWSIM case files safely, handle corrupt files gracefully
    _Leverage: resources/base.py, utils path validation, DWSIM case file format
    _Requirements: Requirements 3.1-3.6 (Sample Cases Resource Provider)
    Success: Lists available cases with metadata, extracts flowsheet structure, integrates with load_case tool. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

- [x] 7. Create sample case files
  - Files: `cases/samples/three-phase-separator.dwxmz`, `cases/samples/simple-flash.dwxmz`, sample case metadata JSON
  - Create sample simulation cases for common scenarios
  - Purpose: Provide working examples agents can load and study
  - _Leverage: DWSIM GUI to create cases_
  - _Requirements: 3.4_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Chemical Engineer with DWSIM expertise
    Task: Create sample DWSIM case files in cases/samples/: three-phase-separator.dwxmz (methane/ethane/propane/water mixture), simple-flash.dwxmz (T-P flash example). Include metadata JSON files describing each case (name, description, compounds, unit_operations, complexity).
    Restrictions: Use realistic but simple parameters, ensure cases are solvable, include metadata for discovery
    _Leverage: Existing DWSIM case files in docs/samples/
    _Requirements: Requirement 3.4 (loadable via load_case tool)
    Success: Cases load successfully, simulate without errors, demonstrate key DWSIM capabilities. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

---

## Phase 5: Session Results Resource Provider

- [x] 8. Create session results resource provider
  - File: `mcp_service/server/dwsim_mcp_server/resources/results.py`
  - Implement ResultsProvider class with session result access
  - Purpose: Serve detailed simulation results larger than tool responses
  - _Leverage: `resources/base.py`, `LimitedSessionClient`, `SessionClient.get_calculation_results`_
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with MCP and DWSIM integration expertise
    Task: Create ResultsProvider class implementing ResourceProvider protocol. Support resource://session/{sessionId}/results (all objects) and resource://session/{sessionId}/results/{objectId} (specific object). Use LimitedSessionClient to validate sessions and get_calculation_results for data. Return NotFound for invalid sessions/objects, InvalidState if no results available.
    Restrictions: Validate session existence, handle pagination for large results, use CAPE-OPEN property names
    _Leverage: ipc/limited_session_client.py, ipc/session_client.py get_calculation_results
    _Requirements: Requirements 1.1-1.6 (Session Results Resource Provider)
    Success: Returns detailed results for valid sessions, handles missing sessions/objects gracefully, formats with CAPE-OPEN properties. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

---

## Phase 6: Resource Registration and Server Integration

- [x] 9. Create resource registry
  - File: `mcp_service/server/dwsim_mcp_server/resources/registry.py`
  - Implement resource registration with MCP server
  - Purpose: Wire up all resource providers to server handlers
  - _Leverage: `tools/registry.py` pattern, MCP Server decorators_
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with MCP SDK expertise
    Task: Create register_resources(server, dependencies) function in resources/registry.py. Instantiate DocsProvider, SamplesProvider, ResultsProvider. Register @server.list_resources() handler aggregating all providers. Register @server.read_resource() handler routing by URI scheme to appropriate provider. Log registration like tools/registry.py.
    Restrictions: Follow tools/registry.py patterns exactly, handle provider initialization errors, support dynamic session resources
    _Leverage: tools/registry.py, MCP Server decorators
    _Requirements: Requirements 4.1-4.5 (Resource Discovery and Listing)
    Success: All resources discoverable via list_resources, read_resource routes correctly, grouped by category. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

- [x] 10. Integrate resources into server bootstrap
  - Files: `mcp_service/server/dwsim_mcp_server/server.py`, `mcp_service/server/dwsim_mcp_server/resources/__init__.py`
  - Add resource registration to server startup
  - Purpose: Enable resource access in running server
  - _Leverage: Existing server.py structure_
  - _Requirements: 4.1_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with server architecture expertise
    Task: Modify server.py to call register_resources(server, dependencies) in create_server function. Update resources/__init__.py to export register_resources and provider classes. Add docs_path and sample_cases_path to ServerSettings if not present.
    Restrictions: Maintain existing server structure, do not break tool registration, add proper imports
    _Leverage: server.py existing patterns, tools registration
    _Requirements: Requirement 4.1 (list_resources returns all available resources)
    Success: Server starts with resources registered, list_resources returns docs/cases/session resources. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

- [x] 11. Add configuration settings for resources
  - File: `mcp_service/server/dwsim_mcp_server/config/server_settings.py`
  - Add docs_path, sample_cases_path, max_resource_size_kb settings
  - Purpose: Make resource paths configurable
  - _Leverage: Existing ServerSettings patterns_
  - _Requirements: Non-functional (Configuration)_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with Pydantic settings expertise
    Task: Add to ServerSettings: docs_path (str, default "./docs/resources", env DWSIM_DOCS_PATH), sample_cases_path (str, default "./cases/samples", env DWSIM_SAMPLE_CASES_PATH), max_resource_size_kb (int, default 100, env DWSIM_MAX_RESOURCE_SIZE_KB).
    Restrictions: Follow existing Field patterns with validation_alias, add descriptions, maintain backward compatibility
    _Leverage: config/server_settings.py existing patterns
    _Requirements: Design document configuration section
    Success: Settings load from env vars, have sensible defaults, are accessible to resource providers. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

---

## Phase 7: Testing

- [x] 12. Create unit tests for resource providers
  - Files: `mcp_service/server/tests/unit/test_docs_provider.py`, `mcp_service/server/tests/unit/test_samples_provider.py`, `mcp_service/server/tests/unit/test_results_provider.py`
  - Write unit tests for each provider with mocked dependencies
  - Purpose: Ensure provider reliability and error handling
  - _Leverage: `tests/unit/` existing patterns, pytest fixtures_
  - _Requirements: All requirements (testing)_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: QA Engineer with pytest expertise
    Task: Create unit tests for DocsProvider (topic listing, content retrieval, missing topic), SamplesProvider (case listing, metadata, missing case), ResultsProvider (session results, object filtering, missing session). Mock file I/O and SessionClient. Test error scenarios.
    Restrictions: Mock all external dependencies, test async methods properly, follow existing test patterns
    _Leverage: tests/unit/ existing patterns, pytest-asyncio
    _Requirements: All requirements - testing scenarios
    Success: All providers tested, error cases covered, tests run independently. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

- [x] 13. Create integration tests for MCP resource protocol
  - File: `mcp_service/server/tests/integration/test_resource_protocol.py`
  - Write integration tests for list_resources and read_resource
  - Purpose: Validate end-to-end resource access via MCP protocol
  - _Leverage: `tests/integration/` existing patterns_
  - _Requirements: All requirements (integration)_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Integration Test Engineer
    Task: Create integration tests that start MCP server and test list_resources (returns all categories), read_resource for docs topic, read_resource for sample case, read_resource for session results (requires creating session and running simulation first).
    Restrictions: Use real server startup, clean up resources after tests, handle async properly
    _Leverage: tests/integration/ existing patterns
    _Requirements: All requirements - end-to-end validation
    Success: Tests validate full resource flow, cover happy path and error cases, run reliably. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

---

## Phase 8: Documentation and Cleanup

- [x] 14. Update module exports and documentation
  - Files: `mcp_service/server/dwsim_mcp_server/resources/__init__.py`, update README if needed
  - Ensure all public APIs are exported and documented
  - Purpose: Complete the feature with proper module structure
  - _Leverage: Existing __init__.py patterns_
  - _Requirements: All (completion)_
  - _Prompt: Implement the task for spec mcp-resource-providers, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with module organization expertise
    Task: Update resources/__init__.py to export DocsProvider, SamplesProvider, ResultsProvider, register_resources, and all public types. Add module docstring describing resource providers. Verify all imports work correctly.
    Restrictions: Only export public APIs, maintain clean namespace, add proper docstrings
    _Leverage: Other __init__.py files in the project
    _Requirements: Completion of all requirements
    Success: Clean module exports, all public APIs accessible, proper documentation. Mark task in-progress in tasks.md before starting, use log-implementation tool after completion, then mark complete._

