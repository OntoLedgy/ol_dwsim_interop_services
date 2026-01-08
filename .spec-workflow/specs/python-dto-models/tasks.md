# Tasks Document

- [x] 1. Add CAPE-OPEN domain models for property packages and unit operations
  - File: models/cape_open/thermo_property_package.py
  - File: models/cape_open/unit_operation.py
  - File: models/cape_open/material_stream.py (update if needed for consistent validation/examples)
  - Define Pydantic models with field descriptions, examples, and validators for physical constraints
  - Purpose: Establish core CAPE-OPEN data structures for validation and serialization
  - _Leverage: models/cape_open/material_stream.py_
  - _Requirements: 1_
  - _Prompt: Implement the task for spec python-dto-models, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in Pydantic modeling | Task: Create CAPE-OPEN models for property packages and unit operations, and align the existing MaterialStream model for consistent validation and schema metadata | Restrictions: One file per class, no pythonnet dependencies in models, keep field names CAPE-OPEN-aligned | _Leverage: models/cape_open/material_stream.py | _Requirements: 1 | Success: New models validate required fields, include schema examples, and enforce basic physical constraints |

- [x] 2. Add session request model for close_session
  - File: models/requests/close_session_request.py
  - File: models/requests/__init__.py (update exports)
  - Define Pydantic request model with session_id validation and schema metadata
  - Purpose: Validate session lifecycle tool inputs
  - _Leverage: models/requests/create_session_request.py_
  - _Requirements: 2, 4_
  - _Prompt: Implement the task for spec python-dto-models, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in API request validation | Task: Add a CloseSessionRequest model with strict session_id validation and schema metadata, and export it in the requests package | Restrictions: One file per class, follow existing request patterns, keep validation deterministic | _Leverage: models/requests/create_session_request.py | _Requirements: 2, 4 | Success: close_session inputs validate correctly and schema metadata is present |

- [x] 3. Add flowsheet request models for add_stream, add_unit, and connect
  - File: models/requests/add_stream_request.py
  - File: models/requests/add_unit_request.py
  - File: models/requests/connect_request.py
  - File: models/requests/__init__.py (update exports)
  - Include type hints, defaults, and validators (e.g., positive pressure)
  - Purpose: Validate flowsheet-building tool inputs before interop
  - _Leverage: models/requests/create_session_request.py, models/cape_open/material_stream.py_
  - _Requirements: 2, 4_
  - _Prompt: Implement the task for spec python-dto-models, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in validation and schemas | Task: Create request DTOs for add_stream, add_unit, and connect with proper validation and schema metadata, and export them in the requests package | Restrictions: One file per class, follow field naming conventions, keep validators simple and explicit | _Leverage: models/requests/create_session_request.py, models/cape_open/material_stream.py | _Requirements: 2, 4 | Success: Requests validate inputs and document defaults/constraints clearly |

- [x] 4. Add simulation request models for run and get_results
  - File: models/requests/run_simulation_request.py
  - File: models/requests/get_results_request.py
  - File: models/requests/__init__.py (update exports)
  - Include optional object_id for targeted results and schema examples
  - Purpose: Validate simulation execution tool inputs
  - _Leverage: models/requests/create_session_request.py_
  - _Requirements: 2, 4_
  - _Prompt: Implement the task for spec python-dto-models, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in MCP tool schemas | Task: Create request DTOs for run_simulation and get_results with strong validation and examples, and export them in the requests package | Restrictions: One file per class, keep fields minimal and consistent with tool plans, do not add tool logic | _Leverage: models/requests/create_session_request.py | _Requirements: 2, 4 | Success: Simulation request models validate and document the MCP inputs cleanly |

- [x] 5. Implement Python-to-C# DTO conversion helpers
  - File: mcp_service/server/dwsim_mcp_server/converters/pythonnet_dto_converter.py
  - File: mcp_service/server/dwsim_mcp_server/converters/__init__.py (update exports)
  - Add conversion functions for CAPE-OPEN models and request DTOs to/from pythonnet C# types
  - Purpose: Provide a single, explicit mapping layer for interop
  - _Leverage: models/cape_open/*.py, models/requests/*.py_
  - _Requirements: 3, 4_
  - _Prompt: Implement the task for spec python-dto-models, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with pythonnet interop experience | Task: Create conversion helpers that map Pydantic models and dicts to C# DTOs and back, with clear errors for missing mappings | Restrictions: Keep conversion logic isolated from models, avoid circular imports, use explicit field mapping | _Leverage: models/cape_open/*.py, models/requests/*.py | _Requirements: 3, 4 | Success: Converters handle supported DTOs bidirectionally and raise actionable errors on unsupported fields |

- [x] 6. Add unit tests for models and converters
  - File: mcp_service/server/tests/unit/models/test_cape_open_models.py
  - File: mcp_service/server/tests/unit/models/test_request_models.py
  - File: mcp_service/server/tests/unit/converters/test_pythonnet_dto_converter.py
  - Cover validation constraints, defaults, and conversion behavior
  - Purpose: Ensure model validation and conversion reliability
  - _Leverage: mcp_service/server/tests/unit/test_resource_limits.py_
  - _Requirements: 1, 2, 3, 4_
  - _Prompt: Implement the task for spec python-dto-models, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer specializing in Python unit tests | Task: Write unit tests for CAPE-OPEN models, request DTOs, and converters covering validation, defaults, and conversion edge cases | Restrictions: Keep tests isolated, no external dependencies, follow existing pytest patterns | _Leverage: mcp_service/server/tests/unit/test_resource_limits.py | _Requirements: 1, 2, 3, 4 | Success: Tests cover positive and negative cases and pass consistently |
