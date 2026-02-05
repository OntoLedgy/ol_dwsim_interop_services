# MCP Apps Backend - Tasks

## Phase 1: Infrastructure (P0)

- [ ] 1.1 Create UiResourceMetadata Pydantic models
  - File: `mcp_service/server/dwsim_mcp_server/models/resources/ui_resource_metadata.py`
  - Define `CspConfig`, `UiResourceMetadata`, and `AppConfig` models
  - Add validation for resource URIs and CSP domains
  - Purpose: Provide type-safe metadata models for UI resources
  - _Leverage: `mcp_service/server/dwsim_mcp_server/models/resources/resource_metadata.py`_
  - _Requirements: FR-1.3_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in Pydantic data models | Task: Create UiResourceMetadata Pydantic models (CspConfig, UiResourceMetadata, AppConfig) for UI resource metadata validation following FR-1.3, leveraging existing patterns from resource_metadata.py | Restrictions: Do not modify existing models, follow one-class-per-file convention for new complex types, maintain consistent Field descriptions | Success: Models compile without errors, validation works for valid/invalid URIs and CSP domains, models serialize to JSON correctly | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 1.2 Create UiResourceProvider class
  - File: `mcp_service/server/dwsim_mcp_server/resources/ui_resource_provider.py`
  - Implement `UiResourceProvider` extending `BaseResourceProvider`
  - Handle `ui://dwsim/*` URI pattern with parameterized support
  - Return HTML with `text/html;profile=mcp-app` MIME type
  - Purpose: Serve UI applications as MCP resources
  - _Leverage: `mcp_service/server/dwsim_mcp_server/resources/docs.py`, `mcp_service/server/dwsim_mcp_server/resources/base.py`_
  - _Requirements: FR-1.1, FR-1.2, FR-1.4_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Backend Developer with MCP protocol expertise | Task: Create UiResourceProvider class extending BaseResourceProvider to serve ui://dwsim/* resources following FR-1.1, FR-1.2, FR-1.4, using DocsProvider as a pattern | Restrictions: Must follow existing resource provider patterns, use async file operations with aiofiles, implement proper caching like DocsProvider | Success: Provider registers successfully, list_resources returns all apps, read_resource returns HTML with correct MIME type and CSP metadata | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 1.3 Register UiResourceProvider in server initialization
  - File: `mcp_service/server/dwsim_mcp_server/resources/__init__.py` (modify)
  - Add `UiResourceProvider` to resource provider list
  - Configure apps directory path from settings
  - Purpose: Enable UI resources in MCP server
  - _Leverage: `mcp_service/server/dwsim_mcp_server/resources/__init__.py`_
  - _Requirements: FR-1.1_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with MCP server configuration expertise | Task: Register UiResourceProvider in the resource provider initialization following FR-1.1, adding configuration for apps directory path | Restrictions: Do not break existing resource providers, follow existing registration pattern, use settings for path configuration | Success: UiResourceProvider is registered alongside existing providers, apps path is configurable, server starts without errors | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 1.4 Create tool UI metadata enhancement utilities
  - File: `mcp_service/server/dwsim_mcp_server/tools/ui_metadata.py`
  - Implement `add_ui_metadata()` and `get_ui_result_annotation()` functions
  - Support visibility configuration and CSP settings
  - Purpose: Simplify adding _meta.ui to tool definitions and results
  - _Leverage: `mcp_service/server/dwsim_mcp_server/tools/__init__.py`_
  - _Requirements: FR-2.1, FR-2.2_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with MCP tool enhancement expertise | Task: Create utility functions for adding _meta.ui to tool definitions and results following FR-2.1, FR-2.2, supporting visibility and CSP configuration | Restrictions: Keep functions pure and stateless, follow existing tool utility patterns, ensure backward compatibility with tools without UI | Success: Functions correctly add _meta.ui to tool definitions, result annotations include proper resource URIs, visibility options work correctly | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 1.5 Create apps directory structure with base template
  - Files:
    - `mcp_service/server/dwsim_mcp_server/apps/__init__.py`
    - `mcp_service/server/dwsim_mcp_server/apps/templates/base.html`
    - `mcp_service/server/dwsim_mcp_server/apps/static/app-client.js`
    - `mcp_service/server/dwsim_mcp_server/apps/static/theme.css`
  - Create base HTML template with MCP Apps SDK loaded from CDN
  - Create shared JS wrapper for MCP Apps client
  - Create shared CSS for theming support
  - Purpose: Provide foundation for all app templates
  - _Leverage: None (new directory)_
  - _Requirements: FR-3.2, FR-3.3_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Frontend Developer with HTML/JS and MCP Apps SDK expertise | Task: Create apps directory structure with base HTML template loading MCP Apps SDK from CDN, shared JS client wrapper, and theme CSS following FR-3.2, FR-3.3 | Restrictions: No build step required, use vanilla JS, load SDK from unpkg.com CDN, keep assets minimal for performance | Success: Base template loads MCP Apps SDK correctly, app-client.js provides convenient wrapper for common operations, theme.css supports light/dark themes | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 1.6 Write unit tests for UiResourceProvider
  - File: `mcp_service/server/tests/unit/test_ui_resource_provider.py`
  - Test resource listing, reading, parameterized URIs
  - Test CSP metadata generation
  - Test error handling for invalid URIs
  - Purpose: Ensure resource provider reliability
  - _Leverage: `mcp_service/server/tests/unit/test_docs_provider.py`_
  - _Requirements: FR-1.1, FR-1.2, FR-1.4_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python QA Engineer with pytest expertise | Task: Create comprehensive unit tests for UiResourceProvider covering listing, reading, parameterized URIs, CSP generation, and error handling following FR-1.1, FR-1.2, FR-1.4, using test_docs_provider.py as pattern | Restrictions: Use pytest fixtures and async tests, mock file system operations, test both success and failure scenarios | Success: All provider methods tested, edge cases covered, tests pass independently and consistently | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

