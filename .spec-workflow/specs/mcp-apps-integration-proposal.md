# MCP Apps Integration Proposal

## Executive Summary

This document proposes integrating the MCP Apps extension (SEP-1865) to enable interactive UIs for DWSIM simulation results within your Next.js platform. This would allow rich visualizations (flowsheet diagrams, charts, 3D views) to render inline in the chat interface.

---

## What is MCP Apps Extension?

**MCP Apps** (`@modelcontextprotocol/ext-apps`) is a specification and SDK that enables MCP servers to deliver interactive HTML-based UIs within conversational AI clients.

### Key Concepts

| Concept | Description |
|---------|-------------|
| `ui://` Resources | HTML content declared by MCP tools that hosts render in iframes |
| App Bridge | Host-side SDK for embedding and communicating with apps |
| Sandbox Proxy | Security pattern using nested iframes with restricted permissions |
| Tool Visibility | Controls whether tools are visible to model, app, or both |

### Communication Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│  Next.js Frontend (MCP Host)                                        │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │ Chat Interface                                                │   │
│  │  ┌────────────────────────────────────────────────────────┐  │   │
│  │  │ Sandboxed Iframe (MCP App)                             │  │   │
│  │  │ - Renders ui:// resource HTML                          │  │   │
│  │  │ - postMessage ↔ JSON-RPC 2.0                           │  │   │
│  │  │ - Can call tools, read resources                       │  │   │
│  │  └────────────────────────────────────────────────────────┘  │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                              ↕ MCP Protocol                          │
└─────────────────────────────────────────────────────────────────────┘
                               ↕ HTTP/WebSocket
┌─────────────────────────────────────────────────────────────────────┐
│  DWSIM MCP Server                                                    │
│  - Exposes tools with ui:// resource references                      │
│  - Serves HTML content for visualization                             │
│  - Returns tool results with structured data                         │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Architecture Decision: Where to Develop?

### Option A: Develop in DWSIM Repo (Backend Focus)

**What goes here:**
- MCP tool definitions with `ui://` resource metadata
- HTML/JS app templates for simulation visualization
- Static HTML served as MCP resources

**Pros:**
- Keeps domain-specific visualization with domain code
- Apps are tightly coupled with simulation data structures
- Single deployment unit for server + apps

**Cons:**
- Python/C# repo now has JavaScript/HTML assets
- Harder to iterate on UI separately

### Option B: Develop in Frontend Repo (Host Focus)

**What goes here:**
- MCP client/host implementation
- App bridge integration
- Iframe sandbox management
- Chat UI components

**Pros:**
- Keeps frontend code together
- Easier to style consistently with platform
- TypeScript/React alignment

**Cons:**
- Need to coordinate with backend for tool changes

### Option C: Hybrid (Recommended)

| Component | Repository | Technology |
|-----------|------------|------------|
| MCP Host (app-bridge) | Frontend (Next.js) | TypeScript, React |
| Tool definitions with `_meta.ui` | DWSIM repo | Python |
| Reusable app templates | Shared package or CDN | HTML/JS/React |
| Domain-specific apps (flowsheet viz) | DWSIM repo | HTML/JS |

**This is the recommended approach** because:
1. Frontend owns the rendering/host logic (Next.js strengths)
2. Backend owns the domain logic and tool definitions
3. Apps can be served from either location based on complexity

---

## Implementation Plan

### Phase 1: Frontend - MCP Host Implementation

**Location:** Frontend Next.js repo

**Tasks:**
1. Install and configure `@modelcontextprotocol/ext-apps/app-bridge`
2. Create `<McpAppRenderer>` component for iframe management
3. Implement sandbox proxy pattern for security
4. Handle host context (theme, dimensions, locale)
5. Wire postMessage communication

**Key Files to Create:**

```
src/
├── components/
│   └── mcp/
│       ├── McpAppHost.tsx        # Main host component
│       ├── McpAppRenderer.tsx    # Iframe renderer
│       ├── SandboxProxy.tsx      # Security sandbox
│       └── useAppBridge.ts       # React hook for app-bridge
├── lib/
│   └── mcp/
│       ├── client.ts             # MCP client with HTTP transport
│       ├── app-bridge.ts         # App bridge wrapper
│       └── types.ts              # TypeScript interfaces
```

**Dependencies to Add:**

```json
{
  "@modelcontextprotocol/sdk": "^1.x",
  "@modelcontextprotocol/ext-apps": "^1.x"
}
```

### Phase 2: Backend - Tool Enhancement

**Location:** DWSIM repo (`mcp_service/server/`)

