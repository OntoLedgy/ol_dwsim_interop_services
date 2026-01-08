# Tasks Document

- [x] 1. Add resource limit settings model
  - File: mcp_service/server/dwsim_mcp_server/config/resource_limit_settings.py
  - Create a Pydantic BaseSettings class for timeout and memory limits
  - Validate ranges (positive timeouts, memory limit > 0, recovery ratio 0.5-1.0)
  - Purpose: Centralize limit configuration and defaults
  - _Leverage: mcp_service/server/README.md (env var names), models/requests/create_session_request.py (timeout bounds)_
  - _Requirements: 4.1, 4.2, 4.3_
  - _Prompt: Implement the task for spec python-resource-limits, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in configuration and settings | Task: Create ResourceLimitSettings in mcp_service/server/dwsim_mcp_server/config/resource_limit_settings.py using Pydantic BaseSettings with environment variable support, defaults, and validation for timeouts and memory limits | Restrictions: Follow one-class-per-file rule, keep configuration fields aligned with README env vars, validate bounds explicitly, do not introduce non-ASCII | _Leverage: mcp_service/server/README.md, models/requests/create_session_request.py | _Requirements: 4.1, 4.2, 4.3 | Success: Settings load from env vars with defaults, invalid values raise clear errors, file follows project structure. Before coding, mark this task as in-progress in tasks.md. After completion, use log-implementation tool with detailed artifacts, then mark the task as complete in tasks.md.

- [x] 2. Add structured error model for limit violations
  - File: models/errors/resource_limit_error.py
  - Update: models/errors/__init__.py
  - Define ResourceLimitError with code, message, session_id, and details
  - Purpose: Provide consistent error payloads for limit violations
  - _Leverage: models/errors/session_error.py_
  - _Requirements: 5.2_
  - _Prompt: Implement the task for spec python-resource-limits, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer focusing on data models | Task: Create ResourceLimitError model and update models/errors/__init__.py to export it, following patterns in models/errors/session_error.py | Restrictions: One class per file, keep fields JSON-serializable, do not change existing error models | _Leverage: models/errors/session_error.py | _Requirements: 5.2 | Success: ResourceLimitError model compiles, includes code/message/session_id/details, exported via __init__.py. Before coding, mark this task as in-progress in tasks.md. After completion, use log-implementation tool with detailed artifacts, then mark the task as complete in tasks.md.

- [x] 3. Implement session lifetime tracking
  - File: mcp_service/server/dwsim_mcp_server/limits/session_lifetime_tracker.py
  - Track session start times and configured lifetimes
  - Provide register, is_expired, remaining, and remove operations
  - Purpose: Enforce session lifetime limits
  - _Leverage: models/requests/create_session_request.py_
  - _Requirements: 2.1, 2.2, 2.3_
  - _Prompt: Implement the task for spec python-resource-limits, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Backend Developer | Task: Implement SessionLifetimeTracker with register/is_expired/remaining/remove methods using monotonic time and internal storage, enforcing per-session timeouts | Restrictions: One class per file, thread-safe access to internal state, avoid external dependencies | _Leverage: models/requests/create_session_request.py | _Requirements: 2.1, 2.2, 2.3 | Success: Tracker correctly identifies expired sessions and cleans up state. Before coding, mark this task as in-progress in tasks.md. After completion, use log-implementation tool with detailed artifacts, then mark the task as complete in tasks.md.

- [x] 4. Implement per-operation timeout runner
  - File: mcp_service/server/dwsim_mcp_server/limits/operation_timeout_runner.py
  - Wrap blocking pythonnet calls with asyncio.wait_for and asyncio.to_thread
  - Provide async run_with_timeout helper with structured timeout error
  - Purpose: Enforce per-operation timeouts
  - _Leverage: dwsim_mcp_server/ipc/session_client.py_
  - _Requirements: 1.1, 1.2, 1.3_
  - _Prompt: Implement the task for spec python-resource-limits, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with async expertise | Task: Create OperationTimeoutRunner to execute blocking pythonnet operations via asyncio.to_thread and enforce timeouts with asyncio.wait_for, returning results or raising a timeout error | Restrictions: Avoid global state, do not block the event loop, keep API minimal and typed | _Leverage: dwsim_mcp_server/ipc/session_client.py | _Requirements: 1.1, 1.2, 1.3 | Success: Operations complete when within limit and raise clear timeout errors when exceeded. Before coding, mark this task as in-progress in tasks.md. After completion, use log-implementation tool with detailed artifacts, then mark the task as complete in tasks.md.

