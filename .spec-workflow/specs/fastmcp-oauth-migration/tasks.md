# Tasks Document: FastMCP OAuth Migration

## Phase 1: Auth Module

- [x] 1.1. Create AuthConfig settings class
  - File: `mcp_service/server/dwsim_mcp_server/auth/settings.py`
  - Create Pydantic settings class for Clerk OAuth configuration
  - Support environment variables: DWSIM_AUTH_ENABLED, CLERK_ISSUER_URL, CLERK_JWKS_URL, CLERK_AUDIENCE, CLERK_REQUIRED_SCOPES
  - Add effective_jwks_url property for default JWKS URL derivation
  - _Leverage: `mcp_service/server/dwsim_mcp_server/config/server_settings.py` for pattern reference_
  - _Requirements: REQ-5_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in Pydantic settings and configuration management | Task: Create AuthConfig Pydantic settings class in auth/settings.py following existing ServerSettings patterns, supporting Clerk OAuth environment variables (DWSIM_AUTH_ENABLED, CLERK_ISSUER_URL, CLERK_JWKS_URL, CLERK_AUDIENCE, CLERK_REQUIRED_SCOPES) with proper validation and defaults | Restrictions: Follow existing pydantic-settings patterns, use SettingsConfigDict with env_prefix, do not hardcode any secrets | Success: AuthConfig loads from environment variables correctly, effective_jwks_url property works, all fields have appropriate defaults and validation | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [x] 1.2. Create ClerkTokenVerifier class
  - File: `mcp_service/server/dwsim_mcp_server/auth/clerk_verifier.py`
  - Implement TokenVerifier interface from mcp.server.auth.provider
  - Add JWKS fetching with PyJWKClient and 1-hour caching
  - Implement JWT verification with RS256, expiration, and scope checking
  - Add structured logging for verification events
  - _Leverage: `mcp_service/server/dwsim_mcp_server/observability/logging.py` for logging patterns_
  - _Requirements: REQ-2_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Security Engineer specializing in OAuth/JWT and Python async programming | Task: Create ClerkTokenVerifier implementing MCP SDK TokenVerifier interface with JWKS-based JWT verification, 1-hour key caching via PyJWKClient, RS256 algorithm enforcement, expiration validation, and scope checking | Restrictions: Must use pyjwt library, cache JWKS keys properly, log all verification events without exposing tokens, handle all JWT exceptions gracefully | Success: Valid tokens return AccessToken, expired/invalid tokens return None, JWKS caching works, all error paths logged appropriately | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [x] 1.3. Create auth module __init__.py
  - File: `mcp_service/server/dwsim_mcp_server/auth/__init__.py`
  - Export AuthConfig and ClerkTokenVerifier
  - _Leverage: Existing __init__.py patterns in the project_
  - _Requirements: REQ-2, REQ-5_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Create auth module __init__.py exporting AuthConfig and ClerkTokenVerifier for clean imports | Restrictions: Follow existing __init__.py patterns, export only public interfaces | Success: `from dwsim_mcp_server.auth import AuthConfig, ClerkTokenVerifier` works correctly | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [x] 1.4. Add pyjwt dependency to pyproject.toml
  - File: `mcp_service/server/pyproject.toml`
  - Add pyjwt[crypto]>=2.8.0 to dependencies
  - _Leverage: Existing pyproject.toml structure_
  - _Requirements: REQ-2_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Add pyjwt[crypto]>=2.8.0 to pyproject.toml dependencies section | Restrictions: Do not modify other dependencies, maintain toml formatting | Success: pyjwt is listed in dependencies, uv sync or pip install works | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [x] 1.5. Create unit tests for auth module
  - File: `mcp_service/server/tests/unit/auth/test_clerk_verifier.py`
  - Test valid token verification with mocked JWKS
  - Test expired token rejection
  - Test invalid signature rejection
  - Test missing scopes rejection
  - Test JWKS caching behavior
  - _Leverage: `mcp_service/server/tests/` for test patterns, pytest fixtures_
  - _Requirements: REQ-2_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer specializing in Python testing and JWT security | Task: Create comprehensive unit tests for ClerkTokenVerifier covering valid token verification, expired token rejection, invalid signature handling, missing scopes, and JWKS caching with mocked dependencies | Restrictions: Use pytest, mock all external dependencies (JWKS fetch, jwt.decode), do not make real HTTP requests, maintain test isolation | Success: All tests pass, coverage includes success and failure paths, edge cases covered | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

