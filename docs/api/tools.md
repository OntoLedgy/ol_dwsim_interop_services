<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

# DWSIM MCP Tool Reference

Canonical reference for all tools exposed by the DWSIM MCP server. Every tool accepts and returns JSON over the MCP protocol.

---

## Table of Contents

- [Session Tools](#session-tools)
  - [create_session](#create_session)
  - [close_session](#close_session)
  - [save_case (Session)](#save_case-session)
  - [load_case](#load_case)
- [Compound Tools](#compound-tools)
  - [validate_compounds](#validate_compounds)
  - [list_available_compounds](#list_available_compounds)
- [Flowsheet Tools](#flowsheet-tools)
  - [add_compound](#add_compound)
  - [set_property_package](#set_property_package)
  - [set_binary_interaction_parameter](#set_binary_interaction_parameter)
  - [add_stream](#add_stream)
  - [flash_stream](#flash_stream)
  - [add_unit](#add_unit)
  - [connect](#connect)
  - [set_object_parameter](#set_object_parameter)
  - [delete_object](#delete_object)
  - [list_objects](#list_objects)
  - [get_flowsheet_topology](#get_flowsheet_topology)
  - [get_stream_properties](#get_stream_properties)
- [Simulation Tools](#simulation-tools)
  - [run](#run)
  - [get_status](#get_status)
  - [get_results](#get_results)
- [Analysis Tools](#analysis-tools)
  - [flash_tp](#flash_tp)
  - [flash_ph](#flash_ph)
  - [flash_ps](#flash_ps)
- [Sensitivity Tools](#sensitivity-tools)
  - [sensitivity_analysis](#sensitivity_analysis)
  - [parameter_sweep](#parameter_sweep)
  - [optimize](#optimize)
  - [get_study_status](#get_study_status)
  - [cancel_study](#cancel_study)
  - [export_study_results](#export_study_results)
- [Export Tools](#export-tools)
  - [export_csv](#export_csv)
  - [export_json](#export_json)
  - [generate_report](#generate_report)
  - [save_case (Export)](#save_case-export)
- [Diagnostics Tools](#diagnostics-tools)
  - [get_diagnostics](#get_diagnostics)
- [Workflow Examples](#workflow-examples)
  - [Simple Flash Calculation](#1-simple-flash-calculation)
  - [Distillation Column Setup](#2-distillation-column-setup)
  - [Sensitivity Analysis](#3-sensitivity-analysis)

---

## Session Tools

Tools for creating, closing, saving, and loading DWSIM sessions. A session must be created before calling any other tool.

---

### create_session

**Category:** Session

Create a new DWSIM session for flowsheet work. This MUST be called first before any other DWSIM tools. Returns a `session_id` used in all subsequent calls.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | string | No | Optional session name for identification |
| `timeout` | integer | No | Session timeout in seconds (default: 3600, min: 60, max: 86400) |

**Example request:**

```json
{
  "name": "Flash Calculation",
  "timeout": 7200
}
```

**Example response:**

```json
{
  "session_id": "sess-a1b2c3d4"
}
```

---

### close_session

**Category:** Session

Close an existing DWSIM session and release resources. Always call this when done to free memory.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4"
}
```

**Example response:**

```json
{
  "success": true
}
```

---

### save_case (Session)

**Category:** Session

Save the current flowsheet case to a DWSIM file (.dwxmz).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `file_path` | string | Yes | Destination file path (must end in .dwxmz) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "file_path": "/cases/my_flowsheet.dwxmz"
}
```

**Example response:**

```json
{
  "success": true
}
```

---

### load_case

**Category:** Session

Load a flowsheet case from a DWSIM file (.dwxmz) into a session.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `file_path` | string | Yes | Source file path (must end in .dwxmz) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "file_path": "/cases/my_flowsheet.dwxmz"
}
```

**Example response:**

```json
{
  "session_id": "sess-a1b2c3d4"
}
```

---

## Compound Tools

Tools for discovering and validating compound names against the DWSIM databank before adding them to a session.

---

### validate_compounds

**Category:** Compound

Validate compound names against the DWSIM databank before adding them. Returns canonical names, alias resolution (CO2, H2O, isobutane), and fuzzy-matched suggestions for typos (e.g., "methne" suggests Methane).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `compound_names` | array of string | Yes | Compound names to validate (min 1 item) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "compound_names": ["CO2", "methne", "Water"]
}
```

**Example response:**

```json
{
  "results": [
    {
      "input_name": "CO2",
      "valid": true,
      "canonical_name": "Carbon Dioxide",
      "alias_used": true,
      "suggestions": []
    },
    {
      "input_name": "methne",
      "valid": false,
      "canonical_name": null,
      "alias_used": false,
      "suggestions": ["Methane"]
    },
    {
      "input_name": "Water",
      "valid": true,
      "canonical_name": "Water",
      "alias_used": false,
      "suggestions": []
    }
  ]
}
```

---

### list_available_compounds

**Category:** Compound

List compounds available in the DWSIM databank to discover valid names. Use `pattern` for case-insensitive search (e.g., "butan") and `category` for type filtering (e.g., Hydrocarbon). Supports pagination with `limit` and `offset`.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `pattern` | string | No | Case-insensitive name pattern filter |
| `category` | string | No | Category filter (e.g., "Hydrocarbon") |
| `limit` | integer | No | Maximum results to return (default: 50, range: 1-100) |
| `offset` | integer | No | Offset for pagination (default: 0, min: 0) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "pattern": "butan",
  "limit": 5
}
```

**Example response:**

```json
{
  "compounds": [
    {
      "name": "n-Butane",
      "category": "Hydrocarbon",
      "aliases": ["Butane"],
      "formula": "C4H10"
    },
    {
      "name": "Isobutane",
      "category": "Hydrocarbon",
      "aliases": ["i-Butane", "2-Methylpropane"],
      "formula": "C4H10"
    }
  ],
  "total_count": 2,
  "has_more": false
}
```

---

## Flowsheet Tools

Tools for building and inspecting process flowsheets: adding compounds, setting thermodynamic packages, creating streams and unit operations, connecting them, and querying state.

---

### add_compound

**Category:** Flowsheet

Add a compound from the DWSIM databank to the session (idempotent). Supports aliases like CO2, H2O, and isobutane. Common inputs include Methane, n-Butane, Water, and Carbon Dioxide.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `compound_name` | string | Yes | Compound name in the DWSIM databank |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "compound_name": "Methane"
}
```

**Example response:**

```json
{
  "compound_name": "Methane",
  "added": true
}
```

---

### set_property_package

**Category:** Flowsheet

Set a thermodynamic property package for the session. Supported packages (case-insensitive): `peng-robinson`, `srk`, `nrtl`, `psrk`, `unifac`.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `package_name` | string | Yes | Property package name (see supported list) |
| `options` | object | No | Optional key-value package options (default: {}) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "package_name": "Peng-Robinson"
}
```

**Example response:**

```json
{
  "package_name": "peng-robinson",
  "applied": true
}
```

---

### set_binary_interaction_parameter

**Category:** Flowsheet

Set a binary interaction parameter (BIP) for a pair of compounds. Critical for accurate phase equilibrium. Typical values: hydrocarbon pairs ~0.01-0.05, water-hydrocarbon ~0.5. Set AFTER property package, BEFORE adding streams.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `compound_a` | string | Yes | First compound name |
| `compound_b` | string | Yes | Second compound name |
| `interaction_value` | number | Yes | Binary interaction parameter value (must be finite) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "compound_a": "Methane",
  "compound_b": "n-Butane",
  "interaction_value": 0.02
}
```

**Example response:**

```json
{
  "compound1": "Methane",
  "compound2": "n-Butane",
  "value": 0.02,
  "applied": true
}
```

---

### add_stream

**Category:** Flowsheet

Create a material stream. For FEED streams: set `is_source=true` with temperature (K), pressure (Pa), molar_flow (mol/s), and composition. For OUTLET streams: set `is_source=false` and omit composition to auto-fill equal fractions (DWSIM computes real values). Always flash feed streams after creation.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `stream_name` | string | Yes | Stream name |
| `stream_type` | string | Yes | Stream type identifier |
| `is_source` | boolean | Yes | True for feed (known conditions), False for outlet (calculated) |
| `temperature` | number | No | Temperature in K (must be > 0; required for source streams) |
| `pressure` | number | No | Pressure in Pa (must be > 0; required for source streams) |
| `molar_flow` | number | No | Molar flow in mol/s (must be > 0; required for source streams if mass_flow not set) |
| `composition` | object | No | Component mole fractions `{"compound": fraction}`. Fractions must be in [0,1] and sum to <= 1.0 |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "stream_name": "Feed",
  "stream_type": "material",
  "is_source": true,
  "temperature": 300.0,
  "pressure": 101325.0,
  "molar_flow": 100.0,
  "composition": {
    "Methane": 0.6,
    "n-Butane": 0.4
  }
}
```

**Example response:**

```json
{
  "stream_id": "stream-feed",
  "name": "Feed"
}
```

---

### flash_stream

**Category:** Flowsheet

Perform flash calculation on a feed stream to compute phase equilibrium. MUST be called on feed streams (`is_source=true`) after creation and before running simulation.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `stream_id` | string | Yes | Stream identifier to flash |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "stream_id": "stream-feed"
}
```

**Example response:**

```json
{
  "stream_id": "stream-feed",
  "flashed": true
}
```

---

### add_unit

**Category:** Flowsheet

Create a unit operation. Supported types (case-insensitive): `separator`, `mixer`, `heater`, `pump`, `valve`. Parameters vary by type. Separator example parameters: `CalculationMode`, `PressureCalculation`, `DimensionRatio`, `ResidenceTime`.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `unit_name` | string | Yes | Unit name |
| `unit_type` | string | Yes | Unit operation type (see supported list) |
| `parameters` | object | No | Unit parameters (key-value, default: {}) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "unit_name": "sep-01",
  "unit_type": "separator",
  "parameters": {
    "CalculationMode": "Legacy",
    "PressureCalculation": "Average",
    "DimensionRatio": 3.0,
    "ResidenceTime": 5.0
  }
}
```

**Example response:**

```json
{
  "unit_id": "unit-sep-01",
  "name": "sep-01",
  "unit_type": "separator"
}
```

---

### connect

**Category:** Flowsheet

Connect a stream to a unit operation port. For separator: `port_name` can be `Inlet`, `VaporOutlet`, `LiquidOutlet1`, or `LiquidOutlet2`. `source_id` is the stream ID, `target_id` is the unit ID.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `source_id` | string | Yes | Source object identifier (stream ID) |
| `target_id` | string | Yes | Target object identifier (unit ID) |
| `port_name` | string | Yes | Port name on the target object |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "source_id": "stream-feed",
  "target_id": "unit-sep-01",
  "port_name": "Inlet"
}
```

**Example response:**

```json
{
  "source_id": "stream-feed",
  "target_id": "unit-sep-01",
  "port_name": "Inlet",
  "connected": true
}
```

---

### set_object_parameter

**Category:** Flowsheet

Update a parameter on a flowsheet object (stream or unit). The value must be a number, string, bool, or null.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `object_id` | string | Yes | Target object identifier |
| `parameter_name` | string | Yes | Parameter name |
| `parameter_value` | any | Yes | Value to set (number, string, bool, or null) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "object_id": "unit-sep-01",
  "parameter_name": "DimensionRatio",
  "parameter_value": 4.0
}
```

**Example response:**

```json
{
  "object_id": "unit-sep-01",
  "parameter_name": "DimensionRatio",
  "value": 4.0,
  "previous_value": 3.0
}
```

---

### delete_object

**Category:** Flowsheet

Delete a flowsheet object and orphaned connections safely.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `object_id` | string | Yes | Object identifier to delete |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "object_id": "stream-vapor"
}
```

**Example response:**

```json
{
  "object_id": "stream-vapor",
  "deleted": true,
  "removed_connections": [
    {
      "source_id": "stream-vapor",
      "target_id": "unit-sep-01",
      "port_name": "VaporOutlet"
    }
  ]
}
```

---

### list_objects

**Category:** Flowsheet

List all streams, units, and connections in the current session. Use to verify flowsheet topology before running simulation.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4"
}
```

**Example response:**

```json
{
  "streams": [
    { "id": "stream-feed", "name": "Feed" },
    { "id": "stream-vapor", "name": "VaporOut" }
  ],
  "units": [
    { "id": "unit-sep-01", "name": "sep-01", "unit_type": "separator" }
  ],
  "connections": [
    { "source_id": "stream-feed", "target_id": "unit-sep-01", "port_name": "Inlet" },
    { "source_id": "stream-vapor", "target_id": "unit-sep-01", "port_name": "VaporOutlet" }
  ]
}
```

---

### get_flowsheet_topology

**Category:** Flowsheet

Retrieve flowsheet topology (streams, units, connections) for visualization. Returns the same data as `list_objects` with UI metadata annotations.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4"
}
```

**Example response:**

```json
{
  "streams": [
    { "id": "stream-feed", "name": "Feed" }
  ],
  "units": [
    { "id": "unit-sep-01", "name": "sep-01", "unit_type": "separator" }
  ],
  "connections": [
    { "source_id": "stream-feed", "target_id": "unit-sep-01", "port_name": "Inlet" }
  ]
}
```

---

### get_stream_properties

**Category:** Flowsheet

Retrieve detailed properties for a specific stream from the latest simulation results, including phase compositions and thermodynamic properties.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `stream_id` | string | Yes | Stream identifier |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "stream_id": "stream-feed"
}
```

**Example response:**

```json
{
  "stream_id": "stream-feed",
  "name": "Feed",
  "temperature": 300.0,
  "pressure": 101325.0,
  "vapor_fraction": 0.65,
  "phases": {
    "vapor": { "fraction": 0.65, "composition": { "Methane": 0.85, "n-Butane": 0.15 } },
    "liquid": { "fraction": 0.35, "composition": { "Methane": 0.14, "n-Butane": 0.86 } }
  }
}
```

---

## Simulation Tools

Tools for executing the DWSIM solver and retrieving results.

---

### run

**Category:** Simulation

Execute the DWSIM solver for a session. Prerequisites: (1) all compounds added, (2) property package set, (3) BIPs configured, (4) feed stream created and flashed, (5) outlet streams created, (6) unit operation added, (7) all streams connected. Returns convergence status, stream properties, and mass balance diagnostics.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `timeout_seconds` | integer | No | Maximum calculation time in seconds (default: 120, min: 1) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "timeout_seconds": 60
}
```

**Example response:**

```json
{
  "status": "completed",
  "convergence_state": "converged",
  "stream_results": [
    {
      "stream_id": "stream-feed",
      "name": "Feed",
      "temperature": 300.0,
      "pressure": 101325.0,
      "vapor_fraction": 0.65
    },
    {
      "stream_id": "stream-vapor",
      "name": "VaporOut",
      "temperature": 300.0,
      "pressure": 101325.0,
      "vapor_fraction": 1.0
    }
  ],
  "mass_balance_error": 1.2e-10
}
```

---

### get_status

**Category:** Simulation

Retrieve the latest simulation status for a session: idle, running, converged, or failed.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4"
}
```

**Example response:**

```json
{
  "status": "converged",
  "is_running": false
}
```

---

### get_results

**Category:** Simulation

Retrieve detailed simulation results including all stream properties, phase compositions, thermodynamic properties (enthalpy, entropy, density, viscosity), and mass balance error. Optionally filter to a single object.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `object_id` | string | No | Optional object identifier for targeted results |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "object_id": "stream-vapor"
}
```

**Example response:**

```json
{
  "status": "completed",
  "convergence_state": "converged",
  "stream_results": [
    {
      "stream_id": "stream-vapor",
      "name": "VaporOut",
      "temperature": 300.0,
      "pressure": 101325.0,
      "vapor_fraction": 1.0,
      "enthalpy": -42500.0,
      "entropy": 185.3,
      "density": 1.18
    }
  ],
  "mass_balance_error": 1.2e-10
}
```

---

## Analysis Tools

Tools for standalone thermodynamic flash calculations on arbitrary mixtures, independent of the flowsheet.

---

### flash_tp

**Category:** Analysis

Perform a temperature-pressure flash calculation for a mixture. Requires a session with a property package configured. Provide compound names and mole fractions (must sum to 1.0), plus temperature and pressure values with units.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session with configured property package |
| `temperature` | number | Yes | Temperature value |
| `pressure` | number | Yes | Pressure value |
| `compounds` | object | Yes | Map of compound name to mole fraction, e.g. `{"Methane": 0.6, "n-Butane": 0.4}`. Must sum to 1.0 |
| `units` | object | Yes | Unit labels: `{"temperature": "K", "pressure": "Pa"}` |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "temperature": 300.0,
  "pressure": 101325.0,
  "compounds": {
    "Methane": 0.6,
    "n-Butane": 0.4
  },
  "units": {
    "temperature": "K",
    "pressure": "Pa"
  }
}
```

**Example response:**

```json
{
  "temperature": 300.0,
  "pressure": 101325.0,
  "vapor_fraction": 0.65,
  "phases": {
    "vapor": { "fraction": 0.65, "composition": { "Methane": 0.85, "n-Butane": 0.15 } },
    "liquid": { "fraction": 0.35, "composition": { "Methane": 0.14, "n-Butane": 0.86 } }
  }
}
```

---

### flash_ph

**Category:** Analysis

Perform a pressure-enthalpy flash calculation for a mixture. Requires a session with a property package configured. Provide compound names and mole fractions (must sum to 1.0), plus pressure and molar enthalpy values with units.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session with configured property package |
| `pressure` | number | Yes | Pressure value |
| `enthalpy` | number | Yes | Molar enthalpy value |
| `compounds` | object | Yes | Map of compound name to mole fraction. Must sum to 1.0 |
| `units` | object | Yes | Unit labels: `{"pressure": "Pa", "enthalpy": "J/mol"}` |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "pressure": 101325.0,
  "enthalpy": -42500.0,
  "compounds": {
    "Methane": 0.6,
    "n-Butane": 0.4
  },
  "units": {
    "pressure": "Pa",
    "enthalpy": "J/mol"
  }
}
```

**Example response:**

```json
{
  "temperature": 298.5,
  "pressure": 101325.0,
  "vapor_fraction": 0.62,
  "phases": {
    "vapor": { "fraction": 0.62, "composition": { "Methane": 0.84, "n-Butane": 0.16 } },
    "liquid": { "fraction": 0.38, "composition": { "Methane": 0.15, "n-Butane": 0.85 } }
  }
}
```

---

### flash_ps

**Category:** Analysis

Perform a pressure-entropy flash calculation for a mixture. Requires a session with a property package configured. Provide compound names and mole fractions (must sum to 1.0), plus pressure and molar entropy values with units.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session with configured property package |
| `pressure` | number | Yes | Pressure value |
| `entropy` | number | Yes | Molar entropy value |
| `compounds` | object | Yes | Map of compound name to mole fraction. Must sum to 1.0 |
| `units` | object | Yes | Unit labels: `{"pressure": "Pa", "entropy": "J/mol/K"}` |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "pressure": 101325.0,
  "entropy": 185.0,
  "compounds": {
    "Methane": 0.6,
    "n-Butane": 0.4
  },
  "units": {
    "pressure": "Pa",
    "entropy": "J/mol/K"
  }
}
```

**Example response:**

```json
{
  "temperature": 301.2,
  "pressure": 101325.0,
  "vapor_fraction": 0.67,
  "phases": {
    "vapor": { "fraction": 0.67, "composition": { "Methane": 0.86, "n-Butane": 0.14 } },
    "liquid": { "fraction": 0.33, "composition": { "Methane": 0.13, "n-Butane": 0.87 } }
  }
}
```

---

## Sensitivity Tools

Tools for parametric studies, multi-variable sweeps, and optimization.

---

### sensitivity_analysis

**Category:** Sensitivity

Run a single-variable sensitivity study by sweeping one parameter over a range and collecting requested outputs at each step. Use this to understand how a specific variable impacts key results.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `variable` | object | Yes | Variable to vary: `{"object_id": "...", "property_name": "..."}` |
| `range` | object | Yes | Range for the sweep: `{"min_value": ..., "max_value": ...}` (must be finite, min < max) |
| `steps` | integer | Yes | Number of evaluation steps (range: 2-100) |
| `outputs` | array of object | Yes | Outputs to collect: `[{"object_id": "...", "property_name": "..."}]` (min 1) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "variable": {
    "object_id": "stream-feed",
    "property_name": "Temperature"
  },
  "range": {
    "min_value": 280.0,
    "max_value": 350.0
  },
  "steps": 15,
  "outputs": [
    { "object_id": "stream-vapor", "property_name": "MolarFlow" },
    { "object_id": "stream-liquid", "property_name": "MolarFlow" }
  ]
}
```

**Example response:**

```json
{
  "study_id": "study-abc123",
  "status": "completed",
  "steps_completed": 15,
  "results": [
    {
      "input_value": 280.0,
      "outputs": { "stream-vapor.MolarFlow": 55.2, "stream-liquid.MolarFlow": 44.8 }
    },
    {
      "input_value": 285.0,
      "outputs": { "stream-vapor.MolarFlow": 58.1, "stream-liquid.MolarFlow": 41.9 }
    }
  ]
}
```

---

### parameter_sweep

**Category:** Sensitivity

Run a multi-variable parameter sweep by evaluating combinations across multiple ranges. Use this to explore interactions between variables and their combined impact on outputs.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `variables` | array of object | Yes | Variables to sweep. Each entry: `{"object_id": "...", "property_name": "...", "range": {"min_value": ..., "max_value": ...}, "steps": ...}` (min 1) |
| `outputs` | array of object | Yes | Outputs to collect: `[{"object_id": "...", "property_name": "..."}]` (min 1) |
| `max_combinations` | integer | No | Maximum total combinations (default: 1000, min: 1) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "variables": [
    {
      "object_id": "stream-feed",
      "property_name": "Temperature",
      "range": { "min_value": 280.0, "max_value": 350.0 },
      "steps": 10
    },
    {
      "object_id": "stream-feed",
      "property_name": "Pressure",
      "range": { "min_value": 50000.0, "max_value": 200000.0 },
      "steps": 5
    }
  ],
  "outputs": [
    { "object_id": "stream-vapor", "property_name": "VaporFraction" }
  ],
  "max_combinations": 500
}
```

**Example response:**

```json
{
  "study_id": "study-sweep-456",
  "status": "completed",
  "total_combinations": 50,
  "results": [
    {
      "inputs": { "stream-feed.Temperature": 280.0, "stream-feed.Pressure": 50000.0 },
      "outputs": { "stream-vapor.VaporFraction": 0.82 }
    }
  ]
}
```

---

### optimize

**Category:** Sensitivity

Run an optimization to find variable values that minimize or maximize an objective. Provide bounds and optional constraints to guide the solver.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `objective` | object | Yes | Objective: `{"object_id": "...", "property_name": "...", "direction": "minimize"|"maximize"}` |
| `variables` | array of object | Yes | Optimization variables: `[{"object_id": "...", "property_name": "...", "lower": ..., "upper": ..., "initial": ...}]` (min 1). `initial` is optional. |
| `constraints` | array of object | No | Optional constraints: `[{"object_id": "...", "property_name": "...", "operator": "<="|">="|"==", "threshold": ...}]` |
| `max_iterations` | integer | No | Maximum iterations (default: 100, min: 1) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "objective": {
    "object_id": "stream-vapor",
    "property_name": "MolarFlow",
    "direction": "maximize"
  },
  "variables": [
    {
      "object_id": "stream-feed",
      "property_name": "Temperature",
      "lower": 280.0,
      "upper": 400.0,
      "initial": 320.0
    }
  ],
  "constraints": [
    {
      "object_id": "stream-liquid",
      "property_name": "MolarFlow",
      "operator": ">=",
      "threshold": 10.0
    }
  ],
  "max_iterations": 50
}
```

**Example response:**

```json
{
  "study_id": "study-opt-789",
  "status": "completed",
  "objective_value": 87.5,
  "optimal_values": {
    "stream-feed.Temperature": 365.2
  },
  "iterations": 34,
  "converged": true
}
```

---

### get_study_status

**Category:** Sensitivity

Check progress for a running sensitivity or sweep study by `study_id`. Returns completion counts and estimated remaining time.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `study_id` | string | Yes | Study identifier |

**Example request:**

```json
{
  "study_id": "study-abc123"
}
```

**Example response:**

```json
{
  "study_id": "study-abc123",
  "status": "running",
  "steps_completed": 8,
  "steps_total": 15,
  "estimated_remaining_seconds": 42
}
```

---

### cancel_study

**Category:** Sensitivity

Cancel a running study by `study_id`. Returns partial results collected so far and marks the study as cancelled.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `study_id` | string | Yes | Study identifier |

**Example request:**

```json
{
  "study_id": "study-abc123"
}
```

**Example response:**

```json
{
  "study_id": "study-abc123",
  "status": "cancelled",
  "steps_completed": 8,
  "partial_results": []
}
```

---

### export_study_results

**Category:** Sensitivity

Export completed or partial study results to a file path (CSV or JSON). Use this to persist results for external analysis.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `study_id` | string | Yes | Study identifier |
| `file_path` | string | Yes | Destination file path (CSV or JSON) |

**Example request:**

```json
{
  "study_id": "study-abc123",
  "file_path": "/results/sensitivity_study.csv"
}
```

**Example response:**

```json
{
  "study_id": "study-abc123",
  "file_path": "/results/sensitivity_study.csv",
  "status": "success"
}
```

---

## Export Tools

Tools for exporting flowsheet data to CSV, JSON, Markdown reports, and DWSIM case files.

---

### export_csv

**Category:** Export

Export flowsheet streams/units to a CSV for spreadsheets or debugging. Provide `file_path` ending in `.csv` and optionally `object_ids` to filter (e.g., `["Feed", "Separator"]`). Returns the saved path and row count.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `file_path` | string | Yes | Destination CSV file path (must end in .csv) |
| `object_ids` | array of string | No | Optional object identifiers to export; omit to export all |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "file_path": "/exports/flowsheet.csv",
  "object_ids": ["stream-feed", "stream-vapor"]
}
```

**Example response:**

```json
{
  "success": true,
  "file_path": "/exports/flowsheet.csv",
  "row_count": 24
}
```

---

### export_json

**Category:** Export

Export flowsheet state to JSON for programmatic inspection or caching. Use `format="summary"` for a top-level overview or `format="full"` for complete data.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `format` | string | No | Export format: `"summary"` or `"full"` (default: `"summary"`) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "format": "full"
}
```

**Example response:**

```json
{
  "data": {
    "session_id": "sess-a1b2c3d4",
    "compounds": ["Methane", "n-Butane"],
    "property_package": "peng-robinson",
    "streams": [],
    "units": [],
    "connections": []
  }
}
```

---

### generate_report

**Category:** Export

Generate a Markdown report file for sharing or QA notes. Provide `file_path` ending in `.md` and choose `template="summary"` or `template="detailed"`.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `file_path` | string | Yes | Destination Markdown file path (must end in .md) |
| `template` | string | No | Report template: `"summary"` or `"detailed"` (default: `"summary"`) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "file_path": "/reports/simulation_report.md",
  "template": "detailed"
}
```

**Example response:**

```json
{
  "success": true,
  "file_path": "/reports/simulation_report.md"
}
```

---

### save_case (Export)

**Category:** Export

Save the flowsheet to a DWSIM case file for later reload. Provide `file_path` ending in `.dwxmz` (compressed) or `.dwxml` (uncompressed).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | Yes | Session identifier |
| `file_path` | string | Yes | Destination case file path (must end in .dwxmz or .dwxml) |

**Example request:**

```json
{
  "session_id": "sess-a1b2c3d4",
  "file_path": "/cases/flowsheet_v2.dwxmz"
}
```

**Example response:**

```json
{
  "success": true,
  "file_path": "/cases/flowsheet_v2.dwxmz"
}
```

---

## Diagnostics Tools

Tools for inspecting server and session health.

---

### get_diagnostics

**Category:** Diagnostics

Retrieve diagnostics for the server or a specific session. Provide `session_id` to get session diagnostics; omit to get server-level diagnostics.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `session_id` | string | No | Session identifier. If omitted, returns server diagnostics. |

**Example request (server):**

```json
{}
```

**Example response (server):**

```json
{
  "active_sessions": 2,
  "uptime_seconds": 3600,
  "memory_usage_mb": 512.4
}
```

**Example request (session):**

```json
{
  "session_id": "sess-a1b2c3d4"
}
```

**Example response (session):**

```json
{
  "session_id": "sess-a1b2c3d4",
  "compounds": ["Methane", "n-Butane"],
  "property_package": "peng-robinson",
  "stream_count": 3,
  "unit_count": 1,
  "last_simulation_status": "converged"
}
```

---

## Workflow Examples

### 1. Simple Flash Calculation

Compute vapor-liquid equilibrium for a methane/butane mixture at 300 K and 1 atm.

**Step 1 -- Create session:**

```json
// Tool: create_session
{ "name": "Flash Example" }
// Response: { "session_id": "sess-flash-01" }
```

**Step 2 -- Add compounds:**

```json
// Tool: add_compound
{ "session_id": "sess-flash-01", "compound_name": "Methane" }
// Response: { "compound_name": "Methane", "added": true }

// Tool: add_compound
{ "session_id": "sess-flash-01", "compound_name": "n-Butane" }
// Response: { "compound_name": "n-Butane", "added": true }
```

**Step 3 -- Set property package:**

```json
// Tool: set_property_package
{ "session_id": "sess-flash-01", "package_name": "Peng-Robinson" }
// Response: { "package_name": "peng-robinson", "applied": true }
```

**Step 4 -- Run TP flash:**

```json
// Tool: flash_tp
{
  "session_id": "sess-flash-01",
  "temperature": 300.0,
  "pressure": 101325.0,
  "compounds": { "Methane": 0.6, "n-Butane": 0.4 },
  "units": { "temperature": "K", "pressure": "Pa" }
}
// Response:
// {
//   "temperature": 300.0,
//   "pressure": 101325.0,
//   "vapor_fraction": 0.65,
//   "phases": {
//     "vapor": { "fraction": 0.65, "composition": { "Methane": 0.85, "n-Butane": 0.15 } },
//     "liquid": { "fraction": 0.35, "composition": { "Methane": 0.14, "n-Butane": 0.86 } }
//   }
// }
```

**Step 5 -- Close session:**

```json
// Tool: close_session
{ "session_id": "sess-flash-01" }
// Response: { "success": true }
```

---

### 2. Distillation Column Setup

Build a three-phase separator flowsheet: feed stream splits into vapor, liquid, and second liquid outlets.

**Step 1 -- Create session:**

```json
// Tool: create_session
{ "name": "Separator Flowsheet" }
// Response: { "session_id": "sess-sep-01" }
```

**Step 2 -- Add compounds:**

```json
// Tool: add_compound
{ "session_id": "sess-sep-01", "compound_name": "Methane" }

// Tool: add_compound
{ "session_id": "sess-sep-01", "compound_name": "n-Butane" }

// Tool: add_compound
{ "session_id": "sess-sep-01", "compound_name": "Water" }
```

**Step 3 -- Set property package and BIPs:**

```json
// Tool: set_property_package
{ "session_id": "sess-sep-01", "package_name": "Peng-Robinson" }

// Tool: set_binary_interaction_parameter
{ "session_id": "sess-sep-01", "compound_a": "Methane", "compound_b": "Water", "interaction_value": 0.5 }

// Tool: set_binary_interaction_parameter
{ "session_id": "sess-sep-01", "compound_a": "n-Butane", "compound_b": "Water", "interaction_value": 0.48 }
```

**Step 4 -- Create feed stream and flash:**

```json
// Tool: add_stream
{
  "session_id": "sess-sep-01",
  "stream_name": "Feed",
  "stream_type": "material",
  "is_source": true,
  "temperature": 320.0,
  "pressure": 500000.0,
  "molar_flow": 100.0,
  "composition": { "Methane": 0.5, "n-Butane": 0.3, "Water": 0.2 }
}
// Response: { "stream_id": "stream-feed", "name": "Feed" }

// Tool: flash_stream
{ "session_id": "sess-sep-01", "stream_id": "stream-feed" }
// Response: { "stream_id": "stream-feed", "flashed": true }
```

**Step 5 -- Create outlet streams:**

```json
// Tool: add_stream
{ "session_id": "sess-sep-01", "stream_name": "VaporOut", "stream_type": "material", "is_source": false }

// Tool: add_stream
{ "session_id": "sess-sep-01", "stream_name": "LiquidOut1", "stream_type": "material", "is_source": false }

// Tool: add_stream
{ "session_id": "sess-sep-01", "stream_name": "LiquidOut2", "stream_type": "material", "is_source": false }
```

**Step 6 -- Add separator unit:**

```json
// Tool: add_unit
{
  "session_id": "sess-sep-01",
  "unit_name": "sep-01",
  "unit_type": "separator",
  "parameters": { "CalculationMode": "Legacy", "PressureCalculation": "Average" }
}
// Response: { "unit_id": "unit-sep-01", "name": "sep-01", "unit_type": "separator" }
```

**Step 7 -- Connect streams to unit:**

```json
// Tool: connect
{ "session_id": "sess-sep-01", "source_id": "stream-feed", "target_id": "unit-sep-01", "port_name": "Inlet" }

// Tool: connect
{ "session_id": "sess-sep-01", "source_id": "stream-vapor-out", "target_id": "unit-sep-01", "port_name": "VaporOutlet" }

// Tool: connect
{ "session_id": "sess-sep-01", "source_id": "stream-liquid-out-1", "target_id": "unit-sep-01", "port_name": "LiquidOutlet1" }

// Tool: connect
{ "session_id": "sess-sep-01", "source_id": "stream-liquid-out-2", "target_id": "unit-sep-01", "port_name": "LiquidOutlet2" }
```

**Step 8 -- Run simulation and get results:**

```json
// Tool: run
{ "session_id": "sess-sep-01", "timeout_seconds": 120 }
// Response: { "status": "completed", "convergence_state": "converged", ... }

// Tool: get_results
{ "session_id": "sess-sep-01" }
// Response: full stream results with phase compositions and mass balance
```

**Step 9 -- Close session:**

```json
// Tool: close_session
{ "session_id": "sess-sep-01" }
```

---

### 3. Sensitivity Analysis

Study how feed temperature affects vapor recovery from a separator.

**Step 1 -- Create session and build flowsheet:**

Follow Steps 1-7 from the Distillation Column Setup example above to create a fully connected separator flowsheet with session `sess-sens-01`.

**Step 2 -- Run baseline simulation:**

```json
// Tool: run
{ "session_id": "sess-sens-01" }
// Response: { "status": "completed", "convergence_state": "converged", ... }
```

**Step 3 -- Run sensitivity analysis:**

```json
// Tool: sensitivity_analysis
{
  "session_id": "sess-sens-01",
  "variable": {
    "object_id": "stream-feed",
    "property_name": "Temperature"
  },
  "range": {
    "min_value": 280.0,
    "max_value": 400.0
  },
  "steps": 20,
  "outputs": [
    { "object_id": "stream-vapor-out", "property_name": "MolarFlow" },
    { "object_id": "stream-liquid-out-1", "property_name": "MolarFlow" }
  ]
}
// Response:
// {
//   "study_id": "study-temp-sweep",
//   "status": "completed",
//   "steps_completed": 20,
//   "results": [ ... ]
// }
```

**Step 4 -- Export results for analysis:**

```json
// Tool: export_study_results
{ "study_id": "study-temp-sweep", "file_path": "/results/temp_sensitivity.csv" }
// Response: { "study_id": "study-temp-sweep", "file_path": "/results/temp_sensitivity.csv", "status": "success" }
```

**Step 5 -- Generate report and close:**

```json
// Tool: generate_report
{ "session_id": "sess-sens-01", "file_path": "/reports/sensitivity_report.md", "template": "detailed" }
// Response: { "success": true, "file_path": "/reports/sensitivity_report.md" }

// Tool: close_session
{ "session_id": "sess-sens-01" }
```