- [x] 5. Implement memory monitor
  - File: mcp_service/server/dwsim_mcp_server/limits/memory_monitor.py
  - Poll process RSS via psutil at configurable interval
  - Maintain breach state with recovery threshold
  - Purpose: Enforce memory usage limits
  - _Leverage: ResourceLimitSettings from task 1_
  - _Requirements: 3.1, 3.2, 3.3_
  - _Prompt: Implement the task for spec python-resource-limits, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer focusing on systems monitoring | Task: Implement MemoryMonitor that polls process RSS using psutil, tracks breach state, and exposes snapshot/is_exceeded APIs | Restrictions: Do not spawn unmanaged threads; use asyncio tasks and ensure clean shutdown | _Leverage: dwsim_mcp_server/config/resource_limit_settings.py | _Requirements: 3.1, 3.2, 3.3 | Success: Monitor detects breaches, respects recovery threshold, and can be started/stopped cleanly. Before coding, mark this task as in-progress in tasks.md. After completion, use log-implementation tool with detailed artifacts, then mark the task as complete in tasks.md.

- [x] 6. Implement resource limit guard and integrate with session client
  - File: mcp_service/server/dwsim_mcp_server/limits/resource_limit_guard.py
  - File: mcp_service/server/dwsim_mcp_server/ipc/limited_session_client.py
  - Wrap SessionClient operations with memory checks, session expiry, and timeouts
  - Purpose: Central enforcement point for limits before pythonnet calls
  - _Leverage: dwsim_mcp_server/ipc/session_client.py, dwsim_mcp_server/ipc/exceptions.py_
  - _Requirements: 1.1, 2.2, 3.2, 5.1, 5.2_
  - _Prompt: Implement the task for spec python-resource-limits, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Backend Developer | Task: Create ResourceLimitGuard to orchestrate memory checks, session lifetime validation, and timeout execution, then add LimitedSessionClient that delegates to SessionClient with limit enforcement | Restrictions: One class per file, keep sync/async boundaries explicit, integrate structured logging on breaches | _Leverage: dwsim_mcp_server/ipc/session_client.py, dwsim_mcp_server/ipc/exceptions.py | _Requirements: 1.1, 2.2, 3.2, 5.1, 5.2 | Success: LimitedSessionClient enforces limits consistently and returns clear errors with logs on breach. Before coding, mark this task as in-progress in tasks.md. After completion, use log-implementation tool with detailed artifacts, then mark the task as complete in tasks.md.

- [x] 7. Add psutil dependency and document limit configuration
  - File: mcp_service/server/requirements.txt
  - File: mcp_service/server/pyproject.toml
  - File: mcp_service/server/README.md
  - Add psutil dependency and document new limit-related environment variables
  - Purpose: Ensure memory monitoring dependency and configuration are documented
  - _Leverage: existing README configuration section_
  - _Requirements: 3.1, 4.1, 5.1_
  - _Prompt: Implement the task for spec python-resource-limits, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Packaging Engineer | Task: Add psutil to requirements.txt and pyproject.toml (if used), and update README to document memory limit and polling configuration env vars | Restrictions: Do not remove existing dependencies or settings, keep documentation concise and consistent with README style | _Leverage: mcp_service/server/README.md | _Requirements: 3.1, 4.1, 5.1 | Success: psutil dependency added where appropriate and README documents all limit-related env vars. Before coding, mark this task as in-progress in tasks.md. After completion, use log-implementation tool with detailed artifacts, then mark the task as complete in tasks.md.

- [x] 8. Add unit tests for limit enforcement
  - File: mcp_service/server/tests/unit/test_resource_limits.py
  - Test timeout runner, session lifetime tracker, and memory monitor breach logic
  - Purpose: Validate limit enforcement behavior
  - _Leverage: mcp_service/server/tests/conftest.py_
  - _Requirements: 1.1, 2.1, 3.1_
  - _Prompt: Implement the task for spec python-resource-limits, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer specializing in pytest | Task: Create unit tests for timeout, session expiry, and memory monitoring with mocks for time and psutil, ensuring deterministic behavior | Restrictions: Tests must be isolated and not require DWSIM assemblies, avoid flaky timing-dependent assertions | _Leverage: mcp_service/server/tests/conftest.py | _Requirements: 1.1, 2.1, 3.1 | Success: Tests pass reliably and cover key limit scenarios without external dependencies. Before coding, mark this task as in-progress in tasks.md. After completion, use log-implementation tool with detailed artifacts, then mark the task as complete in tasks.md.
