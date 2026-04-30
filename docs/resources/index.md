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

# DWSIM MCP Server Documentation

This documentation provides reference materials for using the DWSIM MCP Server to build and run chemical process simulations through LLM agents.

## Available Topics

| Topic | Description |
|-------|-------------|
| [getting-started](resource://docs/getting-started) | Installation, VS Code setup, and first simulation |
| [unit-operations](resource://docs/unit-operations) | Guide to DWSIM unit operations and their parameters |
| [property-packages](resource://docs/property-packages) | Thermodynamic property packages and their applicability |
| [compounds](resource://docs/compounds) | Compound database and adding chemicals to simulations |

## Quick Start Workflow

1. **Create a session**: Use `create_session` tool to start a new simulation workspace
2. **Add compounds**: Use `add_compound` to add chemicals (e.g., "Methane", "Water")
3. **Set property package**: Use `set_property_package` (e.g., "Peng-Robinson")
4. **Set Binary Interaction Parameters (BIPs)**: Use `set_binary_interaction_parameter` for accurate phase equilibrium
5. **Add inlet stream**: Create feed stream with `add_stream` (is_source=true, with T, P, flow, composition)
6. **Flash inlet stream**: Use `flash_stream` to compute phase equilibrium on feed
7. **Add outlet streams**: Create outlet streams with `add_stream` (is_source=false, placeholder values)
8. **Add unit operation**: Use `add_unit` (e.g., type="separator")
9. **Connect streams**: Use `connect` to wire streams to unit ports
10. **Run simulation**: Use `run` tool to execute calculations
11. **Get results**: Use `get_results` to retrieve stream properties
12. **Close session**: Use `close_session` when done

## Complete Three-Phase Separator Example

This is a fully working example that demonstrates phase separation of a gas/oil/water mixture:

### Step 1: Create Session
```json
{"tool": "create_session", "arguments": {"name": "three-phase-separator"}}
// Returns: {"session_id": "77701c7c-6ee3-46be-af67-a3837c761792"}
```

### Step 2: Add Compounds
```json
{"tool": "add_compound", "arguments": {"session_id": "SESSION_ID", "compound_name": "Methane"}}
{"tool": "add_compound", "arguments": {"session_id": "SESSION_ID", "compound_name": "Water"}}
{"tool": "add_compound", "arguments": {"session_id": "SESSION_ID", "compound_name": "n-Decane"}}
```

### Step 3: Set Property Package
```json
{"tool": "set_property_package", "arguments": {"session_id": "SESSION_ID", "package_name": "Peng-Robinson"}}
```

### Step 4: Set Binary Interaction Parameters (Critical for accuracy!)
```json
{"tool": "set_binary_interaction_parameter", "arguments": {"session_id": "SESSION_ID", "compound1": "Methane", "compound2": "n-Decane", "value": 0.0489}}
{"tool": "set_binary_interaction_parameter", "arguments": {"session_id": "SESSION_ID", "compound1": "Water", "compound2": "Methane", "value": 0.5}}
{"tool": "set_binary_interaction_parameter", "arguments": {"session_id": "SESSION_ID", "compound1": "Water", "compound2": "n-Decane", "value": 0.5}}
```

### Step 5: Create Feed Stream (is_source=true)
```json
{"tool": "add_stream", "arguments": {
  "session_id": "SESSION_ID",
  "name": "FEED",
  "temperature": 300.0,
  "pressure": 101325.0,
  "molar_flow": 544.0,
  "composition": {"Methane": 0.333, "Water": 0.333, "n-Decane": 0.334},
  "is_source": true
}}
// Returns: {"stream_id": "S1", "name": "FEED"}
```

### Step 6: Flash Feed Stream
```json
{"tool": "flash_stream", "arguments": {"session_id": "SESSION_ID", "stream_id": "S1"}}
// Returns: {"stream_id": "S1", "flashed": true}
```

### Step 7: Create Outlet Streams (is_source=false, placeholder values)
```json
{"tool": "add_stream", "arguments": {
  "session_id": "SESSION_ID",
  "name": "VAPOR",
  "temperature": 300.0,
  "pressure": 91325.0,
  "molar_flow": 0.001,
  "composition": {"Methane": 0.333, "Water": 0.333, "n-Decane": 0.334},
  "is_source": false
}}

{"tool": "add_stream", "arguments": {
  "session_id": "SESSION_ID",
  "name": "LIGHT_LIQUID",
  "temperature": 300.0,
  "pressure": 91325.0,
  "molar_flow": 0.001,
  "composition": {"Methane": 0.333, "Water": 0.333, "n-Decane": 0.334},
  "is_source": false
}}

{"tool": "add_stream", "arguments": {
  "session_id": "SESSION_ID",
  "name": "HEAVY_LIQUID",
  "temperature": 300.0,
  "pressure": 91325.0,
  "molar_flow": 0.001,
  "composition": {"Methane": 0.333, "Water": 0.333, "n-Decane": 0.334},
  "is_source": false
}}
```

### Step 8: Add Three-Phase Separator
```json
{"tool": "add_unit", "arguments": {
  "session_id": "SESSION_ID",
  "unit_type": "separator",
  "name": "SEP-101",
  "parameters": {
    "CalculationMode": "Legacy",
    "PressureCalculation": "Average",
    "DimensionRatio": 3.0,
    "ResidenceTime": 5.0
  }
}}
// Returns: {"unit_id": "U1", "name": "SEP-101", "unit_type": "separator"}
```

### Step 9: Connect Streams to Separator
```json
{"tool": "connect", "arguments": {"session_id": "SESSION_ID", "source_id": "S1", "target_id": "U1", "port_name": "Inlet"}}
{"tool": "connect", "arguments": {"session_id": "SESSION_ID", "source_id": "S2", "target_id": "U1", "port_name": "VaporOutlet"}}
{"tool": "connect", "arguments": {"session_id": "SESSION_ID", "source_id": "S3", "target_id": "U1", "port_name": "LiquidOutlet1"}}
{"tool": "connect", "arguments": {"session_id": "SESSION_ID", "source_id": "S4", "target_id": "U1", "port_name": "LiquidOutlet2"}}
```

### Step 10: Run Simulation
```json
{"tool": "run", "arguments": {"session_id": "SESSION_ID"}}
```

### Expected Results

The simulation converges in ~123ms with:

| Stream | Flow (mol/s) | Phase | Key Composition |
|--------|--------------|-------|-----------------|
| S1 (FEED) | 544.0 | Mixed | 33.3% each |
| S2 (VAPOR) | 184.5 | 100% Vapor | 98.2% Methane |
| S3 (LIGHT_LIQUID) | 359.5 | 100% Liquid | 49.5% Water, 50.5% n-Decane |
| S4 (HEAVY_LIQUID) | 0 | Empty | (no second liquid at these conditions) |

- **Mass Balance Error**: 2.1×10⁻¹⁴ % (excellent closure)
- **Convergence**: Achieved

### Step 11: Close Session
```json
{"tool": "close_session", "arguments": {"session_id": "SESSION_ID"}}
```

## Separator Port Names

When connecting streams to a three-phase separator:
- `Inlet` - Feed stream connection
- `VaporOutlet` - Vapor product stream
- `LiquidOutlet1` - First liquid product (light liquid)
- `LiquidOutlet2` - Second liquid product (heavy liquid)

## Units

All properties use SI units:
- Temperature: K (Kelvin)
- Pressure: Pa (Pascal)  
- Flow: mol/s (molar) or kg/s (mass)
- Composition: mole fraction (0-1)

## Resources

Access detailed results and documentation via MCP resources:
- `resource://docs/{topic}` - Documentation
- `resource://cases/{name}` - Sample cases
- `resource://session/{id}/results` - Simulation results