## Phase 2: First App - Simulation Results (P1)

- [ ] 2.1 Create simulation-results app HTML structure
  - File: `mcp_service/server/dwsim_mcp_server/apps/templates/simulation-results/index.html`
  - Build status indicator section (converged/failed/running)
  - Add summary metrics section layout
  - Add detailed results expandable sections
  - Purpose: Provide primary visualization for simulation tool results
  - _Leverage: `mcp_service/server/dwsim_mcp_server/apps/templates/base.html`_
  - _Requirements: FR-3.1, FR-3.3_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Frontend Developer with HTML/CSS and data visualization expertise | Task: Create simulation-results app HTML structure with status indicator, metrics summary, and expandable details following FR-3.1, FR-3.3, using base.html template | Restrictions: Self-contained HTML bundle, no external CSS frameworks, use semantic HTML, ensure responsive layout | Success: App displays simulation status clearly, metrics are readable, expandable sections work without JS errors, theme switching works | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 2.2 Implement simulation-results app JavaScript
  - File: `mcp_service/server/dwsim_mcp_server/apps/templates/simulation-results/index.html` (continue)
  - Initialize MCP Apps client and handle tool-result notification
  - Parse simulation result JSON and populate UI
  - Implement theme change handling
  - Purpose: Make simulation results app functional
  - _Leverage: `mcp_service/server/dwsim_mcp_server/apps/static/app-client.js`_
  - _Requirements: FR-3.2, FR-3.3_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: JavaScript Developer with MCP Apps SDK and DOM manipulation expertise | Task: Implement JavaScript for simulation-results app to handle tool-result notifications, parse JSON, populate UI, and support theme changes following FR-3.2, FR-3.3 | Restrictions: Vanilla JS only, handle missing/malformed data gracefully, use app-client.js wrapper, no console errors on load | Success: App initializes correctly, receives and displays tool results, theme switching updates colors, handles empty/error results gracefully | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 2.3 Create app.json configuration for simulation-results
  - File: `mcp_service/server/dwsim_mcp_server/apps/templates/simulation-results/app.json`
  - Define app metadata (name, title, description, version)
  - Configure CSP settings if needed
  - Set entry point and preferences
  - Purpose: Enable app discovery and configuration
  - _Leverage: None (new configuration)_
  - _Requirements: FR-1.3_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Configuration Developer with JSON schema expertise | Task: Create app.json configuration file for simulation-results app with metadata, CSP settings, and entry point following FR-1.3 | Restrictions: Follow AppConfig Pydantic model schema, use sensible defaults for optional fields, validate JSON syntax | Success: Configuration loads correctly in UiResourceProvider, metadata appears in resource listing, CSP settings are applied | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 2.4 Enhance run_simulation tool with _meta.ui
  - File: `mcp_service/server/dwsim_mcp_server/tools/simulation.py` (modify)
  - Add `_meta.ui` to run_simulation tool definition
  - Include UI metadata in tool results
  - Configure resource URI and visibility
  - Purpose: Enable visualization for simulation results
  - _Leverage: `mcp_service/server/dwsim_mcp_server/tools/ui_metadata.py`_
  - _Requirements: FR-2.1, FR-2.2, FR-2.3_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with MCP tool definition expertise | Task: Enhance run_simulation tool with _meta.ui metadata pointing to simulation-results app following FR-2.1, FR-2.2, FR-2.3, using ui_metadata utilities | Restrictions: Do not change tool functionality, maintain backward compatibility, add metadata only | Success: Tool definition includes _meta.ui, tool results include UI annotation, existing tool behavior unchanged | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 2.5 Create test harness for local app development
  - Files:
    - `mcp_service/server/dwsim_mcp_server/apps/test-harness/index.html`
    - `mcp_service/server/dwsim_mcp_server/apps/test-harness/mock-bridge.js`
  - Create HTML page with iframe for app loading
  - Implement MockAppBridge simulating host context and notifications
  - Add control panel for sending mock data and changing themes
  - Purpose: Enable local app development without MCP server
  - _Leverage: None (new test infrastructure)_
  - _Requirements: FR-4.1, FR-4.2_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Frontend Developer with testing infrastructure expertise | Task: Create test harness with iframe loader and MockAppBridge simulating MCP host following FR-4.1, FR-4.2, enabling local app development | Restrictions: Standalone HTML/JS, no dependencies, simulate all MCP Apps notifications, provide clear debug output | Success: Test harness loads apps in iframe, mock bridge simulates tool results, theme switching works, message log shows communication | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 2.6 Add mock data files for test harness
  - Files:
    - `mcp_service/server/dwsim_mcp_server/apps/test-harness/mocks/simulation-result-converged.json`
    - `mcp_service/server/dwsim_mcp_server/apps/test-harness/mocks/simulation-result-failed.json`
  - Create realistic mock simulation results
  - Include variety of scenarios (converged, failed, warnings)
  - Purpose: Provide test data for app development
  - _Leverage: Existing simulation result format from tools_
  - _Requirements: FR-4.3_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Data Engineer with simulation domain knowledge | Task: Create realistic mock simulation result JSON files for test harness following FR-4.3, covering converged and failed scenarios | Restrictions: Match actual simulation result schema, include realistic DWSIM data, provide variety of edge cases | Success: Mock files load correctly in test harness, apps render mock data without errors, scenarios cover success and failure cases | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 2.7 Write integration tests for UI resources
  - File: `mcp_service/server/tests/integration/test_ui_resource_integration.py`
  - Test resources/list includes UI resources
  - Test resources/read returns valid HTML
  - Test enhanced tool includes _meta.ui
  - Purpose: Verify end-to-end UI resource functionality
  - _Leverage: `mcp_service/server/tests/integration/test_resource_protocol.py`_
  - _Requirements: FR-1.1, FR-2.1_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer with MCP protocol integration testing expertise | Task: Create integration tests for UI resources verifying listing, reading, and tool metadata following FR-1.1, FR-2.1, using existing resource protocol tests as pattern | Restrictions: Test through MCP protocol layer, use realistic server setup, verify protocol compliance | Success: Integration tests verify UI resources appear in listings, HTML content is returned correctly, tool results include UI metadata | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

