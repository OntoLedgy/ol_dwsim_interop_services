# MCP Apps Frontend Integration Guide

## Overview

This guide provides everything needed to implement the MCP Host in a Next.js application to render interactive UI apps from the DWSIM MCP Server.

**Target**: Next.js 14+ with App Router
**Effort**: ~2-3 days for core implementation
**Prerequisites**: Basic understanding of MCP protocol, React hooks, iframe communication

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Dependencies](#dependencies)
3. [Directory Structure](#directory-structure)
4. [MCP Client Setup](#mcp-client-setup)
5. [App Bridge Integration](#app-bridge-integration)
6. [Components](#components)
7. [API Routes](#api-routes)
8. [Chat Integration](#chat-integration)
9. [Security Configuration](#security-configuration)
10. [Testing](#testing)
11. [Troubleshooting](#troubleshooting)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│  Next.js Application                                                     │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  Chat Component                                                     │ │
│  │  ┌──────────────────────────────────────────────────────────────┐  │ │
│  │  │  <McpAppHost>                                                 │  │ │
│  │  │  ├── Manages MCP client connection                           │  │ │
│  │  │  ├── Provides app-bridge context                             │  │ │
│  │  │  └── Handles tool calls from apps                            │  │ │
│  │  │                                                               │  │ │
│  │  │  <McpAppRenderer resourceUri="ui://dwsim/simulation-results"> │  │ │
│  │  │  ├── Renders sandboxed iframe                                │  │ │
│  │  │  ├── Manages postMessage communication                       │  │ │
│  │  │  └── Sends tool data to app                                  │  │ │
│  │  └──────────────────────────────────────────────────────────────┘  │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  API Routes                                                         │ │
│  │  ├── /api/mcp/proxy      → Forward MCP requests to DWSIM server    │ │
│  │  └── /api/mcp/sandbox    → Serve app HTML with CSP headers         │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ HTTP (Streamable HTTP Transport)
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  DWSIM MCP Server (http://localhost:8000/mcp)                           │
│  ├── Tools with _meta.ui (run_simulation, get_stream_properties, etc.) │
│  └── UI Resources (ui://dwsim/*)                                        │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Dependencies

### Required Packages

```bash
npm install @modelcontextprotocol/sdk @modelcontextprotocol/ext-apps
```

### Package.json Additions

```json
{
  "dependencies": {
    "@modelcontextprotocol/sdk": "^1.0.0",
    "@modelcontextprotocol/ext-apps": "^1.0.0"
  }
}
```

### TypeScript Types

The packages include TypeScript definitions. Ensure `tsconfig.json` includes:

```json
{
  "compilerOptions": {
    "moduleResolution": "bundler",
    "esModuleInterop": true
  }
}
```

---

## Directory Structure

```
src/
├── lib/
│   └── mcp/
│       ├── client.ts              # MCP client singleton
│       ├── types.ts               # TypeScript interfaces
│       ├── hooks.ts               # React hooks for MCP
│       └── app-bridge.ts          # App bridge wrapper
├── components/
│   └── mcp/
│       ├── McpAppHost.tsx         # Context provider
│       ├── McpAppRenderer.tsx     # Iframe renderer
│       ├── McpToolResult.tsx      # Tool result with optional app
│       └── SandboxFrame.tsx       # Secure iframe wrapper
├── app/
│   └── api/
│       └── mcp/
│           ├── proxy/
│           │   └── route.ts       # MCP proxy endpoint
│           └── sandbox/
│               └── route.ts       # Sandbox proxy endpoint
└── styles/
    └── mcp-app.css                # App container styles
```

---

## MCP Client Setup

### `src/lib/mcp/types.ts`

```typescript
// TypeScript interfaces for MCP Apps

export interface McpClientConfig {
  serverUrl: string;
  clientName: string;
  clientVersion: string;
  timeout?: number;
}

export interface HostContext {
  theme: 'light' | 'dark';
  containerWidth: number | 'flexible';
  containerHeight: number | 'flexible';
  locale: string;
  timezone: string;
  platform: 'web' | 'desktop' | 'mobile';
}

export interface UiResourceMetadata {
  resourceUri: string;
  visibility: ('model' | 'app')[];
  csp?: {
    connectDomains?: string[];
    resourceDomains?: string[];
    frameDomains?: string[];
  };
  permissions?: string[];
  prefersBorder?: boolean;
}

export interface ToolResultWithUi {
  content: Array<{ type: string; text?: string; data?: unknown }>;
  structuredContent?: unknown;
  _meta?: {
    ui?: UiResourceMetadata;
  };
}

export interface McpAppMessage {
  jsonrpc: '2.0';
  id?: string | number;
  method?: string;
  params?: unknown;
  result?: unknown;
  error?: { code: number; message: string; data?: unknown };
}
```

### `src/lib/mcp/client.ts`

```typescript
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StreamableHTTPClientTransport } from '@modelcontextprotocol/sdk/client/streamableHttp.js';
import type { McpClientConfig } from './types';

let clientInstance: Client | null = null;
let transportInstance: StreamableHTTPClientTransport | null = null;

export async function getMcpClient(config: McpClientConfig): Promise<Client> {
  if (clientInstance) {
    return clientInstance;
  }

  const client = new Client({
    name: config.clientName,
    version: config.clientVersion,
    capabilities: {
      // Advertise UI extension support
      extensions: {
        'io.modelcontextprotocol/ui': {
          supportedMimeTypes: ['text/html;profile=mcp-app'],
        },
      },
    },
  });

  const transport = new StreamableHTTPClientTransport(
    new URL(config.serverUrl)
  );

  await client.connect(transport);

  clientInstance = client;
  transportInstance = transport;

  return client;
}

export async function closeMcpClient(): Promise<void> {
  if (clientInstance) {
    await clientInstance.close();
    clientInstance = null;
    transportInstance = null;
  }
}

export function getClientInstance(): Client | null {
  return clientInstance;
}
```

### `src/lib/mcp/hooks.ts`

```typescript
'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import { getMcpClient, closeMcpClient, getClientInstance } from './client';
import type { McpClientConfig, ToolResultWithUi } from './types';

export function useMcpClient(config: McpClientConfig) {
  const [isConnected, setIsConnected] = useState(false);
  const [isConnecting, setIsConnecting] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    let mounted = true;

    async function connect() {
      setIsConnecting(true);
      try {
        await getMcpClient(config);
        if (mounted) {
          setIsConnected(true);
          setError(null);
        }
      } catch (err) {
        if (mounted) {
          setError(err instanceof Error ? err : new Error(String(err)));
          setIsConnected(false);
        }
      } finally {
        if (mounted) {
          setIsConnecting(false);
        }
      }
    }

    connect();

    return () => {
      mounted = false;
    };
  }, [config.serverUrl]);

  const callTool = useCallback(
    async (name: string, args: Record<string, unknown>): Promise<ToolResultWithUi> => {
      const client = getClientInstance();
      if (!client) {
        throw new Error('MCP client not connected');
      }

      const result = await client.callTool({ name, arguments: args });
      return result as ToolResultWithUi;
    },
    []
  );

  const readResource = useCallback(
    async (uri: string): Promise<string> => {
      const client = getClientInstance();
      if (!client) {
        throw new Error('MCP client not connected');
      }

      const result = await client.readResource({ uri });
      const content = result.contents[0];

      if ('text' in content) {
        return content.text;
      }
      throw new Error('Resource did not return text content');
    },
    []
  );

  const listTools = useCallback(async () => {
    const client = getClientInstance();
    if (!client) {
      throw new Error('MCP client not connected');
    }
    return client.listTools();
  }, []);

  return {
    isConnected,
    isConnecting,
    error,
    callTool,
    readResource,
    listTools,
  };
}

export function useMcpTool(
  toolName: string,
  args: Record<string, unknown> | null
) {
  const [result, setResult] = useState<ToolResultWithUi | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const { callTool, isConnected } = useMcpClient({
    serverUrl: process.env.NEXT_PUBLIC_MCP_SERVER_URL!,
    clientName: 'dwsim-platform',
    clientVersion: '1.0.0',
  });

  useEffect(() => {
    if (!isConnected || !args) return;

    let mounted = true;

    async function execute() {
      setIsLoading(true);
      try {
        const toolResult = await callTool(toolName, args);
        if (mounted) {
          setResult(toolResult);
          setError(null);
        }
      } catch (err) {
        if (mounted) {
          setError(err instanceof Error ? err : new Error(String(err)));
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    }

    execute();

    return () => {
      mounted = false;
    };
  }, [toolName, JSON.stringify(args), isConnected]);

  return { result, isLoading, error };
}
```

---

## App Bridge Integration

### `src/lib/mcp/app-bridge.ts`

```typescript
import type { HostContext, McpAppMessage, ToolResultWithUi } from './types';

export interface AppBridgeConfig {
  iframe: HTMLIFrameElement;
  hostContext: HostContext;
  onToolCall: (name: string, args: Record<string, unknown>) => Promise<unknown>;
  onResourceRead: (uri: string) => Promise<string>;
  onMessage?: (message: string) => void;
  onOpenLink?: (url: string) => void;
  onDisplayModeRequest?: (mode: 'inline' | 'fullscreen' | 'pip') => void;
}

export class AppBridge {
  private iframe: HTMLIFrameElement;
  private config: AppBridgeConfig;
  private messageHandler: (event: MessageEvent) => void;
  private pendingRequests: Map<string | number, {
    resolve: (value: unknown) => void;
    reject: (error: Error) => void;
  }> = new Map();
  private requestId = 0;

  constructor(config: AppBridgeConfig) {
    this.iframe = config.iframe;
    this.config = config;

    this.messageHandler = this.handleMessage.bind(this);
    window.addEventListener('message', this.messageHandler);
  }

  private handleMessage(event: MessageEvent) {
    // Verify origin matches iframe
    if (event.source !== this.iframe.contentWindow) {
      return;
    }

    const message = event.data as McpAppMessage;

    if (!message || message.jsonrpc !== '2.0') {
      return;
    }

    // Handle responses to our requests
    if (message.id !== undefined && !message.method) {
      const pending = this.pendingRequests.get(message.id);
      if (pending) {
        this.pendingRequests.delete(message.id);
        if (message.error) {
          pending.reject(new Error(message.error.message));
        } else {
          pending.resolve(message.result);
        }
      }
      return;
    }

    // Handle requests from app
    if (message.method) {
      this.handleAppRequest(message);
    }
  }

  private async handleAppRequest(message: McpAppMessage) {
    const { method, params, id } = message;

    try {
      let result: unknown;

      switch (method) {
        case 'ui/initialize':
          result = {
            protocolVersion: '2026-01-26',
            capabilities: {
              displayModes: ['inline', 'fullscreen'],
              tools: true,
              resources: true,
            },
            hostContext: this.config.hostContext,
          };
          break;

        case 'tools/call':
          const toolParams = params as { name: string; arguments: Record<string, unknown> };
          result = await this.config.onToolCall(toolParams.name, toolParams.arguments);
          break;

        case 'resources/read':
          const resourceParams = params as { uri: string };
          const content = await this.config.onResourceRead(resourceParams.uri);
          result = { contents: [{ uri: resourceParams.uri, text: content }] };
          break;

        case 'ui/message':
          const messageParams = params as { message: string };
          this.config.onMessage?.(messageParams.message);
          result = {};
          break;

        case 'ui/open-link':
          const linkParams = params as { url: string };
          this.config.onOpenLink?.(linkParams.url);
          result = {};
          break;

        case 'ui/request-display-mode':
          const modeParams = params as { mode: 'inline' | 'fullscreen' | 'pip' };
          this.config.onDisplayModeRequest?.(modeParams.mode);
          result = {};
          break;

        default:
          throw new Error(`Unknown method: ${method}`);
      }

      this.sendResponse(id!, result);
    } catch (error) {
      this.sendError(id!, -32000, error instanceof Error ? error.message : String(error));
    }
  }

  private sendResponse(id: string | number, result: unknown) {
    this.postMessage({
      jsonrpc: '2.0',
      id,
      result,
    });
  }

  private sendError(id: string | number, code: number, message: string) {
    this.postMessage({
      jsonrpc: '2.0',
      id,
      error: { code, message },
    });
  }

  private postMessage(message: McpAppMessage) {
    this.iframe.contentWindow?.postMessage(message, '*');
  }

  // Send notifications to app
  sendToolInput(input: unknown) {
    this.postMessage({
      jsonrpc: '2.0',
      method: 'ui/notifications/tool-input',
      params: { input },
    });
  }

  sendToolResult(result: ToolResultWithUi) {
    this.postMessage({
      jsonrpc: '2.0',
      method: 'ui/notifications/tool-result',
      params: {
        content: result.content,
        structuredContent: result.structuredContent,
      },
    });
  }

  sendHostContextChanged(context: Partial<HostContext>) {
    this.postMessage({
      jsonrpc: '2.0',
      method: 'ui/notifications/host-context-changed',
      params: { context },
    });
  }

  sendSizeChanged(width: number, height: number) {
    this.postMessage({
      jsonrpc: '2.0',
      method: 'ui/notifications/size-changed',
      params: { width, height },
    });
  }

  destroy() {
    window.removeEventListener('message', this.messageHandler);
    this.pendingRequests.clear();
  }
}

export function createAppBridge(config: AppBridgeConfig): AppBridge {
  return new AppBridge(config);
}
```

---

## Components

### `src/components/mcp/McpAppHost.tsx`

```tsx
'use client';

import React, { createContext, useContext, useCallback, useMemo } from 'react';
import { useMcpClient } from '@/lib/mcp/hooks';
import type { McpClientConfig, HostContext } from '@/lib/mcp/types';

interface McpAppHostContextValue {
  isConnected: boolean;
  isConnecting: boolean;
  error: Error | null;
  hostContext: HostContext;
  callTool: (name: string, args: Record<string, unknown>) => Promise<unknown>;
  readResource: (uri: string) => Promise<string>;
}

const McpAppHostContext = createContext<McpAppHostContextValue | null>(null);

export function useMcpAppHost() {
  const context = useContext(McpAppHostContext);
  if (!context) {
    throw new Error('useMcpAppHost must be used within McpAppHost');
  }
  return context;
}

interface McpAppHostProps {
  serverUrl: string;
  theme?: 'light' | 'dark';
  locale?: string;
  children: React.ReactNode;
}

export function McpAppHost({
  serverUrl,
  theme = 'light',
  locale = 'en-US',
  children,
}: McpAppHostProps) {
  const config: McpClientConfig = useMemo(
    () => ({
      serverUrl,
      clientName: 'dwsim-platform',
      clientVersion: '1.0.0',
    }),
    [serverUrl]
  );

  const { isConnected, isConnecting, error, callTool, readResource } = useMcpClient(config);

  const hostContext: HostContext = useMemo(
    () => ({
      theme,
      containerWidth: 'flexible',
      containerHeight: 400,
      locale,
      timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
      platform: 'web',
    }),
    [theme, locale]
  );

  const contextValue: McpAppHostContextValue = useMemo(
    () => ({
      isConnected,
      isConnecting,
      error,
      hostContext,
      callTool,
      readResource,
    }),
    [isConnected, isConnecting, error, hostContext, callTool, readResource]
  );

  return (
    <McpAppHostContext.Provider value={contextValue}>
      {children}
    </McpAppHostContext.Provider>
  );
}
```

### `src/components/mcp/McpAppRenderer.tsx`

```tsx
'use client';

import React, { useEffect, useRef, useState, useCallback } from 'react';
import { useMcpAppHost } from './McpAppHost';
import { AppBridge, createAppBridge } from '@/lib/mcp/app-bridge';
import type { ToolResultWithUi, HostContext } from '@/lib/mcp/types';

interface McpAppRendererProps {
  resourceUri: string;
  toolInput?: unknown;
  toolResult?: ToolResultWithUi;
  height?: number | string;
  onMessage?: (message: string) => void;
  onOpenLink?: (url: string) => void;
  className?: string;
}

export function McpAppRenderer({
  resourceUri,
  toolInput,
  toolResult,
  height = 400,
  onMessage,
  onOpenLink,
  className,
}: McpAppRendererProps) {
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const bridgeRef = useRef<AppBridge | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const { hostContext, callTool, readResource, isConnected } = useMcpAppHost();

  // Initialize bridge when iframe loads
  const handleIframeLoad = useCallback(() => {
    if (!iframeRef.current) return;

    // Clean up previous bridge
    bridgeRef.current?.destroy();

    // Create new bridge
    bridgeRef.current = createAppBridge({
      iframe: iframeRef.current,
      hostContext,
      onToolCall: callTool,
      onResourceRead: readResource,
      onMessage,
      onOpenLink: onOpenLink ?? ((url) => window.open(url, '_blank')),
      onDisplayModeRequest: (mode) => {
        console.log('Display mode requested:', mode);
        // Implement fullscreen/pip logic as needed
      },
    });

    setIsLoading(false);

    // Send initial data if available
    if (toolInput) {
      bridgeRef.current.sendToolInput(toolInput);
    }
    if (toolResult) {
      bridgeRef.current.sendToolResult(toolResult);
    }
  }, [hostContext, callTool, readResource, onMessage, onOpenLink, toolInput, toolResult]);

  // Update when tool data changes
  useEffect(() => {
    if (!bridgeRef.current) return;

    if (toolInput) {
      bridgeRef.current.sendToolInput(toolInput);
    }
  }, [toolInput]);

  useEffect(() => {
    if (!bridgeRef.current || !toolResult) return;

    bridgeRef.current.sendToolResult(toolResult);
  }, [toolResult]);

  // Update host context changes
  useEffect(() => {
    if (!bridgeRef.current) return;

    bridgeRef.current.sendHostContextChanged(hostContext);
  }, [hostContext]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      bridgeRef.current?.destroy();
    };
  }, []);

  if (!isConnected) {
    return (
      <div className={`mcp-app-container ${className ?? ''}`}>
        <div className="mcp-app-loading">Connecting to MCP server...</div>
      </div>
    );
  }

  // Construct sandbox URL
  const sandboxUrl = `/api/mcp/sandbox?resource=${encodeURIComponent(resourceUri)}`;

  return (
    <div className={`mcp-app-container ${className ?? ''}`}>
      {isLoading && (
        <div className="mcp-app-loading">Loading app...</div>
      )}
      {loadError && (
        <div className="mcp-app-error">{loadError}</div>
      )}
      <iframe
        ref={iframeRef}
        src={sandboxUrl}
        sandbox="allow-scripts allow-forms"
        onLoad={handleIframeLoad}
        onError={() => setLoadError('Failed to load app')}
        style={{
          width: '100%',
          height: typeof height === 'number' ? `${height}px` : height,
          border: 'none',
          display: isLoading ? 'none' : 'block',
        }}
        title="MCP App"
      />
    </div>
  );
}
```

### `src/components/mcp/McpToolResult.tsx`

```tsx
'use client';

import React from 'react';
import { McpAppRenderer } from './McpAppRenderer';
import type { ToolResultWithUi } from '@/lib/mcp/types';

interface McpToolResultProps {
  result: ToolResultWithUi;
  toolInput?: unknown;
  onMessage?: (message: string) => void;
}

export function McpToolResult({ result, toolInput, onMessage }: McpToolResultProps) {
  const uiMetadata = result._meta?.ui;

  // If result has UI resource, render the app
  if (uiMetadata?.resourceUri) {
    return (
      <McpAppRenderer
        resourceUri={uiMetadata.resourceUri}
        toolInput={toolInput}
        toolResult={result}
        onMessage={onMessage}
      />
    );
  }

  // Otherwise, render text content
  return (
    <div className="mcp-tool-result-text">
      {result.content.map((item, index) => (
        <div key={index}>
          {item.type === 'text' && <p>{item.text}</p>}
          {item.type === 'image' && (
            <img src={`data:image/png;base64,${item.data}`} alt="Result" />
          )}
        </div>
      ))}
    </div>
  );
}
```

---

## API Routes

### `src/app/api/mcp/sandbox/route.ts`

```typescript
import { NextRequest, NextResponse } from 'next/server';

const MCP_SERVER_URL = process.env.MCP_SERVER_URL || 'http://localhost:8000/mcp';

export async function GET(request: NextRequest) {
  const resourceUri = request.nextUrl.searchParams.get('resource');

  if (!resourceUri) {
    return new NextResponse('Missing resource parameter', { status: 400 });
  }

  if (!resourceUri.startsWith('ui://')) {
    return new NextResponse('Invalid resource URI scheme', { status: 400 });
  }

  try {
    // Fetch resource from MCP server
    // In a real implementation, you'd use the MCP client
    const response = await fetch(`${MCP_SERVER_URL}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        jsonrpc: '2.0',
        id: 1,
        method: 'resources/read',
        params: { uri: resourceUri },
      }),
    });

    const result = await response.json();

    if (result.error) {
      return new NextResponse(result.error.message, { status: 500 });
    }

    const html = result.result.contents[0].text;
    const cspMeta = result.result.contents[0]._meta?.ui?.csp;

    // Build CSP header
    const csp = buildCsp(cspMeta);

    return new NextResponse(html, {
      headers: {
        'Content-Type': 'text/html; charset=utf-8',
        'Content-Security-Policy': csp,
        'X-Frame-Options': 'SAMEORIGIN',
        'X-Content-Type-Options': 'nosniff',
      },
    });
  } catch (error) {
    console.error('Failed to fetch MCP resource:', error);
    return new NextResponse('Failed to fetch resource', { status: 500 });
  }
}

function buildCsp(meta?: {
  connectDomains?: string[];
  resourceDomains?: string[];
  frameDomains?: string[];
}): string {
  const directives: string[] = [
    "default-src 'none'",
    "script-src 'self' 'unsafe-inline' https://unpkg.com https://cdn.jsdelivr.net",
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self' data:",
  ];

  // Add connect-src for API calls
  const connectSrc = ["'self'"];
  if (meta?.connectDomains) {
    connectSrc.push(...meta.connectDomains);
  }
  directives.push(`connect-src ${connectSrc.join(' ')}`);

  // Add frame-src if needed
  if (meta?.frameDomains && meta.frameDomains.length > 0) {
    directives.push(`frame-src ${meta.frameDomains.join(' ')}`);
  } else {
    directives.push("frame-src 'none'");
  }

  return directives.join('; ');
}
```

### `src/app/api/mcp/proxy/route.ts`

```typescript
import { NextRequest, NextResponse } from 'next/server';

const MCP_SERVER_URL = process.env.MCP_SERVER_URL || 'http://localhost:8000/mcp';

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();

    const response = await fetch(MCP_SERVER_URL, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        // Forward session ID if present
        ...(request.headers.get('mcp-session-id') && {
          'mcp-session-id': request.headers.get('mcp-session-id')!,
        }),
      },
      body: JSON.stringify(body),
    });

    const result = await response.json();

    return NextResponse.json(result, {
      headers: {
        // Forward session ID from response
        ...(response.headers.get('mcp-session-id') && {
          'mcp-session-id': response.headers.get('mcp-session-id')!,
        }),
      },
    });
  } catch (error) {
    console.error('MCP proxy error:', error);
    return NextResponse.json(
      {
        jsonrpc: '2.0',
        error: { code: -32000, message: 'Proxy error' },
        id: null,
      },
      { status: 500 }
    );
  }
}
```

---

## Chat Integration

### Example: Integrating with Chat Component

```tsx
'use client';

import { McpAppHost } from '@/components/mcp/McpAppHost';
import { McpToolResult } from '@/components/mcp/McpToolResult';
import { useTheme } from 'next-themes';

interface Message {
  role: 'user' | 'assistant' | 'tool';
  content?: string;
  toolCall?: {
    name: string;
    arguments: Record<string, unknown>;
  };
  toolResult?: ToolResultWithUi;
}

export function ChatWithMcp() {
  const { theme } = useTheme();
  const [messages, setMessages] = useState<Message[]>([]);

  const handleAppMessage = (message: string) => {
    // Add message from app to chat
    setMessages((prev) => [
      ...prev,
      { role: 'assistant', content: message },
    ]);
  };

  return (
    <McpAppHost
      serverUrl={process.env.NEXT_PUBLIC_MCP_SERVER_URL!}
      theme={theme === 'dark' ? 'dark' : 'light'}
    >
      <div className="chat-container">
        {messages.map((message, index) => (
          <div key={index} className={`message ${message.role}`}>
            {message.content && <p>{message.content}</p>}

            {message.toolResult && (
              <McpToolResult
                result={message.toolResult}
                toolInput={message.toolCall?.arguments}
                onMessage={handleAppMessage}
              />
            )}
          </div>
        ))}
      </div>
    </McpAppHost>
  );
}
```

---

## Security Configuration

### Environment Variables

```env
# .env.local
NEXT_PUBLIC_MCP_SERVER_URL=http://localhost:8000/mcp
MCP_SERVER_URL=http://localhost:8000/mcp
```

### Iframe Sandbox Attributes

The `sandbox` attribute restricts iframe capabilities:

```html
<!-- Minimal permissions -->
<iframe sandbox="allow-scripts allow-forms">

<!-- DO NOT use allow-same-origin with untrusted content -->
```

### Content Security Policy

Default restrictive CSP:

```
default-src 'none';
script-src 'self' 'unsafe-inline' https://unpkg.com;
style-src 'self' 'unsafe-inline';
img-src 'self' data: blob:;
connect-src 'self';
frame-src 'none';
```

---

## Testing

### Unit Tests

```typescript
// __tests__/lib/mcp/app-bridge.test.ts
import { AppBridge, createAppBridge } from '@/lib/mcp/app-bridge';

describe('AppBridge', () => {
  let mockIframe: HTMLIFrameElement;
  let bridge: AppBridge;

  beforeEach(() => {
    mockIframe = document.createElement('iframe');
    mockIframe.contentWindow = {
      postMessage: jest.fn(),
    } as any;

    bridge = createAppBridge({
      iframe: mockIframe,
      hostContext: {
        theme: 'light',
        containerWidth: 'flexible',
        containerHeight: 400,
        locale: 'en-US',
        timezone: 'UTC',
        platform: 'web',
      },
      onToolCall: jest.fn(),
      onResourceRead: jest.fn(),
    });
  });

  afterEach(() => {
    bridge.destroy();
  });

  it('sends tool input notification', () => {
    bridge.sendToolInput({ test: 'data' });

    expect(mockIframe.contentWindow?.postMessage).toHaveBeenCalledWith(
      expect.objectContaining({
        jsonrpc: '2.0',
        method: 'ui/notifications/tool-input',
        params: { input: { test: 'data' } },
      }),
      '*'
    );
  });
});
```

### Integration Tests

```typescript
// __tests__/components/mcp/McpAppRenderer.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import { McpAppHost } from '@/components/mcp/McpAppHost';
import { McpAppRenderer } from '@/components/mcp/McpAppRenderer';

// Mock fetch for MCP requests
global.fetch = jest.fn();

describe('McpAppRenderer', () => {
  it('renders loading state initially', () => {
    render(
      <McpAppHost serverUrl="http://localhost:8000/mcp">
        <McpAppRenderer resourceUri="ui://dwsim/test" />
      </McpAppHost>
    );

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });
});
```

---

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| "MCP client not connected" | Server not running | Start DWSIM MCP server |
| CORS errors | Missing proxy | Use `/api/mcp/proxy` route |
| Blank iframe | CSP blocking | Check console, adjust CSP |
| postMessage not received | Wrong origin | Verify iframe src |
| App not receiving data | Bridge not initialized | Check `onLoad` handler |

### Debug Logging

Enable verbose logging:

```typescript
// In app-bridge.ts
private postMessage(message: McpAppMessage) {
  console.log('[AppBridge] Sending:', message);
  this.iframe.contentWindow?.postMessage(message, '*');
}

private handleMessage(event: MessageEvent) {
  console.log('[AppBridge] Received:', event.data);
  // ...
}
```

### Testing Locally

1. Start DWSIM MCP server:
   ```bash
   cd mcp_service/server
   dwsim-mcp run --transport streamable-http --port 8000
   ```

2. Start Next.js dev server:
   ```bash
   npm run dev
   ```

3. Open browser console to see MCP communication logs.

---

## Summary Checklist

### Phase 1 Implementation Checklist

- [ ] Install dependencies (`@modelcontextprotocol/sdk`, `@modelcontextprotocol/ext-apps`)
- [ ] Create `src/lib/mcp/` directory with client, types, hooks
- [ ] Create `src/lib/mcp/app-bridge.ts`
- [ ] Create `src/components/mcp/McpAppHost.tsx`
- [ ] Create `src/components/mcp/McpAppRenderer.tsx`
- [ ] Create `src/components/mcp/McpToolResult.tsx`
- [ ] Create `src/app/api/mcp/sandbox/route.ts`
- [ ] Create `src/app/api/mcp/proxy/route.ts`
- [ ] Add environment variables
- [ ] Integrate with chat component
- [ ] Write unit tests
- [ ] Write integration tests
- [ ] Test with DWSIM MCP server

---

## Contact & Support

For questions about:
- **Backend (DWSIM)**: Check `.spec-workflow/specs/mcp-apps-backend/`
- **MCP Protocol**: https://modelcontextprotocol.io
- **ext-apps SDK**: https://github.com/modelcontextprotocol/ext-apps
