# Tasks Document

- [x] 1. Add session request models in models/requests/
  - File: models/requests/close_session_request.py
  - File: models/requests/save_case_request.py
  - File: models/requests/load_case_request.py
  - Define Pydantic request schemas for close/save/load session tools
  - Purpose: Standardize tool input validation
  - _Leverage: models/requests/create_session_request.py_
  - _Requirements: 2, 3, 4, 5_
  - _Prompt: Implement the task for spec mcp-session-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in Pydantic models | Task: Create request models for close_session, save_case, and load_case in models/requests with clear field names, validations, and schema examples aligned to requirements 2-5 | Restrictions: Follow one-file-per-class rule, use snake_case filenames, no breaking changes to existing models | _Leverage: models/requests/create_session_request.py | _Requirements: 2, 3, 4, 5 | Success: New request models validate inputs correctly and produce JSON schemas suitable for MCP tool registration | Instructions: Mark this task as in-progress in tasks.md, log implementation with log-implementation after completion, then mark as complete_

- [x] 2. Add session response models in models/responses/
  - File: models/responses/close_session_response.py
  - File: models/responses/save_case_response.py
  - File: models/responses/load_case_response.py
  - Define Pydantic response schemas for close/save/load session tools
  - Purpose: Standardize tool output formatting
  - _Leverage: models/responses/create_session_response.py_
  - _Requirements: 2, 3, 4, 5_
  - _Prompt: Implement the task for spec mcp-session-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in API response modeling | Task: Create response models for close_session, save_case, and load_case in models/responses with minimal, machine-readable fields that satisfy requirements 2-5 | Restrictions: Keep responses minimal, follow one-file-per-class rule, do not modify existing response models | _Leverage: models/responses/create_session_response.py | _Requirements: 2, 3, 4, 5 | Success: Response models serialize cleanly and align with MCP tool outputs | Instructions: Mark this task as in-progress in tasks.md, log implementation with log-implementation after completion, then mark as complete_

- [x] 3. Add case storage settings and path validation utility
  - File: mcp_service/server/dwsim_mcp_server/config/server_settings.py
  - File: mcp_service/server/dwsim_mcp_server/utils/path_validator.py
  - File: mcp_service/server/dwsim_mcp_server/utils/__init__.py
  - Introduce allowed case storage roots and a validator for save/load paths
  - Purpose: Enforce filesystem allowlist for persistence tools
  - _Leverage: mcp_service/server/dwsim_mcp_server/config/resource_limit_settings.py_
  - _Requirements: 3, 4, 5_
  - _Prompt: Implement the task for spec mcp-session-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer focused on security and configuration | Task: Add server settings for allowed case storage roots and implement a path validation utility that rejects traversal and non-allowed paths, meeting requirements 3-5 | Restrictions: Keep validation logic isolated, avoid OS-specific assumptions, do not introduce network access | _Leverage: mcp_service/server/dwsim_mcp_server/config/resource_limit_settings.py | _Requirements: 3, 4, 5 | Success: Settings expose allowed roots, validator returns normalized safe paths and raises clear errors on invalid input | Instructions: Mark this task as in-progress in tasks.md, log implementation with log-implementation after completion, then mark as complete_

- [x] 4. Extend session interop client with save/load operations
  - File: mcp_service/server/dwsim_mcp_server/ipc/session_client.py
  - File: mcp_service/server/dwsim_mcp_server/ipc/limited_session_client.py
  - Add save_case and load_case methods with error mapping and limit enforcement
  - Purpose: Provide interop operations for persistence tools
  - _Leverage: mcp_service/server/dwsim_mcp_server/ipc/exceptions.py_
  - _Requirements: 3, 4, 5_
  - _Prompt: Implement the task for spec mcp-session-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Interop Engineer | Task: Add save_case and load_case methods to SessionClient and LimitedSessionClient with proper exception mapping and limit handling aligned to requirements 3-5 | Restrictions: Follow existing error mapping patterns, do not bypass resource guards, keep method signatures consistent | _Leverage: mcp_service/server/dwsim_mcp_server/ipc/exceptions.py | _Requirements: 3, 4, 5 | Success: Interop client exposes save/load operations with deterministic error handling | Instructions: Mark this task as in-progress in tasks.md, log implementation with log-implementation after completion, then mark as complete_

- [x] 5. Implement MCP session tools and register them
  - File: mcp_service/server/dwsim_mcp_server/tools/session.py
  - File: mcp_service/server/dwsim_mcp_server/tools/registry.py
  - Define MCP tool handlers for create_session, close_session, save_case, load_case
  - Purpose: Expose session lifecycle tools to MCP clients
  - _Leverage: mcp_service/server/dwsim_mcp_server/server.py, mcp_service/server/dwsim_mcp_server/observability/logging.py_
  - _Requirements: 1, 2, 3, 4, 5_
  - _Prompt: Implement the task for spec mcp-session-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python MCP Tool Developer | Task: Create session tool handlers and register them with the MCP server using existing dependency injection, satisfying requirements 1-5 | Restrictions: Use Pydantic models for validation, return minimal outputs, log structured events, do not add global state | _Leverage: mcp_service/server/dwsim_mcp_server/server.py, mcp_service/server/dwsim_mcp_server/observability/logging.py | _Requirements: 1, 2, 3, 4, 5 | Success: Tools are registered with clear descriptions and can be invoked through MCP with validated inputs and structured responses | Instructions: Mark this task as in-progress in tasks.md, log implementation with log-implementation after completion, then mark as complete_

- [x] 6. Add unit tests for session tools and path validation
  - File: mcp_service/server/tests/unit/test_session_tools.py
  - File: mcp_service/server/tests/unit/test_path_validator.py
  - Create tests covering validation, success paths, and error mapping
  - Purpose: Ensure tool correctness and prevent regressions
  - _Leverage: mcp_service/server/tests/conftest.py_
  - _Requirements: 1, 2, 3, 4, 5_
  - _Prompt: Implement the task for spec mcp-session-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer specializing in pytest | Task: Add unit tests for session tool handlers and path validation covering success, invalid path, and session-not-found scenarios per requirements 1-5 | Restrictions: Use mocks for interop client, avoid real DWSIM dependencies, keep tests isolated | _Leverage: mcp_service/server/tests/conftest.py | _Requirements: 1, 2, 3, 4, 5 | Success: Tests pass consistently and cover core behaviors for session tools and path validation | Instructions: Mark this task as in-progress in tasks.md, log implementation with log-implementation after completion, then mark as complete_