## Phase 3: Additional Apps (P2)

- [ ] 3.1 Create stream-properties app
  - File: `mcp_service/server/dwsim_mcp_server/apps/templates/stream-properties/index.html`
  - Build property table with T, P, flow rates
  - Add composition chart using Plotly.js (CDN)
  - Add phase distribution visualization
  - Handle tool-result notifications
  - Purpose: Visualize material stream properties
  - _Leverage: `mcp_service/server/dwsim_mcp_server/apps/templates/simulation-results/index.html`_
  - _Requirements: FR-3.1, FR-3.3_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Frontend Developer with data visualization and Plotly.js expertise | Task: Create stream-properties app with property table, composition chart, and phase distribution following FR-3.1, FR-3.3, using Plotly.js from CDN | Restrictions: Load Plotly.js from CDN, handle missing composition gracefully, responsive charts, match existing app patterns | Success: App displays stream properties in table, composition pie chart renders correctly, phase distribution shown, theme switching updates chart colors | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 3.2 Create stream-properties app.json and enhance tool
  - Files:
    - `mcp_service/server/dwsim_mcp_server/apps/templates/stream-properties/app.json`
    - `mcp_service/server/dwsim_mcp_server/tools/flowsheet.py` (modify for get_stream_properties)
  - Create app configuration with Plotly CDN in CSP
  - Enhance get_stream_properties tool with _meta.ui
  - Purpose: Enable stream property visualization
  - _Leverage: `mcp_service/server/dwsim_mcp_server/apps/templates/simulation-results/app.json`_
  - _Requirements: FR-2.1, FR-2.3_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python/Configuration Developer | Task: Create app.json for stream-properties with Plotly CDN CSP and enhance get_stream_properties tool following FR-2.1, FR-2.3 | Restrictions: Include cdn.plot.ly in CSP connectDomains, follow existing app.json pattern, maintain tool backward compatibility | Success: App loads Plotly.js without CSP errors, tool results include _meta.ui, configuration validates correctly | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 3.3 Create flowsheet-viewer app
  - File: `mcp_service/server/dwsim_mcp_server/apps/templates/flowsheet-viewer/index.html`
  - Build SVG-based flowsheet diagram renderer
  - Parse topology JSON and position units/streams
  - Add zoom/pan controls
  - Implement click handlers for unit details
  - Purpose: Interactive flowsheet visualization
  - _Leverage: `mcp_service/server/dwsim_mcp_server/apps/templates/simulation-results/index.html`_
  - _Requirements: FR-3.1, FR-3.4_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Frontend Developer with SVG and interactive visualization expertise | Task: Create flowsheet-viewer app with SVG diagram, zoom/pan controls, and clickable units following FR-3.1, FR-3.4 | Restrictions: Vanilla JS SVG manipulation, handle complex flowsheets gracefully, provide clear visual hierarchy, accessible controls | Success: Flowsheet renders from topology JSON, zoom/pan work smoothly, clicking units triggers appropriate actions, legend is clear | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 3.4 Create flowsheet-viewer app.json and enhance tool
  - Files:
    - `mcp_service/server/dwsim_mcp_server/apps/templates/flowsheet-viewer/app.json`
    - `mcp_service/server/dwsim_mcp_server/tools/flowsheet.py` (modify for get_flowsheet_topology)
  - Create app configuration
  - Enhance get_flowsheet_topology tool with _meta.ui
  - Purpose: Enable flowsheet topology visualization
  - _Leverage: `mcp_service/server/dwsim_mcp_server/apps/templates/simulation-results/app.json`_
  - _Requirements: FR-2.1, FR-2.3_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python/Configuration Developer | Task: Create app.json for flowsheet-viewer and enhance get_flowsheet_topology tool with _meta.ui following FR-2.1, FR-2.3 | Restrictions: Follow existing patterns, maintain tool backward compatibility | Success: App configuration loads correctly, tool results include _meta.ui, resources list shows flowsheet-viewer | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 3.5 Create diagnostics app
  - File: `mcp_service/server/dwsim_mcp_server/apps/templates/diagnostics/index.html`
  - Build server status display section
  - Add session list with details
  - Add resource usage metrics display
  - Implement periodic refresh via tool calls
  - Purpose: Server monitoring and debugging visualization
  - _Leverage: `mcp_service/server/dwsim_mcp_server/apps/templates/simulation-results/index.html`_
  - _Requirements: FR-3.1_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Frontend Developer with dashboard and metrics visualization expertise | Task: Create diagnostics app with server status, session list, and resource metrics following FR-3.1, with periodic refresh capability | Restrictions: Vanilla JS, handle refresh errors gracefully, clear visual indicators for status, minimal polling frequency | Success: App displays server diagnostics clearly, session list updates, resource usage shown with appropriate visualizations | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 3.6 Create diagnostics app.json and enhance tool
  - Files:
    - `mcp_service/server/dwsim_mcp_server/apps/templates/diagnostics/app.json`
    - `mcp_service/server/dwsim_mcp_server/tools/session.py` (modify for get_diagnostics)
  - Create app configuration
  - Enhance get_diagnostics tool with _meta.ui
  - Purpose: Enable diagnostics visualization
  - _Leverage: `mcp_service/server/dwsim_mcp_server/apps/templates/simulation-results/app.json`_
  - _Requirements: FR-2.1, FR-2.3_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python/Configuration Developer | Task: Create app.json for diagnostics and enhance get_diagnostics tool with _meta.ui following FR-2.1, FR-2.3 | Restrictions: Follow existing patterns, maintain tool backward compatibility | Success: App configuration loads correctly, tool results include _meta.ui | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 3.7 Add mock data files for additional apps
  - Files:
    - `mcp_service/server/dwsim_mcp_server/apps/test-harness/mocks/stream-properties.json`
    - `mcp_service/server/dwsim_mcp_server/apps/test-harness/mocks/flowsheet-topology.json`
    - `mcp_service/server/dwsim_mcp_server/apps/test-harness/mocks/diagnostics.json`
  - Create realistic mock data for each app type
  - Purpose: Enable test harness usage for all apps
  - _Leverage: Existing tool result formats_
  - _Requirements: FR-4.3_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Data Engineer with simulation domain knowledge | Task: Create mock data JSON files for stream-properties, flowsheet-topology, and diagnostics apps following FR-4.3 | Restrictions: Match actual tool result schemas, provide realistic DWSIM data, include edge cases | Success: Mock files load correctly in test harness, all apps render mock data without errors | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

