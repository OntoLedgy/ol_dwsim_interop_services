# DWSIM MCP Test Harness - Getting Started

A browser-based environment for testing DWSIM MCP UI apps. Supports offline testing with mock data and live testing against a running MCP server.

## Prerequisites

- Python 3.x (for the static file server)
- A modern browser (Chrome, Firefox, Edge)
- **Live mode only**: A running DWSIM MCP server with OAuth credentials

## Quick Start (Mock Mode)

Mock mode lets you test app UIs with sample data — no server needed.

### 1. Clone the repository

```bash
git clone <repo-url>
cd dwsim_interop_services
```

### 2. Start the static file server

```bash
cd mcp_service/server/dwsim_mcp_server/apps
python -m http.server 8080
```

### 3. Open the test harness

Navigate to:

```
http://localhost:8080/test-harness/index.html
```

You should see a two-panel layout: a control sidebar on the left and an app preview iframe on the right.

### 4. Test an app with mock data

1. Select an app from the **App** dropdown (default is `scenario-setup`)
2. Click **Load App** — the app loads in the iframe
3. The matching mock data is auto-selected and sent to the app
4. To manually send different data, pick from the **Mock Data** dropdown and click **Send Tool Result**

That's it — you're running the test harness.

## Interface Overview

```
┌─────────────────────┬──────────────────────────────────────┐
│  Control Panel       │                                      │
│                      │                                      │
│  [App Dropdown]      │        App Preview (iframe)          │
│  [Load App]          │                                      │
│                      │   Selected app renders here          │
│  ── Mock / Live ──   │                                      │
│                      │                                      │
│  [Mock Data Select]  │                                      │
│  [Send Tool Result]  │                                      │
│                      │                                      │
│  [Toggle Theme]      │                                      │
│                      │                                      │
│  ┌── Message Log ──┐ │                                      │
│  │ timestamps ...   │ │                                      │
│  └─────────────────┘ │                                      │
└─────────────────────┴──────────────────────────────────────┘
```

### Controls

| Control | Description |
|---------|-------------|
| **App dropdown** | Select which app template to load |
| **Load App** | Reload the selected app in the iframe |
| **Mock / Live tabs** | Switch between mock data and live server modes |
| **Mock Data dropdown** | Choose which sample data to send |
| **Send Tool Result** | Push the selected mock data to the app |
| **Auto-send on app load** | Checkbox — automatically send mock data when an app loads |
| **Toggle Theme** | Switch between light and dark themes |
| **Message Log** | Shows timestamped messages between harness and app |

## Available Apps

| App | Description | Default Mock Data |
|-----|-------------|-------------------|
| `scenario-setup` | Configure and run three-phase separator simulations | `simulation-result-converged` |
| `simulation-results` | Display simulation convergence, streams, mass balance | `simulation-result-converged` |
| `stream-properties` | Show detailed stream thermodynamic properties | `stream-properties` |
| `flowsheet-viewer` | Visualize equipment and stream connections | `flowsheet-topology` |
| `sensitivity-analysis` | Chart sensitivity study and parameter sweep results | `sensitivity-2d` |
| `diagnostics` | Server health, memory usage, active sessions | `diagnostics` |

## Available Mock Data

| File | Contents |
|------|----------|
| `simulation-result-converged` | Successful simulation with 2 streams, mass balance OK |
| `simulation-result-failed` | Failed convergence with error messages |
| `stream-properties` | Vapor/liquid phase compositions for a single stream |
| `flowsheet-topology` | 4 units, 7 streams, 10 connections (separator + cooler + pumps) |
| `sensitivity-2d` | Single-variable sweep: temperature vs. phase fractions (11 points) |
| `sensitivity-3d` | Two-variable sweep: temperature x pressure grid (25 points) |
| `sensitivity (real MCP format)` | Sensitivity results in actual MCP server response format |
| `diagnostics` | Server uptime, memory, active sessions |

## Live Mode

Live mode connects to a real DWSIM MCP server to run actual simulations.

### Prerequisites for Live Mode

1. A DWSIM MCP server running in Streamable HTTP mode with OAuth enabled
2. OAuth client credentials (Client ID and Client Secret) from your auth provider (e.g., Clerk)

### Connecting

1. Click the **Live Server** tab in the control panel
2. Enter:
   - **MCP Server URL**: The server's MCP endpoint (e.g., `https://your-domain.com/mcp`)
   - **Client ID**: Your OAuth client ID
   - **Client Secret**: Your OAuth client secret
3. Click **Login & Connect**
4. A popup opens for OAuth login — sign in with your credentials
5. On success, the status badge turns green ("Connected") and a DWSIM session ID appears

These settings are saved to `localStorage` so you don't need to re-enter them each time.

### Calling Tools Directly

Once connected:

1. Select a tool from the **Tool** dropdown (`get_diagnostics`, `list_objects`, `run`, etc.)
2. Edit the **Arguments (JSON)** field if needed
3. Click **Call Tool**
4. The `session_id` is auto-injected — you don't need to include it manually
5. Results are forwarded to the app in the iframe and logged in the Message Log

