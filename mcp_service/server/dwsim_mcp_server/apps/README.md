<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

This file is part of the OntoLedgy Thermodynamics Architecture and is
dual-licensed:

  1. Open source under the GNU Affero General Public License v3.0 or
     later (AGPL-3.0-or-later). See the LICENSE file in the repository
     root for the full licence text and NOTICE for attribution.
  2. Commercial under a separate proprietary licence offered by
     OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

# MCP Apps Test Harness

This directory contains the shared assets, templates, and a standalone test harness
for developing MCP UI apps without running the MCP server.

## Run the Test Harness

1. Start a local static file server from the `apps` directory:

   - From `mcp_service/server/dwsim_mcp_server/apps`:
     - `python -m http.server 8080`

2. Open the harness in your browser:

   - `http://localhost:8080/test-harness/index.html`

## Load Different Apps

Use the **App** dropdown to select an app template:

- `simulation-results`
- `stream-properties`
- `flowsheet-viewer`
- `diagnostics`

Click **Load App** to refresh the iframe with the selected template.

## Send Mock Tool Results

Use the **Mock Data** dropdown and click **Send Tool Result** to push sample
tool results to the app via the MockAppBridge.

Mock files live in:

- `apps/test-harness/mocks/`

Add new JSON files here to test different scenarios.

## Inject Custom Test Data

1. Create a new JSON file under `apps/test-harness/mocks/`
2. Add an option in `apps/test-harness/index.html` to surface the mock
3. Load the app and click **Send Tool Result**

The mock payload is passed as `structuredContent` in a
`ui/notifications/tool-result` message.

## Theme Testing

Use **Toggle Theme** to send host context changes to the app. Apps should
respond by updating CSS variables and re-rendering charts.

## Troubleshooting

- Blank iframe: Ensure the static server is running from the `apps` directory.
- Missing styles: Check the `../static` paths in the template.
- No updates: Confirm the app listens for `ui/notifications/tool-result`.
- CSP issues in real host: Update `app.json` CSP settings for required domains.