## Phase 4: Documentation and Polish (P3)

- [ ] 4.1 Document test harness usage
  - File: `mcp_service/server/dwsim_mcp_server/apps/README.md`
  - Explain how to run test harness locally
  - Document how to load different apps
  - Describe how to inject test data
  - Purpose: Enable developers to work on apps locally
  - _Leverage: None_
  - _Requirements: FR-4.4_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Technical Writer with developer documentation expertise | Task: Create comprehensive README documenting test harness usage, app loading, and test data injection following FR-4.4 | Restrictions: Clear step-by-step instructions, include troubleshooting section, assume minimal prior MCP knowledge | Success: Developer can follow README to run test harness and test apps locally without additional help | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 4.2 Document app creation process
  - File: `mcp_service/server/dwsim_mcp_server/apps/CREATING_APPS.md`
  - Explain app structure requirements
  - Document MCP Apps SDK usage patterns
  - Provide template for new apps
  - Purpose: Enable creation of new visualization apps
  - _Leverage: Existing app implementations_
  - _Requirements: FR-4.4_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Technical Writer with SDK documentation expertise | Task: Create CREATING_APPS.md documenting app structure, MCP Apps SDK patterns, and providing a template following FR-4.4 | Restrictions: Include code examples, reference existing apps, explain all required files and patterns | Success: Developer can create a new app by following the documentation without referencing existing app code extensively | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

