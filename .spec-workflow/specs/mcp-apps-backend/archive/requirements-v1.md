# MCP Apps Backend Integration - Requirements

## Overview

Enable the DWSIM MCP Server to deliver interactive UI applications alongside tool results, following the MCP Apps Extension specification (SEP-1865).

## Goals

1. **Enhance existing tools** with `_meta.ui` metadata to reference UI resources
2. **Serve HTML applications** as `ui://` resources via MCP protocol
3. **Build domain-specific apps** for simulation visualization
4. **Enable local testing** without requiring full frontend integration

## Non-Goals

- Building the MCP host/client (that's the frontend's responsibility)
- User authentication within apps (inherited from host)
- Offline app functionality (not in initial scope)

---

## Functional Requirements

### FR-1: UI Resource Provider

**FR-1.1**: The server MUST implement a resource provider that serves `ui://dwsim/*` URIs.

**FR-1.2**: Resources MUST return HTML content with MIME type `text/html;profile=mcp-app`.

**FR-1.3**: Resources MUST include `_meta.ui` with:
- `csp`: Content Security Policy configuration
- `permissions`: Required browser permissions (if any)
- `prefersBorder`: Boolean for visual framing preference

**FR-1.4**: The resource provider MUST support parameterized resources (e.g., `ui://dwsim/stream/{stream_id}`).

### FR-2: Tool Enhancement

**FR-2.1**: Tools that benefit from visualization MUST include `_meta.ui.resourceUri` pointing to their UI resource.

**FR-2.2**: Tools MUST specify `visibility` in `_meta.ui`:
- `["model"]` - Only visible to LLM
- `["app"]` - Only callable by UI apps
- `["model", "app"]` - Both

**FR-2.3**: The following tools SHOULD be enhanced with UI resources:

| Tool | UI Resource | Priority |
|------|-------------|----------|
| `run_simulation` | Simulation results dashboard | P0 |
| `get_stream_properties` | Stream property table/charts | P1 |
| `get_flowsheet_topology` | Interactive flowsheet diagram | P1 |
| `get_diagnostics` | Server/session diagnostics view | P2 |
| `analyze_sensitivity` | Multi-parameter chart view | P2 |

### FR-3: App Development

**FR-3.1**: Apps MUST be self-contained HTML/JS/CSS bundles.

**FR-3.2**: Apps MUST use the MCP Apps client SDK (`@modelcontextprotocol/ext-apps`).

**FR-3.3**: Apps MUST handle these notifications:
- `ui/notifications/tool-input` - Receive tool arguments
- `ui/notifications/tool-result` - Receive tool results
- `ui/notifications/host-context-changed` - Theme/size changes

**FR-3.4**: Apps MAY call tools via `tools/call` request (if visibility allows).

**FR-3.5**: Apps MAY send messages to chat via `ui/message` request.

### FR-4: Test Harness

**FR-4.1**: A local test harness MUST be provided for app development.

**FR-4.2**: The test harness MUST simulate:
- Host initialization with mock context
- Tool input/result notifications
- Message logging for debugging

**FR-4.3**: The test harness MUST run standalone (no MCP server required).

---

## Non-Functional Requirements

### NFR-1: Performance

- App HTML bundles SHOULD be < 500KB uncompressed
- Resource fetch latency SHOULD be < 100ms for cached resources
- Apps SHOULD render initial view within 500ms

### NFR-2: Security

- Apps MUST NOT require `allow-same-origin` sandbox permission
- Apps MUST declare all external domains in CSP metadata
- Apps MUST NOT access localStorage/sessionStorage (sandboxed)

### NFR-3: Compatibility

- Apps MUST work in Chrome 90+, Firefox 90+, Edge 90+, Safari 15+
- Apps SHOULD degrade gracefully if JavaScript is disabled
- Apps MUST handle missing tool data without crashing

### NFR-4: Maintainability

- Apps SHOULD use vanilla JS or lightweight frameworks
- Apps SHOULD minimize external dependencies
- App code SHOULD be documented with JSDoc comments

---

## Technical Constraints

1. **Python resource serving**: Apps are served via Python MCP server, not a separate static server
2. **No build step required**: Apps should work without webpack/vite (optional build for optimization)
3. **Portable**: Apps can be copied to frontend repo if needed
4. **DWSIM data structures**: Apps must handle DWSIM-specific JSON schemas

---

## Dependencies

| Dependency | Version | Purpose |
|------------|---------|---------|
| `@modelcontextprotocol/ext-apps` | ^1.0.0 | Client SDK for apps (loaded via CDN) |
| `plotly.js` | ^2.x | Charts and graphs (optional) |
| `d3.js` | ^7.x | Custom visualizations (optional) |

---

## Acceptance Criteria

### Phase 2: Tool Enhancement

- [ ] UI resource provider implemented and tested
- [ ] At least one tool (`run_simulation`) has `_meta.ui` metadata
- [ ] Resource returns valid HTML with correct MIME type
- [ ] CSP metadata is properly structured

### Phase 3: App Development

- [ ] Simulation results dashboard app completed
- [ ] App receives and displays tool results
- [ ] App handles theme changes from host
- [ ] Test harness works for local development
- [ ] Documentation for creating new apps

---

## Glossary

| Term | Definition |
|------|------------|
| **MCP Apps** | Extension to MCP enabling interactive UIs in chat clients |
| **ui:// resource** | URI scheme for app HTML content |
| **App Bridge** | Host-side SDK for iframe communication |
| **Host Context** | Theme, dimensions, locale provided by host to app |
| **Tool Visibility** | Controls whether tool is visible to model, app, or both |
