# Requirements Document: NRTL Binary Interaction Parameters Support

## Introduction

This specification defines the requirements for extending the `set_binary_interaction_parameter` tool to properly support activity coefficient models, specifically NRTL (Non-Random Two-Liquid). The current implementation only supports cubic equations of state (Peng-Robinson, SRK) with a single kij parameter, but NRTL requires three asymmetric parameters per binary compound pair.

The scope covers:
1. **Extended Parameter API**: Support for multiple parameter types (alpha, tau12, tau21)
2. **NRTL-Specific Logic**: Access to DWSIM's NRTL internal parameter structure
3. **Backward Compatibility**: Existing PR/SRK usage continues to work unchanged
4. **Better Error Handling**: Clear errors when parameters don't match property package type

## Problem Statement

### Current Limitation

The existing `set_binary_interaction_parameter` implementation:
- Only accesses the `m_pr` field (Peng-Robinson model)
- Only sets a single `kij` value per compound pair
- **Silently fails** for NRTL with a warning log - no error is raised
- Has no mechanism to specify parameter type or asymmetric values

### NRTL Model Requirements

NRTL (Non-Random Two-Liquid) is an activity coefficient model for liquid-liquid equilibrium (LLE) that requires **three parameters per binary compound pair**:

| Parameter | Description | Typical Range | Notes |
|-----------|-------------|---------------|-------|
| α₁₂ (alpha) | Non-randomness parameter | 0.0 - 1.0 | Often ~0.3, temperature-independent |
| τ₁₂ (tau12) | Interaction parameter (1→2) | Varies | Temperature-dependent, asymmetric |
| τ₂₁ (tau21) | Interaction parameter (2→1) | Varies | Temperature-dependent, asymmetric |

**Critical**: τ₁₂ ≠ τ₂₁ in general - the parameters are asymmetric.

### Impact

Without proper NRTL parameters, the activity coefficient model cannot compute:
- Liquid-liquid phase split (immiscibility)
- Accurate vapor-liquid equilibrium for polar/non-ideal systems
- Proper activity coefficients for non-ideal mixtures

## Alignment with Product Vision

This feature directly supports the DWSIM MCP Server's core mission:

- **Composable Operations**: LLM agents can build sophisticated thermodynamic models
- **AI-Native Interface**: Intuitive parameter specification matching textbook conventions
- **Safety First**: Clear errors prevent silent failures in critical calculations
- **Observable by Default**: Parameter validation confirms correct setup before simulation

## Requirements

### REQ-1: Extended Parameter Type Specification

**User Story:** As an LLM agent, I want to specify which type of binary interaction parameter I'm setting, so that I can configure NRTL and other activity coefficient models correctly.

#### Acceptance Criteria

1. WHEN the agent calls `set_binary_interaction_parameter` with an optional `parameter_type` field THEN the system SHALL accept the following values:
   - `"kij"` - Binary interaction parameter for cubic EOS (PR, SRK) - **default**
   - `"alpha"` - Non-randomness parameter for NRTL
   - `"tau12"` - Interaction parameter τ₁₂ for NRTL (compound1 → compound2)
   - `"tau21"` - Interaction parameter τ₂₁ for NRTL (compound2 → compound1)

2. IF `parameter_type` is omitted THEN the system SHALL default to `"kij"` for backward compatibility.

3. WHEN `parameter_type` is `"alpha"` THEN the system SHALL validate that the value is between 0.0 and 1.0 (inclusive).

4. IF `parameter_type` is not valid for the current property package (e.g., `"alpha"` with Peng-Robinson) THEN the system SHALL return an error: "Parameter type '{type}' is not valid for property package '{package}'. Valid types: [kij]".

### REQ-2: NRTL Parameter Storage in DWSIM

**User Story:** As an LLM agent, I want NRTL parameters to be correctly stored in DWSIM's internal structure, so that phase equilibrium calculations use my specified values.

#### Acceptance Criteria

1. WHEN the agent sets `parameter_type: "alpha"` for compound pair (A, B) THEN the system SHALL store the value in DWSIM's NRTL α₁₂ parameter location.

2. WHEN the agent sets `parameter_type: "tau12"` for compound pair (A, B) THEN the system SHALL store the value in DWSIM's NRTL τ₁₂ parameter location (A→B direction).

3. WHEN the agent sets `parameter_type: "tau21"` for compound pair (A, B) THEN the system SHALL store the value in DWSIM's NRTL τ₂₁ parameter location (B→A direction).

4. IF the property package is NRTL and the compound pair already has a default database value THEN the system SHALL override it with the user-specified value.

5. WHEN parameters are set THEN the system SHALL return confirmation including: compound pair, parameter type, old value (if any), new value.

### REQ-3: Bulk Parameter Setting for NRTL

**User Story:** As an LLM agent, I want to set all three NRTL parameters for a compound pair in a single call, so that I can configure interactions efficiently.

#### Acceptance Criteria

1. WHEN the agent calls a new tool `set_nrtl_parameters` with `compound_a`, `compound_b`, `alpha`, `tau12`, and `tau21` THEN the system SHALL set all three parameters atomically.

2. IF any parameter is omitted in `set_nrtl_parameters` THEN the system SHALL use DWSIM's default database value for that parameter (if available) or zero.

3. WHEN all three parameters are set successfully THEN the system SHALL return a summary including all parameter values.

