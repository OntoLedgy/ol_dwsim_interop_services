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

# MCP Tools: Export and Compound Validation

## Overview

These tools extend the MCP server with export and compound validation workflows.

- Export tools: `export_csv`, `export_json`, `generate_report`, `save_case`
- Compound tools: `validate_compounds`, `list_available_compounds`

## Export Tools

### export_csv

Export flowsheet streams and units to a CSV file.

**Parameters**
- `session_id` (string, required): Active session identifier.
- `file_path` (string, required): Destination path ending in `.csv`.
- `object_ids` (string[], optional): Specific object IDs to export; omit to export all.

**Example**
```json
{
  "tool": "export_csv",
  "arguments": {
    "session_id": "session-123",
    "file_path": "C:/exports/flowsheet.csv",
    "object_ids": ["S-1", "U-101"]
  }
}
```

### export_json

Export flowsheet state to JSON.

**Formats**
- `summary`: high-level overview
- `full`: complete data payload

**Parameters**
- `session_id` (string, required): Active session identifier.
- `format` (string, required): `summary` or `full` (case-insensitive).

**Example**
```json
{
  "tool": "export_json",
  "arguments": {
    "session_id": "session-123",
    "format": "summary"
  }
}
```

### generate_report

Generate a Markdown report of the flowsheet.

**Templates**
- `summary`: quick overview
- `detailed`: full analysis

**Parameters**
- `session_id` (string, required): Active session identifier.
- `template` (string, required): `summary` or `detailed` (case-insensitive).
- `file_path` (string, required): Destination path ending in `.md`.

**Example**
```json
{
  "tool": "generate_report",
  "arguments": {
    "session_id": "session-123",
    "template": "detailed",
    "file_path": "C:/exports/flowsheet-report.md"
  }
}
```

### save_case

Save the flowsheet to a DWSIM case file.

**Formats**
- `.dwxmz`: compressed
- `.dwxml`: uncompressed

**Parameters**
- `session_id` (string, required): Active session identifier.
- `file_path` (string, required): Destination path ending in `.dwxmz` or `.dwxml`.

**Example**
```json
{
  "tool": "save_case",
  "arguments": {
    "session_id": "session-123",
    "file_path": "C:/cases/sample.dwxmz"
  }
}
```

## Compound Tools

### validate_compounds

Validate compound names against the DWSIM databank. Returns canonical names, alias resolution, and suggestions.

**Parameters**
- `session_id` (string, required): Active session identifier.
- `compound_names` (string[], required): One or more compound names to validate.

**Example**
```json
{
  "tool": "validate_compounds",
  "arguments": {
    "session_id": "session-123",
    "compound_names": ["CO2", "water", "isobutane", "methne"]
  }
}
```

### list_available_compounds

List compounds available in the DWSIM databank with optional filtering and pagination.

**Parameters**
- `session_id` (string, required): Active session identifier.
- `pattern` (string, optional): Case-insensitive name filter.
- `category` (string, optional): Category filter (e.g., `Hydrocarbon`, `Inorganic`).
- `limit` (number, optional): Max results (1-100). Default `50`.
- `offset` (number, optional): Offset for pagination. Default `0`.

**Example**
```json
{
  "tool": "list_available_compounds",
  "arguments": {
    "session_id": "session-123",
    "pattern": "but",
    "category": "Hydrocarbon",
    "limit": 25,
    "offset": 0
  }
}
```

## Compound Alias Mappings

Alias matching is case-insensitive. The mappings below are common examples resolved by the databank.

| Alias | Canonical name |
| --- | --- |
| CO2 | Carbon dioxide |
| H2O | Water |
| H2S | Hydrogen sulfide |
| N2 | Nitrogen |
| isobutane | Isobutane |
| isopentane | Isopentane |
| nC4 | n-Butane |
| nC5 | n-Pentane |
| nC6 | n-Hexane |

## Troubleshooting

- `VALIDATION_ERROR` with file extension: Ensure `export_csv` uses `.csv`, `generate_report` uses `.md`, and `save_case` uses `.dwxmz` or `.dwxml`.
- `VALIDATION_ERROR` for format/template: Use `summary` or `full` for `export_json`, and `summary` or `detailed` for `generate_report`.
- `No compounds registered`: Add compounds to the session (or load a case) before running compound-dependent operations.
- Invalid file path errors: Confirm the destination path is allowed by server settings and points to a writable location.

