# Creating MCP Apps

This guide explains how to add new MCP UI apps to the DWSIM MCP server.

## Required Structure

Each app lives under `apps/templates/{app-name}` and must include:

- `index.html` (entry point)
- `app.json` (metadata + CSP)

Optional shared assets live under `apps/static/`.

Example layout:

```
apps/
  templates/
    my-app/
      index.html
      app.json
  static/
    app-client.js
    theme.css
```

## app.json Template

```json
{
  "name": "my-app",
  "title": "My App",
  "description": "Describe what the app visualizes.",
  "version": "1.0.0",
  "entryPoint": "index.html",
  "csp": {
    "connectDomains": [],
    "resourceDomains": [],
    "frameDomains": []
  }
}
```

Notes:
- `name` must match the directory name and the `ui://dwsim/{name}` URI.
- Add CDN domains to `csp` when loading external scripts (Plotly, etc.).

## App HTML Template

Start from `apps/templates/base.html` or one of the existing apps.

Minimum structure:

```html
<!doctype html>
<html lang="en" data-theme="light">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>My App</title>
    <link rel="stylesheet" href="../../static/theme.css" />
  </head>
  <body>
    <main class="app-shell">
      <header class="app-header">
        <div class="app-title">My App</div>
      </header>
      <section id="app"></section>
    </main>
    <script src="https://unpkg.com/@modelcontextprotocol/ext-apps/dist/index.umd.js"></script>
    <script src="../../static/app-client.js"></script>
  </body>
</html>
```

## MCP Apps SDK Patterns

Use the shared wrapper in `apps/static/app-client.js`:

```js
window.DwsimApp.initialize({
  onToolResult: (result) => {
    const payload = result.structuredContent || result;
    // Render UI
  },
  onHostContextChanged: (changes) => {
    if (changes.theme) {
      window.DwsimApp.applyTheme(changes.theme);
    }
  },
  onInitialized: (context) => {
    if (context?.hostContext?.theme) {
      window.DwsimApp.applyTheme(context.hostContext.theme);
    }
  },
});
```

Apps receive tool output via `ui/notifications/tool-result`.

## Register Tool UI Metadata

To link a tool to an app, add `_meta.ui` metadata:

```python
from dwsim_mcp_server.tools.ui_metadata import add_ui_metadata, get_ui_result_annotation

tool = add_ui_metadata(tool, "ui://dwsim/my-app")

return ToolResult(
    structured_content=payload,
    meta=get_ui_result_annotation("ui://dwsim/my-app"),
)
```

Use the same `ui://dwsim/{app-name}` resource URI as `app.json`.

## Local Testing

Use the test harness:

1. Run `python -m http.server 8080` from `apps/`
2. Open `http://localhost:8080/test-harness/index.html`
3. Load your app and send mock data

## Reference Implementations

- `simulation-results` (status + metrics)
- `stream-properties` (Plotly charts + composition)
- `flowsheet-viewer` (SVG topology)
- `diagnostics` (periodic refresh)