## Phase 2: Context and Lifespan

- [x] 2.1. Create AppContext dataclass
  - File: `mcp_service/server/dwsim_mcp_server/context.py`
  - Define AppContext dataclass with typed dependencies (settings, session_client, flowsheet_client, services)
  - Create app_lifespan async context manager for FastMCP
  - Initialize all dependencies on startup, dispose on shutdown
  - _Leverage: `mcp_service/server/dwsim_mcp_server/server.py` current ServerDependencies pattern_
  - _Requirements: REQ-1_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in async context managers and dependency injection | Task: Create AppContext dataclass containing all server dependencies (ServerSettings, LimitedSessionClient, FlowsheetClient, FlowsheetService, ThermodynamicsService, SensitivityService, DiagnosticsService) and app_lifespan async context manager that initializes dependencies on startup and disposes on shutdown | Restrictions: Follow existing ServerDependencies initialization logic, use @asynccontextmanager decorator, ensure proper cleanup in finally block | Success: AppContext contains all typed dependencies, lifespan manages lifecycle correctly, logging shows startup/shutdown events | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

## Phase 3: Tool Conversion

- [ ] 3.1. Convert session tools to FastMCP decorators
  - File: `mcp_service/server/dwsim_mcp_server/tools/session.py`
  - Convert 4 tools: create_session, close_session, save_case, load_case
  - Replace build_session_tools/handle_session_tool with register_session_tools(mcp)
  - Use @mcp.tool() decorator with description
  - Access dependencies via ctx.request_context.lifespan_context
  - _Leverage: Existing tool descriptions and logic in session.py_
  - _Requirements: REQ-1, REQ-4_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in FastMCP and async programming | Task: Convert session.py tools (create_session, close_session, save_case, load_case) from build_session_tools/handle_session_tool pattern to register_session_tools(mcp) with @mcp.tool() decorators, accessing dependencies via ctx.request_context.lifespan_context | Restrictions: Preserve existing tool descriptions exactly, maintain parameter names and return types, keep all validation logic | Success: 4 tools registered with FastMCP, identical behavior to current implementation, proper type hints | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [ ] 3.2. Convert flowsheet tools to FastMCP decorators
  - File: `mcp_service/server/dwsim_mcp_server/tools/flowsheet.py`
  - Convert 10 tools: add_compound, set_property_package, add_stream, add_unit, connect, set_object_parameter, delete_object, list_objects, flash_stream, set_binary_interaction_parameter
  - Replace build_flowsheet_tools/handle_flowsheet_tool with register_flowsheet_tools(mcp)
  - _Leverage: Existing tool descriptions and logic in flowsheet.py_
  - _Requirements: REQ-1, REQ-4_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in FastMCP | Task: Convert flowsheet.py tools (10 tools including add_compound, set_property_package, add_stream, add_unit, connect, etc.) from current pattern to register_flowsheet_tools(mcp) with @mcp.tool() decorators | Restrictions: Preserve all tool descriptions and parameter schemas exactly, maintain Pydantic model validation, keep existing error handling | Success: 10 tools registered with FastMCP, identical behavior and schemas | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [ ] 3.3. Convert simulation tools to FastMCP decorators
  - File: `mcp_service/server/dwsim_mcp_server/tools/simulation.py`
  - Convert 3 tools: run, get_status, get_results
  - Replace build_simulation_tools/handle_simulation_tool with register_simulation_tools(mcp)
  - _Leverage: Existing tool descriptions and logic in simulation.py_
  - _Requirements: REQ-1, REQ-4_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Convert simulation.py tools (run, get_status, get_results) to register_simulation_tools(mcp) with @mcp.tool() decorators | Restrictions: Preserve tool descriptions and behavior exactly | Success: 3 tools registered with FastMCP, identical behavior | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [ ] 3.4. Convert compound tools to FastMCP decorators
  - File: `mcp_service/server/dwsim_mcp_server/tools/compound.py`
  - Convert 2 tools: validate_compounds, list_available_compounds
  - Replace build_compound_tools/handle_compound_tool with register_compound_tools(mcp)
  - _Leverage: Existing tool descriptions and logic in compound.py_
  - _Requirements: REQ-1, REQ-4_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Convert compound.py tools (validate_compounds, list_available_compounds) to register_compound_tools(mcp) with @mcp.tool() decorators | Restrictions: Preserve tool descriptions and behavior exactly | Success: 2 tools registered with FastMCP, identical behavior | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [ ] 3.5. Convert analysis tools to FastMCP decorators
  - File: `mcp_service/server/dwsim_mcp_server/tools/analysis.py`
  - Convert 3 tools: flash_tp, flash_ph, flash_ps
  - Replace build_analysis_tools/handle_analysis_tool with register_analysis_tools(mcp)
  - _Leverage: Existing tool descriptions and logic in analysis.py_
  - _Requirements: REQ-1, REQ-4_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Convert analysis.py tools (flash_tp, flash_ph, flash_ps) to register_analysis_tools(mcp) with @mcp.tool() decorators | Restrictions: Preserve tool descriptions and Pydantic input models exactly | Success: 3 tools registered with FastMCP, identical behavior | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [ ] 3.6. Convert sensitivity tools to FastMCP decorators
  - File: `mcp_service/server/dwsim_mcp_server/tools/sensitivity.py`
  - Convert 5 tools: sensitivity_analysis, parameter_sweep, optimize, get_study_status, cancel_study, export_study_results
  - Replace build_sensitivity_tools/handle_sensitivity_tool with register_sensitivity_tools(mcp)
  - _Leverage: Existing tool descriptions and logic in sensitivity.py_
  - _Requirements: REQ-1, REQ-4_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Convert sensitivity.py tools (sensitivity_analysis, parameter_sweep, optimize, get_study_status, cancel_study, export_study_results) to register_sensitivity_tools(mcp) with @mcp.tool() decorators | Restrictions: Preserve tool descriptions and Pydantic input models exactly | Success: All sensitivity tools registered with FastMCP, identical behavior | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [ ] 3.7. Convert export tools to FastMCP decorators
  - File: `mcp_service/server/dwsim_mcp_server/tools/export.py`
  - Convert 3 tools: export_csv, export_json, generate_report
  - Replace build_export_tools/handle_export_tool with register_export_tools(mcp)
  - _Leverage: Existing tool descriptions and logic in export.py_
  - _Requirements: REQ-1, REQ-4_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Convert export.py tools (export_csv, export_json, generate_report) to register_export_tools(mcp) with @mcp.tool() decorators | Restrictions: Preserve tool descriptions and behavior exactly | Success: 3 tools registered with FastMCP, identical behavior | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [ ] 3.8. Convert diagnostics tools to FastMCP decorators
  - File: `mcp_service/server/dwsim_mcp_server/tools/diagnostics.py`
  - Convert diagnostics tools: get_diagnostics (and any others)
  - Replace build_diagnostics_tools/handle_diagnostics_tool with register_diagnostics_tools(mcp)
  - _Leverage: Existing tool descriptions and logic in diagnostics.py_
  - _Requirements: REQ-1, REQ-4_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Convert diagnostics.py tools to register_diagnostics_tools(mcp) with @mcp.tool() decorators | Restrictions: Preserve tool descriptions and behavior exactly | Success: All diagnostics tools registered with FastMCP, identical behavior | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