### Running a Full Scenario (Scenario Orchestration)

The `scenario-setup` app can orchestrate a complete simulation workflow through the harness:

1. Load the `scenario-setup` app
2. Switch to **Live Server** mode and connect
3. In the app, select a predefined scenario (e.g., "Light Sweet Crude") or configure a custom one
4. Click **Run Simulation** in the app

The harness automatically executes these steps against the live server:

| Step | Operation |
|------|-----------|
| 1 | Create a new DWSIM session |
| 2 | Add all compounds from the scenario composition |
| 3 | Set the thermodynamic property package |
| 4 | Create the feed stream with temperature, pressure, flow, composition |
| 5 | Create three outlet streams (vapor, liquid 1, liquid 2) |
| 6 | Create a three-phase separator unit |
| 7 | Resolve auto-generated object IDs via `list_objects` |
| 8 | Flash the feed stream |
| 9 | Connect all streams to the separator |
| 10 | Run the DWSIM solver |
| 11 | Retrieve and display results |
| 12 | Close the scenario session |

A progress bar and status messages update in real time as each step completes.

### Disconnecting

Click **Disconnect** to close the DWSIM session and clear credentials.

## Theme Testing

Click **Toggle Theme** to switch the harness and app between light and dark modes. This sends a `host-context-changed` notification to the app, which should update its CSS variables and re-render any charts.

## Adding Custom Mock Data

1. Create a JSON file matching the expected data format (see examples in `apps/test-harness/mocks/`)
2. Add your data as a new key in the `MOCK_DATA` object inside `apps/test-harness/index.html` (the mock data is embedded inline to avoid CORS issues)
3. Add a corresponding `<option>` in the mock data `<select>` element
4. Reload the harness

## How It Works

### Architecture

```
┌─────────────────────────────────────────────────┐
│  Test Harness (index.html)                       │
│                                                  │
│  ┌──────────────┐    postMessage     ┌────────┐ │
│  │ MockAppBridge │ ───────────────► │ iframe  │ │
│  │ (mock-bridge) │ ◄─────────────── │  (app)  │ │
│  └──────────────┘                    └────────┘ │
│                                          │       │
│  Live mode:                              │       │
│  ┌──────────────┐                        ▼       │
│  │ OAuth + MCP  │              ┌────────────────┐│
│  │ HTTP client  │              │  app-client.js  ││
│  └──────┬───────┘              │  (DwsimApp SDK) ││
│         │                      └────────────────┘│
└─────────┼────────────────────────────────────────┘
          │  JSON-RPC 2.0 over HTTP
          ▼
   ┌──────────────┐
   │  MCP Server   │  (only in Live mode)
   └──────────────┘
```

### Communication Protocol

The harness communicates with apps via `window.postMessage` using JSON-RPC 2.0 format:

**Harness → App** (three notification types):
- `ui/notifications/tool-result` — simulation results, tool outputs
- `ui/notifications/tool-input` — input data
- `ui/notifications/host-context-changed` — theme changes

**App → Harness** (for scenario orchestration):
- `dwsim-run-scenario` — request to run a full simulation scenario

### App SDK (app-client.js)

Apps use `window.DwsimApp` to receive data:

```javascript
window.DwsimApp.initialize({
  onToolResult: function(params) {
    // params.structuredContent contains the data
    renderResults(params.structuredContent);
  },
  onHostContextChanged: function(context) {
    // context.theme is "light" or "dark"
    window.DwsimApp.applyTheme(context.theme);
  },
  onInitialized: function(context) {
    console.log("App ready");
  }
});
```

The SDK automatically detects whether it's running inside the test harness (postMessage) or a real MCP host (official MCP Apps SDK) and uses the appropriate communication method.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Blank iframe | Ensure the static server is running from the `apps/` directory |
| Missing styles | Check that `../static/theme.css` paths resolve correctly |
| App doesn't update | Confirm the app listens for `ui/notifications/tool-result` via `DwsimApp.initialize` |
| OAuth popup blocked | Allow popups for `localhost:8080` in your browser |
| "Not connected" on scenario run | Switch to Live Server tab and click Login & Connect first |
| Token expired | Click Disconnect, then Login & Connect again |
| CORS errors in Live mode | The MCP server must have CORS configured for your harness origin |

## File Reference

```
apps/
├── static/
│   ├── app-client.js          # DwsimApp SDK (used by all apps)
│   └── theme.css              # Shared CSS variables and components
├── test-harness/
│   ├── index.html             # Test harness UI + embedded mock data + scenario orchestration
│   ├── mock-bridge.js         # postMessage bridge (harness → app communication)
│   └── mocks/                 # JSON mock data files (also embedded in index.html)
└── templates/
    ├── scenario-setup/        # Scenario configuration and execution app
    ├── simulation-results/    # Results viewer
    ├── stream-properties/     # Stream detail viewer
    ├── flowsheet-viewer/      # Equipment/connection diagram
    ├── sensitivity-analysis/  # Sensitivity study charts
    └── diagnostics/           # Server health dashboard
```
