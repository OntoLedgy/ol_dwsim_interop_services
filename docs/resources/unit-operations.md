<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

# Unit Operations

DWSIM provides a comprehensive library of unit operations for building chemical process flowsheets. This guide covers the most commonly used operations and their key parameters.

## Separators

### Three-Phase Separator

Separates a feed stream into vapor, liquid hydrocarbon, and aqueous phases based on density differences.

**Parameters:**
- `CalculationMode`: "Legacy" (recommended) or "Experiment"
- `PressureCalculation`: "Average" or other modes
- `DimensionRatio`: Length/diameter ratio (default: 3.0)
- `ResidenceTime`: Liquid residence time in minutes (default: 5.0)

**Port Names (for `connect` tool):**
- `Inlet` - Feed stream (single mixed stream)
- `VaporOutlet` - Vapor product stream
- `LiquidOutlet1` - First liquid product (light liquid/hydrocarbon)
- `LiquidOutlet2` - Second liquid product (heavy liquid/aqueous)

**Usage:**
```json
{"tool": "add_unit", "arguments": {
  "session_id": "SESSION_ID",
  "unit_type": "separator",
  "name": "V-100",
  "parameters": {
    "CalculationMode": "Legacy",
    "PressureCalculation": "Average",
    "DimensionRatio": 3.0,
    "ResidenceTime": 5.0
  }
}}
```

**Connecting Streams:**
```json
{"tool": "connect", "arguments": {"session_id": "SESSION_ID", "source_id": "S1", "target_id": "U1", "port_name": "Inlet"}}
{"tool": "connect", "arguments": {"session_id": "SESSION_ID", "source_id": "S2", "target_id": "U1", "port_name": "VaporOutlet"}}
{"tool": "connect", "arguments": {"session_id": "SESSION_ID", "source_id": "S3", "target_id": "U1", "port_name": "LiquidOutlet1"}}
{"tool": "connect", "arguments": {"session_id": "SESSION_ID", "source_id": "S4", "target_id": "U1", "port_name": "LiquidOutlet2"}}
```

### Flash Separator (Vessel)

Two-phase vapor-liquid separator. Use for simple flash operations.

**Parameters:**
- `pressure_drop`: Pressure drop (Pa)
- `flash_type`: "TP" (isothermal), "PH" (isenthalpic), "PS" (isentropic)

**Ports:**
- Inlet: Feed
- Outlets: Vapor, Liquid

### Component Separator

Ideal separator that splits components by specified fractions.

**Parameters:**
- `split_fractions`: Dict mapping compound names to vapor fraction (0-1)

## Heat Transfer

### Heater/Cooler

Single-stream heat exchanger for heating or cooling.

**Parameters:**
- `outlet_temperature`: Target outlet temperature (K)
- `outlet_vapor_fraction`: Target vapor fraction (0-1)
- `heat_duty`: Specified heat duty (W)
- `pressure_drop`: Pressure drop (Pa)

**Calculation Modes:**
1. Specify outlet temperature
2. Specify outlet vapor fraction
3. Specify heat duty

**Usage:**
```python
add_unit(session_id, "Heater", "H-100", {
    "outlet_temperature": 350.0,  # K
    "pressure_drop": 5000  # Pa
})
```

### Heat Exchanger

Two-stream shell-and-tube heat exchanger.

**Parameters:**
- `heat_transfer_area`: Area (m²)
- `overall_u`: Overall heat transfer coefficient (W/m²·K)
- `hot_side_pressure_drop`: Pa
- `cold_side_pressure_drop`: Pa

## Pressure Changers

### Pump

Increases pressure of liquid streams.

**Parameters:**
- `outlet_pressure`: Target discharge pressure (Pa)
- `pressure_increase`: Pressure delta (Pa)
- `efficiency`: Pump efficiency (0-1)

**Usage:**
```python
add_unit(session_id, "Pump", "P-100", {
    "outlet_pressure": 1000000,  # 10 bar
    "efficiency": 0.75
})
```

### Compressor

Increases pressure of gas streams.

**Parameters:**
- `outlet_pressure`: Target discharge pressure (Pa)
- `pressure_ratio`: Compression ratio
- `efficiency`: Isentropic efficiency (0-1)
- `calculation_mode`: "OutletPressure", "PressureRatio", "PowerRequired"

### Valve

Reduces pressure through isenthalpic expansion.

**Parameters:**
- `outlet_pressure`: Target pressure (Pa)
- `pressure_drop`: Pressure reduction (Pa)
- `calculation_mode`: "OutletPressure", "PressureDrop"

## Mixers and Splitters

### Mixer

Combines multiple inlet streams into one outlet.

**Parameters:**
- `pressure_calculation`: "Minimum", "Maximum", "Average"

**Ports:**
- Inlets: Multiple feed streams
- Outlet: Single mixed stream

### Splitter

Divides one inlet into multiple outlets.

**Parameters:**
- `split_ratios`: List of fractions for each outlet (must sum to 1.0)

## Reactors

### Conversion Reactor

Simple reactor with specified conversion per reaction.

**Parameters:**
- `reactions`: List of reaction definitions
- `conversion`: Fractional conversion (0-1) per reaction
- `outlet_temperature`: Isothermal temperature (K)

### Equilibrium Reactor

Calculates equilibrium composition for specified reactions.

**Parameters:**
- `reactions`: Reaction definitions with equilibrium constants
- `temperature`: Reactor temperature (K)
- `pressure`: Reactor pressure (Pa)

### CSTR (Continuous Stirred Tank Reactor)

Well-mixed reactor with residence time distribution.

**Parameters:**
- `volume`: Reactor volume (m³)
- `reactions`: Reaction kinetics definitions
- `outlet_temperature`: Operating temperature (K)

## Columns

### Shortcut Column

Fenske-Underwood-Gilliland distillation estimation.

**Parameters:**
- `light_key`: Light key component name
- `heavy_key`: Heavy key component name
- `light_key_recovery`: Recovery in distillate (0-1)
- `heavy_key_recovery`: Recovery in bottoms (0-1)
- `reflux_ratio`: Actual/minimum reflux ratio

### Rigorous Column (CAPE-OPEN)

Full tray-by-tray distillation calculation.

**Parameters:**
- `number_of_stages`: Total theoretical stages
- `feed_stage`: Feed location (1 = top)
- `condenser_type`: "Total", "Partial"
- `reboiler_type`: "Kettle", "Thermosiphon"
- `reflux_ratio`: Operating reflux ratio
- `distillate_rate`: Distillate flow rate (mol/s)

## Tips for LLM Agents

1. **Start simple**: Use Heater/Cooler before complex heat exchangers
2. **Check phase**: Pumps require liquid, compressors require vapor
3. **Mass balance**: Total inlet flow must equal total outlet flow
4. **Energy balance**: Account for heat duties and work
5. **Convergence**: If simulation fails, check property package suitability
