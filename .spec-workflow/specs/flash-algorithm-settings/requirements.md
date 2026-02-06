# Requirements Document: Flash Algorithm Settings Exposure via MCP

## Introduction

This specification defines the requirements for exposing DWSIM's FlashAlgorithm configuration settings through the MCP flash calculation tools (`flash_tp`, `flash_ph`, `flash_ps`). Currently, all flash calculations use DWSIM's default flash algorithm behavior with no ability for the LLM agent to control phase identification, stability testing, or liquid-liquid detection.

The three target settings are:
1. **DoPhaseIdentificationAtFlash** (`bool`): Controls whether the flash algorithm performs phase identification (distinguishing vapor from liquid) after convergence
2. **StabilityTestSeverity** (`int`, 0-2): Controls the rigor of Gibbs energy stability analysis used to detect phase splits
3. **LiquidLiquidDetection** (`bool`): Controls whether the flash algorithm checks for liquid-liquid equilibrium (two immiscible liquid phases)

These settings are properties on DWSIM's `FlashAlgorithm` object, which is accessible via the property package. None of these settings are currently referenced anywhere in the codebase.

## Problem Statement

### Current Limitation

The existing flash calculation pipeline:
- Creates a temporary stream with compounds and conditions
- Flashes the stream using the property package's default flash algorithm
- Returns phase results
- **Never configures** `FlashAlgorithm.DoPhaseIdentificationAtFlash`, `FlashAlgorithm.StabilityTestSeverity`, or `FlashAlgorithm.LiquidLiquidDetection`
- Relies entirely on DWSIM's compiled defaults

### Impact

Without control over these settings:
- **Phase misidentification**: The flash may converge to a solution that labels phases incorrectly (e.g., a dense supercritical phase labeled as liquid)
- **Missed liquid-liquid splits**: Systems with partial miscibility (water + hydrocarbon, organic solvent pairs) may incorrectly predict a single liquid phase
- **Insufficient stability testing**: For complex multicomponent systems near phase boundaries, the default stability test may miss metastable solutions
- **No user recourse**: The agent cannot tune flash behavior for difficult systems without these controls

### DWSIM Flash Algorithm Architecture

