<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

# Golden Test: Three-Phase Separator Simulation

**Purpose:** Regression test to verify the complete DWSIM MCP workflow functions correctly.

**Last Verified:** 2026-01-31
**Status:** ✅ Passing

## Test Overview

This golden test validates the end-to-end MCP workflow for a three-phase separator simulation. It exercises all critical MCP tools in sequence and verifies the simulation converges with correct mass balance.

## System Under Test

- **Components:** Python MCP Server → DwsimWorker.dll → DWSIM Thermodynamics
- **Property Package:** Peng-Robinson equation of state
- **Unit Operation:** Three-phase separator (gas-liquid-liquid)

## Test Specification

### Input Conditions

| Parameter | Value | Units |
|-----------|-------|-------|
| Temperature | 300 | K |
| Pressure | 500,000 | Pa |
| Molar Flow | 100 | mol/s |

### Feed Composition

| Compound | Mole Fraction |
|----------|---------------|
| Methane | 0.40 |
| n-Hexane | 0.40 |
| Water | 0.20 |

### Binary Interaction Parameters (BIPs)

| Pair | kij |
|------|-----|
| Methane / Water | 0.50 |
| n-Hexane / Water | 0.50 |
| Methane / n-Hexane | 0.03 |

## MCP Tool Sequence

Execute these MCP tools in order:

### Step 1: Create Session
```json
{"name": "Golden Test - Three Phase Separator"}
```
**Expected:** Returns `session_id`

### Step 2: Add Compounds
```json
{"compound_name": "Methane"}
{"compound_name": "n-Hexane"}
{"compound_name": "Water"}
```
**Expected:** All return `{"added": true}`

### Step 3: Set Property Package
```json
{"package_name": "Peng-Robinson"}
```
**Expected:** `{"applied": true}`

### Step 4: Set Binary Interaction Parameters
```json
{"compound1": "Methane", "compound2": "Water", "value": 0.5}
{"compound1": "n-Hexane", "compound2": "Water", "value": 0.5}
{"compound1": "Methane", "compound2": "n-Hexane", "value": 0.03}
```
**Expected:** All return `{"applied": true}`

### Step 5: Create Feed Stream
```json
{
  "name": "Feed",
  "is_source": true,
  "temperature": 300,
  "pressure": 500000,
  "molar_flow": 100,
  "composition": {"Methane": 0.4, "n-Hexane": 0.4, "Water": 0.2}
}
```
**Expected:** Returns `{"stream_id": "S1"}`

### Step 6: Flash Feed Stream
```json
{"stream_id": "S1"}
```
**Expected:** `{"flashed": true}`

### Step 7: Create Outlet Streams
```json
{"name": "Vapor", "is_source": false}
{"name": "Liquid1", "is_source": false}
{"name": "Liquid2", "is_source": false}
```
**Expected:** Returns `S2`, `S3`, `S4` stream IDs

### Step 8: Add Separator Unit
```json
{
  "unit_type": "separator",
  "name": "Separator",
  "parameters": {"CalculationMode": "Legacy", "PressureCalculation": "Average"}
}
```
**Expected:** Returns `{"unit_id": "U1"}`

### Step 9: Connect Streams
```json
{"source_id": "S1", "target_id": "U1", "port_name": "Inlet"}
{"source_id": "S2", "target_id": "U1", "port_name": "VaporOutlet"}
{"source_id": "S3", "target_id": "U1", "port_name": "LiquidOutlet1"}
{"source_id": "S4", "target_id": "U1", "port_name": "LiquidOutlet2"}
```
**Expected:** All return `{"connected": true}`

### Step 10: Run Simulation
```json
{"timeout_seconds": 120}
```
**Expected:** `{"status": "converged"}`

### Step 11: Close Session
```json
{"session_id": "<session_id>"}
```
**Expected:** `{"success": true}`

## Expected Results

### Convergence Criteria

| Criterion | Expected | Tolerance |
|-----------|----------|-----------|
| Status | `converged` | exact |
| Mass Balance Error | 0% | < 0.1% |
| Elapsed Time | < 5000ms | - |

### Stream Results

#### S1 (Feed)
| Property | Expected Value | Units |
|----------|----------------|-------|
| Temperature | 300 | K |
| Pressure | 500,000 | Pa |
| Total Molar Flow | 100 | mol/s |
| Vapor Fraction | ~0.413 | - |
| Liquid Fraction | ~0.587 | - |

#### S2 (Vapor Outlet)
| Property | Expected Value | Units |
|----------|----------------|-------|
| Temperature | 300 | K |
| Pressure | 500,000 | Pa |
| Molar Flow | ~41.28 | mol/s |
| Phase | 100% Vapor | - |
| Methane | ~96.9% | mol |
| n-Hexane | ~2.9% | mol |
| Water | ~0.2% | mol |

#### S3 (Liquid Outlet 1)
| Property | Expected Value | Units |
|----------|----------------|-------|
| Temperature | 300 | K |
| Pressure | 500,000 | Pa |
| Molar Flow | ~58.72 | mol/s |
| Phase | 100% Liquid | - |
| Methane | ~0.01% | mol |
| n-Hexane | ~66.1% | mol |
| Water | ~33.9% | mol |

#### S4 (Liquid Outlet 2)
| Property | Expected Value | Units |
|----------|----------------|-------|
| Molar Flow | 0 | mol/s |
| Notes | Empty - hexane and water form single liquid phase at these conditions |

### Physical Interpretation

At 300K and 500 kPa:
- **Methane** (light hydrocarbon) preferentially partitions to vapor phase
- **n-Hexane** (heavy hydrocarbon) preferentially partitions to liquid phase
- **Water** remains in liquid phase with hexane
- The hexane-water mixture forms a single liquid phase (no liquid-liquid separation) at these specific conditions

## Failure Modes

### Common Failures and Root Causes

| Failure | Likely Cause |
|---------|--------------|
| `flash_stream` returns error | GlobalSettings not configured for headless mode |
| `IndexOutOfRangeException` in Inspector | `InspectorEnabled` not set to `false` |
| Simulation timeout | DWSIM threading/deadlock issue |
| `converged: false` | Missing BIPs or invalid composition |
| Mass balance > 0.1% | Numerical convergence issue |

### Recovery Steps

If this test fails:
1. Check MCP server logs for exceptions
2. Verify DWSIM binaries are patched for headless mode
3. Confirm `GlobalSettings.AutomationMode = true`
4. Confirm `GlobalSettings.InspectorEnabled = false`
5. Restart MCP server and retry

## Related Documentation

- [Bug Fix: flash-calculation-failure](../../.spec-workflow/bugs/flash-calculation-failure.md)
- [Threading Fix Report](../architecture/threading-fix-report.md)
- [MCP Tools Reference](../mcp/mcp-tools.md)

## Automation

This test can be automated via:
1. **MCP Client:** Direct MCP tool calls in sequence
2. **Python Integration Test:** `tests/integration/test_simulation_integration.py`
3. **C# Unit Test:** `DwsimWorker.Tests.GoldenTest_ThreePhaseSeparatorCalculation_Succeeds`