**Tasks:**
1. Add `_meta.ui` to tool definitions that benefit from visualization
2. Create HTML templates for simulation results
3. Serve HTML as `ui://` resources
4. Implement resource provider for app content

**Tools to Enhance:**

| Tool | UI Enhancement |
|------|----------------|
| `run_simulation` | Interactive results dashboard |
| `get_stream_properties` | Property table with charts |
| `get_flowsheet_topology` | Interactive flowsheet diagram |
| `analyze_sensitivity` | Multi-chart analysis view |

**Example Tool Definition:**

```python
# In tools/simulation.py
def build_simulation_tools() -> list[types.Tool]:
    return [
        types.Tool(
            name="run_simulation",
            description="Run the flowsheet simulation",
            inputSchema=RunSimulationInput.model_json_schema(),
            # NEW: Add UI metadata
            annotations={
                "_meta": {
                    "ui": {
                        "resourceUri": "ui://dwsim/simulation-results",
                        "visibility": ["model", "app"]
                    }
                }
            }
        )
    ]
```

### Phase 3: App Development

**Location:** Either repo, or shared package

**Example Apps to Build:**

1. **Simulation Results Dashboard**
   - Shows converged/failed status
   - Displays key KPIs
   - Links to detailed stream data

2. **Flowsheet Visualizer**
   - Interactive P&ID-style diagram
   - Click units to see properties
   - Zoom/pan controls

3. **Property Charts**
   - T-xy, P-xy diagrams
   - Composition charts
   - Energy balance Sankey

**Technology Options:**

| Framework | Use Case |
|-----------|----------|
| React | Complex interactive apps |
| Vanilla JS | Simple displays, faster load |
| Plotly.js | Charts and graphs |
| D3.js | Custom flowsheet rendering |
| Three.js | 3D visualization |

---

## Frontend Integration Details

### MCP Client Setup

```typescript
// lib/mcp/client.ts
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StreamableHTTPClientTransport } from '@modelcontextprotocol/sdk/client/streamableHttp.js';

export async function createMcpClient(serverUrl: string) {
  const client = new Client({
    name: 'dwsim-platform',
    version: '1.0.0',
    capabilities: {
      // Advertise UI extension support
      extensions: {
        'io.modelcontextprotocol/ui': {
          supportedMimeTypes: ['text/html;profile=mcp-app']
        }
      }
    }
  });

  const transport = new StreamableHTTPClientTransport(
    new URL(serverUrl)
  );

  await client.connect(transport);
  return client;
}
```

### App Renderer Component

```tsx
// components/mcp/McpAppRenderer.tsx
import { useEffect, useRef, useState } from 'react';
import { createAppBridge } from '@modelcontextprotocol/ext-apps/app-bridge';

interface McpAppRendererProps {
  resourceUri: string;
  toolInput?: unknown;
  toolResult?: unknown;
  onMessage?: (message: string) => void;
}

export function McpAppRenderer({
  resourceUri,
  toolInput,
  toolResult,
  onMessage
}: McpAppRendererProps) {
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const [bridge, setBridge] = useState<ReturnType<typeof createAppBridge>>();

  useEffect(() => {
    if (!iframeRef.current) return;

    const appBridge = createAppBridge({
      iframe: iframeRef.current,
      hostContext: {
        theme: 'light', // or from your theme context
        containerWidth: 'flexible',
        containerHeight: 400,
        locale: 'en-US',
        timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
      },
      onToolCall: async (name, args) => {
        // Forward tool calls to MCP client
        return mcpClient.callTool({ name, arguments: args });
      },
      onMessage: (msg) => onMessage?.(msg),
    });

    setBridge(appBridge);

    return () => appBridge.destroy();
  }, [resourceUri]);

  // Send tool data when available
  useEffect(() => {
    if (bridge && toolInput) {
      bridge.sendToolInput(toolInput);
    }
  }, [bridge, toolInput]);

  useEffect(() => {
    if (bridge && toolResult) {
      bridge.sendToolResult(toolResult);
    }
  }, [bridge, toolResult]);

  return (
    <iframe
      ref={iframeRef}
      src={`/api/mcp/sandbox?resource=${encodeURIComponent(resourceUri)}`}
      sandbox="allow-scripts"
      style={{ width: '100%', height: 400, border: 'none' }}
    />
  );
}
```

### Sandbox Proxy API Route

