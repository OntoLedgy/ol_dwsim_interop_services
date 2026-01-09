# Tasks Document

- [x] 1. Define MCP server settings model
  - File: mcp_service/server/dwsim_mcp_server/config/server_settings.py
  - Add Pydantic settings for log level, enable_pythonnet, worker_assembly_path, and resource limit settings
  - Purpose: Centralize configuration for MCP server bootstrap
  - _Leverage: mcp_service/server/dwsim_mcp_server/config/resource_limit_settings.py_
  - _Requirements: 2_
  - _Prompt: Implement the task for spec mcp-server-bootstrap, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in configuration management | Task: Create ServerSettings using Pydantic settings with environment overrides and defaults, following existing config patterns | Restrictions: Do not hardcode secrets, keep settings isolated, follow one-file-per-class | _Leverage: mcp_service/server/dwsim_mcp_server/config/resource_limit_settings.py | _Requirements: 2 | Success: Settings load correctly from env, defaults documented, validation errors are clear |

- [x] 2. Add logging configuration helper
  - File: mcp_service/server/dwsim_mcp_server/observability/logging.py
  - Configure structured logging setup for startup, tool calls, and shutdown
  - Purpose: Ensure consistent observability across MCP server
  - _Leverage: structlog patterns in existing codebase (if any)_
  - _Requirements: 3_
  - _Prompt: Implement the task for spec mcp-server-bootstrap, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in observability | Task: Build logging configuration helper that initializes structured logging with configurable log level and consistent fields | Restrictions: Avoid logging secrets, keep helper isolated, do not add tool logic | _Leverage: mcp_service/server/dwsim_mcp_server/observability | _Requirements: 3 | Success: Logging is configured once at startup and produces structured output |

- [x] 3. Implement MCP server bootstrap and lifecycle
  - File: mcp_service/server/dwsim_mcp_server/server.py
  - Initialize MCP server, load settings, wire dependencies, register tools/resources, handle graceful shutdown
  - Purpose: Provide the main entry point for the MCP server
  - _Leverage: mcp_service/server/dwsim_mcp_server/ipc/session_client.py, mcp_service/server/dwsim_mcp_server/tools/__init__.py_
  - _Requirements: 1, 3, 4_
  - _Prompt: Implement the task for spec mcp-server-bootstrap, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Backend Developer with MCP SDK experience | Task: Implement server bootstrap that loads settings, configures logging, instantiates dependencies, registers tools, and handles shutdown | Restrictions: Keep server.py focused on lifecycle only, no tool logic here, use MCP SDK stdio transport | _Leverage: dwsim_mcp_server/ipc/session_client.py, dwsim_mcp_server/tools/__init__.py | _Requirements: 1, 3, 4 | Success: Server starts, lists tools, and shuts down cleanly |

- [x] 4. Wire tool registration helper
  - File: mcp_service/server/dwsim_mcp_server/tools/registry.py
  - Register tool implementations with MCP server using injected dependencies
  - Purpose: Keep tool registration isolated from server bootstrap
  - _Leverage: existing tool modules as they are implemented_
  - _Requirements: 4_
  - _Prompt: Implement the task for spec mcp-server-bootstrap, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in modular tool registration | Task: Create a registry module that registers tools on the MCP server using injected dependencies | Restrictions: Do not implement tool logic here, keep registry focused on wiring | _Leverage: dwsim_mcp_server/tools | _Requirements: 4 | Success: Tools are registered from a single registry function |

- [x] 5. Add smoke tests for server bootstrap
  - File: mcp_service/server/tests/smoke/test_server_bootstrap.py
  - Verify settings load, logging configured, and server can initialize without errors
  - Purpose: Basic verification of server bootstrap
  - _Leverage: mcp_service/server/tests/smoke/test_pythonnet_loading.py_
  - _Requirements: 1, 2, 3_
  - _Prompt: Implement the task for spec mcp-server-bootstrap, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer specializing in service bootstrap tests | Task: Add smoke tests covering server bootstrap, settings load, and logging configuration | Restrictions: Avoid integration with external services, keep tests fast | _Leverage: mcp_service/server/tests/smoke/test_pythonnet_loading.py | _Requirements: 1, 2, 3 | Success: Smoke tests pass consistently and validate basic startup behavior |