## Phase 4: Server Bootstrap

- [ ] 4.1. Add FastMCP dependency to pyproject.toml
  - File: `mcp_service/server/pyproject.toml`
  - Add fastmcp>=2.0.0 to dependencies
  - _Requirements: REQ-1_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Add fastmcp>=2.0.0 to pyproject.toml dependencies section | Restrictions: Do not modify other dependencies, maintain toml formatting | Success: fastmcp is listed in dependencies, uv sync or pip install works | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [ ] 4.2. Update server.py to use FastMCP
  - File: `mcp_service/server/dwsim_mcp_server/server.py`
  - Replace Server + StreamableHTTPSessionManager with FastMCP
  - Create create_mcp_server() function that:
    - Loads AuthConfig and conditionally enables OAuth
    - Creates FastMCP with app_lifespan context manager
    - Registers all tools via register_*_tools functions
    - Configures auth settings if enabled
  - Update run() function for FastMCP transport
  - _Leverage: Existing server.py structure, new context.py and auth module_
  - _Requirements: REQ-1, REQ-2, REQ-3, REQ-4_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in FastMCP server architecture | Task: Replace server.py implementation with FastMCP-based create_mcp_server() that loads AuthConfig, conditionally enables Clerk OAuth, uses app_lifespan for dependency injection, registers all tools via register_*_tools functions, and configures appropriate transport (streamable-http or stdio) | Restrictions: Maintain backward compatibility for stdio mode, preserve existing observability setup, keep DWSIM_AUTH_ENABLED=false as default | Success: Server starts with FastMCP, OAuth works when enabled, stdio mode works, all 33 tools registered | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [ ] 4.3. Update tools registry.py
  - File: `mcp_service/server/dwsim_mcp_server/tools/registry.py`
  - Remove old register_tools function or keep as legacy
  - Add register_all_tools(mcp) that calls all register_*_tools functions
  - _Leverage: Existing registry.py structure_
  - _Requirements: REQ-1_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Update tools/registry.py to add register_all_tools(mcp) function that calls all register_*_tools functions, deprecate or remove old register_tools function | Restrictions: Ensure all 33 tools are registered, maintain import order | Success: register_all_tools(mcp) registers all tools correctly | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