```typescript
// app/api/mcp/sandbox/route.ts
import { NextRequest, NextResponse } from 'next/server';

export async function GET(request: NextRequest) {
  const resourceUri = request.nextUrl.searchParams.get('resource');

  if (!resourceUri?.startsWith('ui://')) {
    return new NextResponse('Invalid resource URI', { status: 400 });
  }

  // Fetch resource from MCP server
  const html = await mcpClient.readResource({ uri: resourceUri });

  // Return with strict CSP headers
  return new NextResponse(html.contents[0].text, {
    headers: {
      'Content-Type': 'text/html',
      'Content-Security-Policy': buildCsp(resourceUri),
      'X-Frame-Options': 'SAMEORIGIN',
    },
  });
}
```

---

## Security Considerations

### Iframe Sandbox Attributes

```html
<iframe
  sandbox="allow-scripts allow-forms"
  <!-- NO allow-same-origin for untrusted content -->
  <!-- NO allow-top-navigation -->
>
```

### Content Security Policy

```
default-src 'none';
script-src 'self' 'unsafe-inline';
style-src 'self' 'unsafe-inline';
img-src 'self' data: blob:;
connect-src https://your-mcp-server.com;
```

### Domain Allowlists

Apps declare required domains in `_meta.ui.csp`:
- `connectDomains`: API endpoints
- `resourceDomains`: CDN for assets
- `frameDomains`: Nested iframes (if any)

Host validates and enforces these.

---

## What the Frontend Repo Needs

### 1. Package Dependencies

```bash
npm install @modelcontextprotocol/sdk @modelcontextprotocol/ext-apps
```

### 2. Environment Configuration

```env
# .env.local
MCP_SERVER_URL=http://localhost:8000/mcp
MCP_SERVER_TIMEOUT=30000
```

### 3. New Components/Files

| File | Purpose |
|------|---------|
| `src/lib/mcp/client.ts` | MCP client singleton |
| `src/lib/mcp/hooks.ts` | React hooks for MCP operations |
| `src/components/mcp/McpAppHost.tsx` | Main host wrapper |
| `src/components/mcp/McpAppRenderer.tsx` | Iframe renderer |
| `src/components/chat/McpToolResult.tsx` | Enhanced tool result display |
| `app/api/mcp/sandbox/route.ts` | Sandbox proxy endpoint |
| `app/api/mcp/[...path]/route.ts` | MCP proxy (if needed) |

### 4. Chat Integration

Modify your chat component to detect tool results with `ui://` resources and render `<McpAppRenderer>` instead of plain text.

```tsx
// In chat message rendering
{message.toolResult && message.toolResult._meta?.ui?.resourceUri ? (
  <McpAppRenderer
    resourceUri={message.toolResult._meta.ui.resourceUri}
    toolResult={message.toolResult}
  />
) : (
  <ToolResultText result={message.toolResult} />
)}
```

---

## Recommended Development Order

### Sprint 1: Foundation (Frontend)
1. Set up MCP client with HTTP transport
2. Create basic `<McpAppRenderer>` component
3. Implement sandbox proxy API route
4. Test with simple static HTML app

### Sprint 2: Backend Integration
1. Add `_meta.ui` to one DWSIM tool (e.g., `get_diagnostics`)
2. Create simple HTML template served as resource
3. Test end-to-end flow

### Sprint 3: Rich Visualization
1. Build simulation results dashboard app
2. Add charts with Plotly.js
3. Implement tool-to-app data flow

### Sprint 4: Interactive Features
1. Flowsheet visualizer with clickable units
2. App-to-host tool calls (click unit → get properties)
3. Display mode switching (inline/fullscreen)

---

## Questions to Resolve

1. **Hosting**: Where will app HTML be served from?
   - Option A: DWSIM MCP server serves as resources
   - Option B: CDN/static hosting
   - Option C: Next.js public folder

2. **State Management**: How does app state persist across turns?
   - MCP Apps are ephemeral by default
   - May need to use `ui/update-model-context` for persistence

3. **Authentication**: How does the app authenticate to DWSIM?
   - Inherit session from host
   - Separate API keys
   - Proxy through Next.js

4. **Offline/Caching**: Should apps work offline?
   - Service workers in sandbox
   - Pre-caching common resources

---

## Summary

| Question | Answer |
|----------|--------|
| Develop in which repo? | **Both** - Host in frontend, tools/apps in DWSIM |
| What does frontend need? | MCP client, app-bridge, iframe renderer, sandbox proxy |
| What does DWSIM need? | `_meta.ui` on tools, HTML resource serving |
| First milestone? | Basic app rendering with static HTML |
| Key technology? | `@modelcontextprotocol/ext-apps`, sandboxed iframes |