In DWSIM, every property package has a `FlashAlgorithm` property (type `DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.FlashAlgorithm`). The relevant properties are:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DoPhaseIdentificationAtFlash` | `bool` | varies | Run phase identification after flash convergence |
| `StabilityTestSeverity` | `int` | 0 | 0 = light, 1 = moderate, 2 = rigorous Gibbs stability test |
| `LiquidLiquidDetection` | `bool` | varies | Check for liquid-liquid phase split |

These properties are set on `propertyPackage.FlashAlgorithm` before the flash calculation is executed.

## Alignment with Product Vision

This feature directly supports the DWSIM MCP Server's core mission:

- **Composable Operations**: Agents can fine-tune flash behavior per-calculation without changing the property package itself
- **AI-Native Interface**: Intuitive boolean/enum parameters that map directly to DWSIM concepts
- **DWSIM-Native**: Thin adapter layer exposing existing DWSIM controls, not inventing new abstractions
- **Safety First**: Optional parameters with sensible defaults — existing workflows are unaffected
- **Observable by Default**: Flash results already return convergence status; these settings improve result quality

## Requirements

### REQ-1: Optional Flash Algorithm Settings on Flash Input Models

**User Story:** As an LLM agent, I want to optionally specify flash algorithm settings when calling `flash_tp`, `flash_ph`, or `flash_ps`, so that I can control phase identification and liquid-liquid detection for difficult systems.

#### Acceptance Criteria

1. WHEN the agent calls any flash tool (`flash_tp`, `flash_ph`, `flash_ps`) with an optional `flash_settings` object THEN the system SHALL accept the following fields:
   - `do_phase_identification` (`bool`, optional) - Controls `DoPhaseIdentificationAtFlash`
   - `stability_test_severity` (`int`, optional, 0-2) - Controls `StabilityTestSeverity`
   - `liquid_liquid_detection` (`bool`, optional) - Controls `LiquidLiquidDetection`

2. IF `flash_settings` is omitted entirely THEN the system SHALL use DWSIM's default flash algorithm behavior (no change from current behavior).

3. IF `flash_settings` is provided but individual fields are omitted THEN the system SHALL use DWSIM's default for each omitted field.

4. WHEN `stability_test_severity` is provided THEN the system SHALL validate it is 0, 1, or 2. IF outside this range THEN the system SHALL return a validation error.

### REQ-2: Flash Algorithm Configuration in C# Worker

**User Story:** As the MCP service, I want the C# ThermodynamicsAdapter to apply flash algorithm settings before running a flash calculation, so that DWSIM uses the agent-specified configuration.

#### Acceptance Criteria

1. WHEN the `ThermodynamicsAdapter.RunFlash()` method receives flash settings THEN the system SHALL access `propertyPackage.FlashAlgorithm` and set the specified properties before creating and flashing the temporary stream.

2. WHEN `do_phase_identification` is specified THEN the system SHALL set `FlashAlgorithm.DoPhaseIdentificationAtFlash` to the provided value.

3. WHEN `stability_test_severity` is specified THEN the system SHALL set `FlashAlgorithm.StabilityTestSeverity` to the provided value.

4. WHEN `liquid_liquid_detection` is specified THEN the system SHALL set `FlashAlgorithm.LiquidLiquidDetection` to the provided value.

5. AFTER the flash calculation completes (success or failure) THEN the system SHALL restore the original flash algorithm settings to avoid side effects on subsequent calculations in the same session.

### REQ-3: Pass-Through from Python Service to C# Adapter

**User Story:** As the Python MCP service layer, I want to pass flash algorithm settings from the MCP tool inputs through to the C# ThermodynamicsAdapter, so that the settings reach DWSIM.

#### Acceptance Criteria

1. WHEN the Python `ThermodynamicsService.flash_tp()` (and `flash_ph`, `flash_ps`) receives a payload with `flash_settings` THEN the service SHALL pass these settings to the C# adapter's `FlashTP()`, `FlashPH()`, `FlashPS()` methods.

2. WHEN calling the C# adapter via pythonnet THEN the system SHALL serialize flash settings as individual parameters (booleans and int) rather than as a complex object, to avoid pythonnet marshalling issues.

3. IF flash settings are `None`/omitted THEN the service SHALL call the adapter methods without flash setting parameters (maintaining backward compatibility with the existing method signatures).

### REQ-4: Backward Compatibility

**User Story:** As an LLM agent using existing flash workflows, I want my current flash tool calls to continue working unchanged.

#### Acceptance Criteria

1. WHEN the agent calls any flash tool without `flash_settings` THEN the system SHALL behave exactly as it does today (DWSIM defaults).

2. WHEN the C# adapter methods are called without flash setting parameters THEN the adapter SHALL not modify the flash algorithm configuration.

3. WHEN upgrading the MCP server THEN no changes SHALL be required for existing flash workflows.

### REQ-5: Flash Settings in Results

**User Story:** As an LLM agent, I want to see which flash algorithm settings were active during a calculation, so that I can verify my configuration and debug convergence issues.

#### Acceptance Criteria

1. WHEN a flash calculation completes THEN the response MAY include a `flash_settings_applied` object showing the effective values of `do_phase_identification`, `stability_test_severity`, and `liquid_liquid_detection`.

2. IF no custom flash settings were provided THEN `flash_settings_applied` SHALL indicate "defaults" or be omitted.

## Non-Functional Requirements

### Code Architecture and Modularity

- **Single Responsibility Principle**: Flash settings model should be a separate Pydantic class, not inlined into each flash input model
- **Modular Design**: A shared `FlashSettings` model reused across `FlashTPInputs`, `FlashPHInputs`, `FlashPSInputs`
- **Clear Interfaces**: The C# adapter method signatures should accept optional flash settings parameters cleanly
- **Restore Pattern**: Flash algorithm settings must be saved/restored around each calculation to prevent session-level side effects

### Files Requiring Modification

1. **Python Models**:
   - NEW: `mcp_service/server/dwsim_mcp_server/models/mcp_inputs/flash_settings.py` - `FlashSettings` Pydantic model
   - MODIFY: `mcp_service/server/dwsim_mcp_server/models/mcp_inputs/flash_inputs.py` - Add optional `flash_settings` field to all three input classes

2. **Python Service**:
   - MODIFY: `mcp_service/server/dwsim_mcp_server/services/thermodynamics_service.py` - Pass flash settings to C# adapter calls

3. **Python Tools**:
   - MODIFY: `mcp_service/server/dwsim_mcp_server/tools/analysis.py` - Pass `flash_settings` from MCP tool inputs to service layer

4. **C# Worker**:
   - MODIFY: `mcp_service/dwsim_worker/DwsimWorker/Adapters/ThermodynamicsAdapter.cs` - Add overloads or optional parameters for flash settings; apply settings to `propertyPackage.FlashAlgorithm` before flash; restore after

5. **Python Response Models** (optional, for REQ-5):
   - MODIFY: `mcp_service/server/dwsim_mcp_server/models/responses/flash_results.py` - Add optional `flash_settings_applied` field

### Performance

- **Settings Application**: SHALL add < 10ms overhead per flash calculation (simple property sets via reflection)
- **Settings Restoration**: SHALL add < 5ms overhead (save/restore three property values)
- **No regression**: Flash calculations without custom settings SHALL have zero additional overhead

### Security

- **Input Validation**: `stability_test_severity` SHALL be validated to integer range [0, 2]
- **Type Safety**: Boolean fields SHALL reject non-boolean values
- **No Injection**: Settings are applied via typed property access, not string-based reflection

### Reliability

- **Settings Restoration**: Flash algorithm settings SHALL always be restored after calculation, even on exception
- **Idempotency**: Setting the same flash settings twice SHALL produce identical behavior
- **Session Isolation**: Flash settings are per-calculation, not per-session — concurrent calculations in different tools SHALL not interfere

### Usability

- **Optional by Design**: All flash settings are optional; agents only specify what they need
- **Clear Defaults**: Tool descriptions SHALL explain the default behavior when settings are omitted
- **Descriptive Errors**: Invalid `stability_test_severity` values SHALL produce clear error messages listing valid values (0, 1, 2)
- **Self-Documenting**: MCP tool descriptions SHALL explain what each setting controls and when to use it

## Technical Investigation Required

Before implementation, the following DWSIM internals need to be verified:

1. **FlashAlgorithm Access**: Confirm that `propertyPackage.FlashAlgorithm` is accessible via reflection through pythonnet and the C# adapter
2. **Property Names**: Verify the exact property names (`DoPhaseIdentificationAtFlash`, `StabilityTestSeverity`, `LiquidLiquidDetection`) on the flash algorithm object in the DWSIM version used
3. **Default Values**: Document what DWSIM's defaults are for each property on each supported property package (PR, SRK, NRTL, UNIFAC)
4. **Thread Safety**: Confirm that modifying flash algorithm settings on a property package is safe within a single session (no cross-session state sharing)
5. **Save/Restore**: Determine whether flash algorithm settings persist after modification or reset on each flash call

## References

- DWSIM FlashAlgorithm source: `DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms`
- DWSIM Nested Loops algorithm: Primary flash algorithm using successive substitution
- Gibbs energy stability analysis: Michelsen, M.L. (1982). "The isothermal flash problem. Part I. Stability". Fluid Phase Equilibria. 9(1): 1-19.