## Phase 5: Testing and Documentation

- [ ] 5.1. Create OAuth integration tests
  - File: `mcp_service/server/tests/integration/test_oauth_flow.py`
  - Test OAuth discovery endpoint (/.well-known/oauth-protected-resource)
  - Test unauthenticated request rejection when auth enabled
  - Test authenticated request acceptance (with mocked token)
  - Test stdio mode bypass
  - _Leverage: Existing integration test patterns in tests/integration/_
  - _Requirements: REQ-2, REQ-3, REQ-4_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer specializing in integration testing and OAuth flows | Task: Create integration tests for OAuth flow covering discovery endpoint, unauthenticated rejection, authenticated acceptance with mocked tokens, and stdio bypass | Restrictions: Use pytest and httpx AsyncClient, mock token verification for predictable tests, do not make real Clerk API calls | Success: All OAuth scenarios tested, tests run in CI without external dependencies | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [ ] 5.2. Run existing integration tests
  - Verify all existing tests pass with new FastMCP server
  - Fix any regressions
  - _Leverage: Existing test suite in tests/_
  - _Requirements: REQ-4_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer | Task: Run all existing integration tests against new FastMCP server implementation, identify and fix any regressions | Restrictions: Do not modify test expectations unless behavior intentionally changed, document any intentional behavior changes | Success: All existing tests pass, no regressions | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [ ] 5.3. Update .env.example with OAuth settings
  - File: `mcp_service/server/.env.example` or template
  - Add DWSIM_AUTH_ENABLED, CLERK_ISSUER_URL, CLERK_AUDIENCE, CLERK_REQUIRED_SCOPES
  - Add comments explaining each setting
  - _Requirements: REQ-5_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: DevOps Engineer | Task: Update .env.example to include OAuth configuration variables (DWSIM_AUTH_ENABLED, CLERK_ISSUER_URL, CLERK_AUDIENCE, CLERK_REQUIRED_SCOPES) with helpful comments | Restrictions: Do not include real secrets, provide sensible defaults, maintain existing variable formatting | Success: .env.example documents all OAuth settings clearly | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [ ] 5.4. Create Clerk setup documentation
  - File: `docs/mcp/clerk-oauth-setup.md`
  - Document Clerk dashboard configuration steps
  - Document environment variable setup
  - Document mcp-remote client configuration
  - Include troubleshooting section
  - _Requirements: REQ-2, REQ-3, REQ-5_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Technical Writer | Task: Create comprehensive Clerk OAuth setup documentation covering Clerk dashboard configuration, environment variables, mcp-remote client config, and troubleshooting | Restrictions: Do not include real secrets or account-specific URLs, provide generic examples | Success: Documentation enables new users to set up OAuth end-to-end | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

- [ ] 5.5. Update README with authentication section
  - File: `mcp_service/server/README.md` or main README
  - Add section on authentication options
  - Link to Clerk setup documentation
  - Document local development without auth
  - _Requirements: REQ-4, REQ-5_
  - _Prompt: Implement the task for spec fastmcp-oauth-migration, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Technical Writer | Task: Update README with authentication section covering OAuth options, linking to Clerk setup docs, and explaining local development without auth | Restrictions: Keep existing README structure, add new section appropriately | Success: README clearly explains authentication options | After completing: Mark task as in-progress in tasks.md before starting, use log-implementation tool to record artifacts, then mark as complete when done_

## Summary

| Phase | Tasks | Tools Affected |
|-------|-------|----------------|
| 1. Auth Module | 5 tasks | - |
| 2. Context | 1 task | - |
| 3. Tool Conversion | 8 tasks | 33 tools |
| 4. Server Bootstrap | 3 tasks | - |
| 5. Testing & Docs | 5 tasks | - |
| **Total** | **22 tasks** | **33 tools** |