4. IF the current property package is not NRTL THEN the system SHALL return an error: "set_nrtl_parameters requires NRTL property package. Current package: '{package}'".

### REQ-4: Property Package Validation

**User Story:** As an LLM agent, I want clear errors when I try to set parameters incompatible with my property package, so that I can fix my configuration.

#### Acceptance Criteria

1. WHEN the agent tries to set NRTL parameters (alpha, tau12, tau21) with a non-NRTL property package THEN the system SHALL return an error with:
   - Current property package name
   - Supported parameter types for that package
   - Suggestion to change property package if NRTL parameters are needed

2. WHEN the agent tries to set kij with an NRTL property package THEN the system SHALL return a warning (not error) indicating that kij is typically not used with NRTL and suggesting NRTL-specific parameters.

3. IF the property package has not been set yet THEN the system SHALL return an error: "Property package not set. Call set_property_package before setting binary interaction parameters."

### REQ-5: Get Binary Interaction Parameters Tool

**User Story:** As an LLM agent, I want to query current binary interaction parameters, so that I can verify my configuration and debug issues.

#### Acceptance Criteria

1. WHEN the agent calls `get_binary_interaction_parameters` with `session_id` and optionally `compound_a`, `compound_b` THEN the system SHALL return all BIP values for the specified pair (or all pairs if not specified).

2. WHEN returning NRTL parameters THEN the response SHALL include: alpha, tau12, tau21, and source (user-set vs database default).

3. WHEN returning PR/SRK parameters THEN the response SHALL include: kij and source.

4. IF no parameters have been set for a compound pair THEN the response SHALL indicate "using database defaults" or "no parameters available".

### REQ-6: Backward Compatibility

**User Story:** As an LLM agent using existing workflows, I want my current `set_binary_interaction_parameter` calls to continue working unchanged.

#### Acceptance Criteria

1. WHEN the agent calls `set_binary_interaction_parameter` without `parameter_type` THEN the system SHALL behave exactly as before (set kij for PR/SRK).

2. IF the property package is PR or SRK THEN the single-value call SHALL continue to work without requiring parameter_type specification.

3. WHEN migrating from old API usage THEN no changes SHALL be required for existing PR/SRK workflows.

## Non-Functional Requirements

### Code Architecture and Modularity

- **Single Responsibility Principle**: Parameter type handling logic should be separate from DWSIM internal access
- **Modular Design**: NRTL parameter access should be a separate adapter method from PR/SRK
- **Strategy Pattern**: Consider using strategy pattern to handle different property package parameter structures
- **Clear Interfaces**: Each property package type should have a clear interface for its parameter operations

### Files Requiring Modification

1. **Python API Layer**:
   - `mcp_service/server/dwsim_mcp_server/tools/flowsheet.py` - Add parameter_type to tool definition
   - `mcp_service/server/dwsim_mcp_server/models/mcp_inputs/flowsheet_build.py` - Extend Pydantic model

2. **Python Service Layer**:
   - `mcp_service/server/dwsim_mcp_server/service/flowsheet_service.py` - Validation logic
   - `mcp_service/server/dwsim_mcp_server/ipc/flowsheet_client.py` - Pass parameter type to C#

3. **C# Engine Layer**:
   - `mcp_service/dwsim_worker/DwsimWorker/Engine/FlowsheetOperations.cs` - Dispatch to adapter
   - `mcp_service/dwsim_worker/DwsimWorker/Adapters/PropertyPackageAdapter.cs` - Add NRTL parameter access

### Performance

- **Parameter Setting**: SHALL complete in under 100ms per parameter
- **Bulk NRTL Setting**: SHALL complete in under 200ms for all three parameters
- **Parameter Query**: SHALL complete in under 500ms for all compound pairs

### Security

- **Input Validation**: All parameter values SHALL be validated as finite numbers
- **Bounds Checking**: Alpha parameter SHALL be validated to [0.0, 1.0] range
- **No Injection**: Compound names SHALL be validated against DWSIM database

### Reliability

- **Atomic Operations**: Bulk parameter setting SHALL be atomic (all succeed or all fail)
- **Error Recovery**: Failed parameter setting SHALL not corrupt existing parameters
- **Idempotency**: Setting the same parameter value twice SHALL produce identical state

### Usability

- **Clear Error Messages**: All errors SHALL explain what went wrong and how to fix it
- **Parameter Discovery**: Tool descriptions SHALL list valid parameter types per property package
- **Self-Documenting**: Response messages SHALL confirm what was set and its effect

## Technical Investigation Required

Before implementation, the following DWSIM internals need to be investigated:

1. **NRTL Internal Structure**: What field/property holds NRTL parameters in DWSIM? (similar to `m_pr` for PR)
2. **Parameter Access Pattern**: How does DWSIM store α, τ₁₂, τ₂₁ internally?
3. **Database Defaults**: How are default NRTL parameters loaded from the DWSIM compound database?
4. **UNIQUAC/UNIFAC**: Do these models have similar parameter structures that should be considered?

## References

- NRTL Model: Renon, H.; Prausnitz, J.M. (1968). "Local compositions in thermodynamic excess functions for liquid mixtures". AIChE Journal. 14 (1): 135–144.
- DWSIM Property Package Documentation: https://dwsim.org/wiki/index.php?title=Property_Packages
