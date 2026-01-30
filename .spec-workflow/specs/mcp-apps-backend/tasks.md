# MCP Apps Backend - Implementation Tasks

## Phase 2: Tool Enhancement & Resource Serving

### 2.1 Infrastructure Setup

- [ ] **2.1.1** Create `apps/` directory structure under `dwsim_mcp_server/`
  - `apps/__init__.py`
  - `apps/templates/` for HTML templates
  - `apps/static/` for shared JS/CSS

- [ ] **2.1.2** Create `UiResourceProvider` class in `resources/ui_resource_provider.py`
  - Inherit from `BaseResourceProvider`
  - Handle `ui://dwsim/*` URI pattern
  - Return HTML with `text/html;profile=mcp-app` MIME type

- [ ] **2.1.3** Register UI resource provider in server initialization
  - Add to `create_server()` in `server.py`
  - Ensure resources are listed in `resources/list`

- [ ] **2.1.4** Add `_meta.ui` support to tool builder utilities
  - Create helper function `add_ui_metadata(tool, resource_uri, visibility)`
  - Update tool schema generation to include `_meta`

### 2.2 Tool Enhancement

- [ ] **2.2.1** Enhance `run_simulation` tool with UI metadata
  ```python
  _meta.ui = {
      "resourceUri": "ui://dwsim/simulation-results",
      "visibility": ["model", "app"]
  }
  ```

- [ ] **2.2.2** Enhance `get_stream_properties` tool with UI metadata
  ```python
  _meta.ui = {
      "resourceUri": "ui://dwsim/stream-properties/{stream_id}",
      "visibility": ["model", "app"]
  }
  ```

- [ ] **2.2.3** Enhance `get_flowsheet_topology` tool with UI metadata
  ```python
  _meta.ui = {
      "resourceUri": "ui://dwsim/flowsheet-viewer",
      "visibility": ["model", "app"]
  }
  ```

- [ ] **2.2.4** Enhance `get_diagnostics` tool with UI metadata
  ```python
  _meta.ui = {
      "resourceUri": "ui://dwsim/diagnostics",
      "visibility": ["model", "app"]
  }
  ```

### 2.3 Resource Implementation

- [ ] **2.3.1** Implement resource listing for UI resources
  - Return all available `ui://dwsim/*` resources
  - Include proper metadata (name, description, mimeType, _meta.ui)

- [ ] **2.3.2** Implement resource reading for UI resources
  - Load HTML template from `apps/templates/`
  - Inject CSP metadata into response
  - Handle parameterized URIs (extract path parameters)

- [ ] **2.3.3** Create base HTML template with MCP Apps SDK
  ```html
  <!-- apps/templates/base.html -->
  <script src="https://unpkg.com/@modelcontextprotocol/ext-apps/dist/index.umd.js"></script>
  ```

- [ ] **2.3.4** Add CSP configuration per app
  - Define default restrictive CSP
  - Allow apps to declare additional domains

### 2.4 Testing

- [ ] **2.4.1** Write unit tests for `UiResourceProvider`
  - Test resource listing
  - Test resource reading
  - Test parameterized URIs
  - Test CSP metadata generation

- [ ] **2.4.2** Write integration tests for tool metadata
  - Verify `_meta.ui` appears in tool definitions
  - Verify resource URIs are valid

---

## Phase 3: App Development

### 3.1 Test Harness

- [ ] **3.1.1** Create test harness HTML page
  - `apps/test-harness/index.html`
  - Iframe to load apps
  - Controls for sending mock data
  - Message log panel

- [ ] **3.1.2** Create mock app-bridge implementation
  - `apps/test-harness/mock-bridge.js`
  - Simulate `ui/notifications/tool-input`
  - Simulate `ui/notifications/tool-result`
  - Simulate `ui/notifications/host-context-changed`
  - Log all messages from app

- [ ] **3.1.3** Add sample mock data files
  - `apps/test-harness/mocks/simulation-result.json`
  - `apps/test-harness/mocks/stream-properties.json`
  - `apps/test-harness/mocks/flowsheet-topology.json`

- [ ] **3.1.4** Document test harness usage
  - How to run locally
  - How to load different apps
  - How to inject test data

### 3.2 Simulation Results Dashboard

- [ ] **3.2.1** Create base HTML structure
  - `apps/templates/simulation-results/index.html`
  - Status indicator (converged/failed/running)
  - Summary metrics section
  - Detailed results tabs

- [ ] **3.2.2** Implement MCP Apps client integration
  - Initialize with `McpApp.initialize()`
  - Handle `tool-result` notification
  - Parse simulation result JSON

- [ ] **3.2.3** Build status display component
  - Green/red/yellow status indicator
  - Iteration count
  - Convergence tolerance
  - Solver messages

- [ ] **3.2.4** Build metrics summary component
  - Key performance indicators
  - Energy balance
  - Mass balance
  - Warnings/errors count

- [ ] **3.2.5** Build detailed results view
  - Expandable sections per unit operation
  - Stream summary table
  - Link to open stream details

- [ ] **3.2.6** Add theme support
  - Listen for `host-context-changed`
  - Apply light/dark theme
  - Match host styling

### 3.3 Stream Properties Viewer

- [ ] **3.3.1** Create base HTML structure
  - `apps/templates/stream-properties/index.html`
  - Property table
  - Composition chart
  - Phase information

- [ ] **3.3.2** Implement property table
  - Temperature, pressure, flow rates
  - Sortable columns
  - Unit conversion display

- [ ] **3.3.3** Implement composition pie chart
  - Use Plotly.js or Chart.js
  - Show mole/mass fractions
  - Interactive legend

- [ ] **3.3.4** Implement phase distribution view
  - Vapor/liquid/solid fractions
  - Phase properties comparison

### 3.4 Flowsheet Viewer

- [ ] **3.4.1** Create base HTML structure
  - `apps/templates/flowsheet-viewer/index.html`
  - SVG canvas for diagram
  - Zoom/pan controls
  - Legend panel

- [ ] **3.4.2** Implement flowsheet rendering
  - Parse topology JSON
  - Position unit operations
  - Draw stream connections

- [ ] **3.4.3** Add interactivity
  - Click unit → show properties tooltip
  - Click stream → show stream data
  - Hover highlights

- [ ] **3.4.4** Implement tool calls from app
  - Click unit → call `get_unit_properties`
  - Use `tools/call` via app bridge

### 3.5 Diagnostics Viewer

- [ ] **3.5.1** Create diagnostics dashboard
  - `apps/templates/diagnostics/index.html`
  - Server status panel
  - Session list
  - Resource usage charts

- [ ] **3.5.2** Implement auto-refresh
  - Poll for updates via tool calls
  - Update charts in real-time

---

## Phase 3+: Future Enhancements

### Backlog

- [ ] **B.1** Sensitivity analysis chart app
- [ ] **B.2** T-xy / P-xy diagram app
- [ ] **B.3** Energy balance Sankey diagram
- [ ] **B.4** Equipment sizing calculator app
- [ ] **B.5** Cost estimation summary app

---

## Definition of Done

### For Each Task
- [ ] Code implemented
- [ ] Unit tests pass
- [ ] Works in test harness
- [ ] Documentation updated
- [ ] Code reviewed

### For Each Phase
- [ ] All tasks completed
- [ ] Integration tests pass
- [ ] End-to-end test with frontend
- [ ] Performance requirements met
