# Tasks Document

- [x] 1. Add pythonnet bridge exceptions module
  - File: mcp_service/server/dwsim_mcp_server/ipc/exceptions.py
  - Define interop-specific Python exception types and mapping helpers
  - Purpose: Provide consistent error handling and translation from .NET exceptions
  - _Leverage: mcp_service/server/tests/smoke/test_pythonnet_loading.py_
  - _Requirements: 3.1, 3.2_
  - _Prompt: Implement the task for spec pythonnet-bridge, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Backend Developer specializing in interop error handling | Task: Create interop exception classes and mapping helpers in mcp_service/server/dwsim_mcp_server/ipc/exceptions.py that normalize .NET exceptions into Python errors with inner-exception summaries | Restrictions: Do not add new heavy dependencies, keep logic self-contained, follow project naming conventions | _Leverage: mcp_service/server/tests/smoke/test_pythonnet_loading.py | _Requirements: 3.1, 3.2 | Success: Exception types exist, mapping preserves message context, unit tests or smoke coverage can consume helpers; update tasks.md status to in-progress then complete and log implementation with log-implementation tool_

- [x] 2. Implement CLR loader for DwsimWorker.dll
  - File: mcp_service/server/dwsim_mcp_server/ipc/clr_loader.py
  - Resolve DwsimWorker.dll path and load pythonnet/CLR
  - Purpose: Centralized assembly loading with clear errors
  - _Leverage: mcp_service/server/tests/conftest.py_
  - _Requirements: 1.1, 1.2, 1.3_
  - _Prompt: Implement the task for spec pythonnet-bridge, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with pythonnet experience | Task: Implement clr_loader.py to resolve the DwsimWorker.dll path (using config + defaults) and load pythonnet/CLR with informative errors for missing pythonnet or DLL | Restrictions: Do not hardcode absolute paths, keep loader reusable, follow single-responsibility | _Leverage: mcp_service/server/tests/conftest.py | _Requirements: 1.1, 1.2, 1.3 | Success: Loader returns access to DwsimWorker types, throws clear errors when dependencies missing; update tasks.md status to in-progress then complete and log implementation with log-implementation tool_

- [x] 3. Create session client wrapper around SessionManager
  - File: mcp_service/server/dwsim_mcp_server/ipc/session_client.py
  - Wrap SessionManager create/close operations with Python-friendly API
  - Purpose: Provide a thin, typed session interface for Python services
  - _Leverage: mcp_service/server/dwsim_mcp_server/ipc/clr_loader.py, mcp_service/server/dwsim_mcp_server/ipc/exceptions.py_
  - _Requirements: 2.1, 2.2, 2.3_
  - _Prompt: Implement the task for spec pythonnet-bridge, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Backend Developer with C# interop experience | Task: Implement session_client.py that instantiates SessionManager via clr_loader and exposes create_session/close_session with error mapping | Restrictions: Keep wrapper minimal, ensure IDisposable disposal, avoid MCP tool logic | _Leverage: mcp_service/server/dwsim_mcp_server/ipc/clr_loader.py; mcp_service/server/dwsim_mcp_server/ipc/exceptions.py | _Requirements: 2.1, 2.2, 2.3 | Success: Session client can create/close sessions; errors are mapped consistently; update tasks.md status to in-progress then complete and log implementation with log-implementation tool_

- [x] 4. Add pythonnet bridge integration test
  - File: mcp_service/server/tests/smoke/test_pythonnet_loading.py
  - Use the new session client to create/close a session
  - Purpose: Validate bridge behavior with real DwsimWorker.dll
  - _Leverage: mcp_service/server/tests/conftest.py_
  - _Requirements: 5.1, 5.2, 5.3_
  - _Prompt: Implement the task for spec pythonnet-bridge, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer specializing in pytest and interop tests | Task: Extend smoke test to use session_client wrapper and assert session creation/closure works | Restrictions: Keep test deterministic, skip when pythonnet or DLL missing, avoid long-running simulation work | _Leverage: mcp_service/server/tests/conftest.py | _Requirements: 5.1, 5.2, 5.3 | Success: pytest smoke test passes on Windows with built DwsimWorker.dll; update tasks.md status to in-progress then complete and log implementation with log-implementation tool_