- [ ] 4.3 Add settings configuration for apps
  - File: `mcp_service/server/dwsim_mcp_server/config/settings.py` (modify)
  - Add apps_path setting with default
  - Add app caching configuration
  - Add CSP default configuration
  - Purpose: Make apps feature configurable
  - _Leverage: `mcp_service/server/dwsim_mcp_server/config/settings.py`_
  - _Requirements: NFR-4_
  - _Prompt: Implement the task for spec mcp-apps-backend, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer with configuration management expertise | Task: Add apps configuration settings (apps_path, caching, CSP defaults) to settings.py following NFR-4 | Restrictions: Follow existing settings patterns, use Pydantic with environment variable support, provide sensible defaults | Success: Apps path is configurable via environment or config, caching settings work, CSP defaults can be overridden | Instructions: Before starting, edit tasks.md to change [ ] to [-] for this task. After completing, use log-implementation tool to record what was implemented, then change [-] to [x] in tasks.md_

## Definition of Done

### For Each Task
- Code implemented following design.md
- Follows project structure.md conventions
- Unit tests pass (where applicable)
- Works in test harness (for apps)
- Implementation logged with log-implementation tool
- Task marked complete in tasks.md

### For Each Phase
- All phase tasks completed
- Integration tests pass
- No regressions in existing functionality
- Documentation updated
