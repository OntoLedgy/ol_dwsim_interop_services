# Requirements Document

## Introduction

This specification implements thermodynamic calculation execution on a configured three-phase separator unit operation in DWSIM. Building on Spec 1.2 (three-phase-separator-properties) which validated property setting capabilities, this specification validates the core simulation capability - the most critical technical risk in the DWSIM MCP Server project.

The ability to execute calculations and extract results is the fundamental purpose of integrating with DWSIM. Without this capability, the MCP server cannot provide any meaningful simulation functionality to LLM agents. This specification proves that DWSIM's flowsheet solver can be invoked programmatically, converges for realistic inputs, and produces extractable results.

This specification focuses on executing a **three-phase separator** calculation - a common unit operation in oil and gas processing that separates a mixed feed into vapor, light liquid (hydrocarbon), and heavy liquid (water) phases. It tests the complete calculation workflow: solver invocation, convergence handling, result extraction, mass balance validation, and error capture.

## Alignment with Product Vision

This specification directly supports several key aspects of the DWSIM MCP Server product vision outlined in product.md:

1. **Core Simulation Capability**: Validates the most critical technical risk - that DWSIM's flowsheet solver can be programmatically invoked and produces correct results. This is foundational for all MCP tools that execute simulations.

2. **CAPE-OPEN Domain Model**: Tests result extraction through CAPE-OPEN interfaces (ICapeThermoMaterialObject.GetProp), validating that calculated properties can be retrieved using the standard interface vocabulary.

3. **Structured Error Handling**: Implements convergence failure detection, solver error capture, and diagnostic information extraction - essential for providing actionable feedback to LLM agents.

4. **Observability**: Captures calculation timing, convergence status, and solver messages - the foundation for performance monitoring and debugging tools.

5. **Foundational for Future Phases**:
   - Spec 2.1 (test framework) will use this calculation as the primary golden test case
   - Spec 3.2 (JSON-RPC methods) will expose `simulation.run` and `simulation.get_results` based on this implementation
   - Spec 5.4 (MCP simulation tools) will wrap these operations as MCP tools

## Requirements

### Requirement 1: Flowsheet Solver Invocation

**User Story:** As a simulation developer, I want to invoke the DWSIM flowsheet solver on a configured separator, so that thermodynamic calculations are executed and phase equilibrium is determined.

#### Acceptance Criteria

1. WHEN the flowsheet solver is invoked on a properly configured flowsheet THEN the system SHALL execute the calculation without throwing unhandled exceptions
2. WHEN the solver is invoked THEN the system SHALL provide a clear success/failure status upon completion
3. WHEN the solver is invoked THEN the system SHALL capture the total calculation time (wall-clock milliseconds)
4. WHEN the solver is invoked THEN the system SHALL execute calculations on all connected unit operations in the flowsheet
5. IF the flowsheet is not properly configured (missing connections, no property package) THEN the system SHALL return a failure status with a descriptive error message before attempting calculation

### Requirement 2: Solver Convergence Handling

**User Story:** As a simulation developer, I want to know whether the flowsheet solver converged successfully, so that I can trust the calculated results or take corrective action.

#### Acceptance Criteria

1. WHEN the solver completes THEN the system SHALL report a convergence status (converged, not converged, error)
2. WHEN the solver converges successfully THEN the convergence status SHALL be "Converged" and calculation results SHALL be available
3. WHEN the solver fails to converge THEN the system SHALL capture the reason for non-convergence from DWSIM's solver messages
4. WHEN the solver encounters an error (not just non-convergence) THEN the system SHALL capture the exception type and message
5. WHEN convergence status is queried THEN the system SHALL return the status along with any solver messages or warnings

### Requirement 3: Outlet Stream Property Extraction

**User Story:** As a simulation developer, I want to extract calculated properties from the separator outlet streams, so that I can analyze the simulation results.

#### Acceptance Criteria

1. WHEN the solver converges THEN the system SHALL be able to extract properties from all outlet streams (vapor, liquid, water)
2. WHEN outlet stream properties are extracted THEN the system SHALL return:
   - Temperature (K)
   - Pressure (Pa)
   - Molar flow (mol/s)
   - Phase composition (mole fractions per compound)
   - Phase fraction (vapor fraction, liquid fraction)
3. WHEN outlet stream properties are extracted THEN the values SHALL be physically reasonable:
   - Temperature within ±50 K of inlet (for adiabatic separator)
   - Pressure equal to inlet pressure minus pressure drop (within tolerance)
   - Molar flows sum to inlet molar flow (mass balance)
4. WHEN composition is extracted THEN mole fractions SHALL sum to 1.0 ± 1e-6 for each outlet stream
5. WHEN a phase does not exist (e.g., no water phase for dry gas) THEN the system SHALL return zero flow for that outlet with appropriate indication

### Requirement 4: Mass Balance Validation

**User Story:** As a simulation developer, I want to verify that the calculated results satisfy mass balance, so that I can confirm the simulation is physically valid.

#### Acceptance Criteria

1. WHEN results are extracted THEN the system SHALL be able to calculate total inlet molar flow and total outlet molar flow
2. WHEN mass balance is checked THEN the relative error SHALL be < 1% (|inlet - outlet| / inlet < 0.01)
3. WHEN mass balance is checked per component THEN each component's inlet moles SHALL equal outlet moles within 1% relative error
4. WHEN mass balance fails (error > 1%) THEN the system SHALL report this as a validation warning (not failure - may indicate solver issues)
5. WHEN mass balance validation is performed THEN the system SHALL return the actual relative error percentage

### Requirement 5: Separator Performance Metrics

**User Story:** As a simulation developer, I want to retrieve performance metrics from the three-phase separator, so that I can analyze the separation efficiency.

#### Acceptance Criteria

1. WHEN the solver converges THEN the system SHALL be able to retrieve separator-specific metrics:
   - Actual pressure drop across separator (Pa)
   - Separation efficiency or split fractions (if available)
2. WHEN separator metrics are queried THEN values SHALL be consistent with outlet stream properties
3. WHEN a metric is not available in DWSIM THEN the system SHALL return null/not-available with clear indication (not throw exception)

### Requirement 6: Calculation Error and Warning Capture

**User Story:** As a simulation developer, I want to capture all errors, warnings, and diagnostic messages from the calculation, so that I can troubleshoot issues and understand solver behavior.

#### Acceptance Criteria

1. WHEN the solver runs THEN the system SHALL capture all solver messages (info, warning, error levels)
2. WHEN calculation errors occur THEN the system SHALL capture:
   - Error type/code
   - Error message
   - Source unit operation (if identifiable)
   - Stack trace (for debugging, not exposed to end users)
3. WHEN warnings are generated (e.g., near-boundary conditions) THEN the system SHALL capture and return them separately from errors
4. WHEN the calculation completes (success or failure) THEN all captured messages SHALL be accessible via the result object
5. WHEN no errors or warnings occur THEN the message collections SHALL be empty (not null)

### Requirement 7: Calculation Timing and Performance

**User Story:** As a simulation developer, I want to measure calculation performance, so that I can monitor and optimize simulation execution.

#### Acceptance Criteria

1. WHEN the solver is invoked THEN the system SHALL measure and record:
   - Total wall-clock time (milliseconds)
   - Solver initialization time (if separable)
   - Actual calculation time
2. WHEN timing is queried THEN all timing values SHALL be non-negative
3. WHEN the calculation completes THEN timing information SHALL be included in the result object
4. WHEN a typical three-phase separator calculation runs THEN it SHALL complete in < 5 seconds (performance baseline)

## Non-Functional Requirements

### Code Architecture and Modularity

- **Single Responsibility Principle**:
  - Solver invocation logic shall be separate from result extraction
  - Mass balance validation shall be isolated in a dedicated validator class
  - Error/warning capture shall be separate from convergence status handling
  - Each adapter method shall handle one specific concern

- **Modular Design**:
  - Create `CalculationAdapter` for solver invocation and result extraction
  - Create `MassBalanceValidator` for mass balance checking
  - Create `CalculationResult` model to encapsulate all calculation outputs
  - Extend existing `StreamAdapter` with methods for extracting calculated properties

- **Dependency Management**:
  - CalculationAdapter depends on FlowsheetContext (from Spec 1.2)
  - CalculationAdapter uses StreamAdapter for property extraction
  - Result models are independent (pure data, no DWSIM dependencies)

- **Clear Interfaces**:
  - Define `ICalculationResult` interface for result data
  - Define clear method signatures for solver invocation
  - Document expected DWSIM API methods used

### Performance

- **Calculation Latency**:
  - Simple three-phase separator (4 compounds, 1 unit): < 5 seconds
  - Solver initialization: < 1 second
  - Result extraction (all streams): < 500 ms total
  - Mass balance validation: < 50 ms

- **Memory Footprint**:
  - Calculation should not increase memory beyond 100 MB additional
  - Results object should be < 10 KB for typical separator
  - No memory leaks after calculation completes

### Security

- **Input Validation**:
  - Verify flowsheet is properly configured before solver invocation
  - Validate that all required connections exist
  - Check property package is set before calculation

- **Resource Protection**:
  - Implement calculation timeout (configurable, default 60 seconds)
  - Detect infinite loops or non-terminating calculations
  - Clean up resources on timeout or cancellation

- **Error Isolation**:
  - Calculation errors shall not corrupt flowsheet state
  - Failed calculations shall be recoverable (can retry after fixing issues)
  - Exception handling shall not expose internal implementation details

### Reliability

- **Deterministic Behavior**:
  - Running the same calculation with the same inputs SHALL produce the same results
  - Convergence behavior SHALL be reproducible
  - Result extraction SHALL return consistent values on repeated calls

- **Error Recovery**:
  - Failed calculations SHALL leave flowsheet in usable state
  - Partial results SHALL NOT be returned as if complete
  - System SHALL clearly distinguish between "not converged" and "error"

- **Resource Cleanup**:
  - Calculation resources SHALL be released after completion
  - Timeout handlers SHALL properly clean up
  - No file handles or memory leaks after calculation

### Usability

- **Clear Result Interpretation**:
  - CalculationResult SHALL clearly indicate success/failure/convergence
  - Error messages SHALL suggest corrective actions where possible
  - Timing information SHALL use standard units (milliseconds)

- **Discoverability**:
  - Result object properties SHALL follow consistent naming
  - Outlet stream results SHALL be easily accessible by stream ID or port name
  - All numerical results SHALL include units in property names or documentation

- **Debugging Support**:
  - Solver messages SHALL be preserved for debugging
  - Timing breakdown SHALL help identify bottlenecks
  - Mass balance errors SHALL indicate which component failed

### Compatibility

- **DWSIM Version Compatibility**:
  - Code SHALL be compatible with DWSIM 6.x+ assemblies
  - Tested against DWSIM 8.x (latest stable)
  - Solver API differences between versions SHALL be documented

- **.NET Framework Requirements**:
  - Code SHALL target .NET Framework 4.8
  - All asynchronous patterns SHALL be compatible with .NET Framework
  - No .NET Core/.NET 5+ APIs SHALL be used

- **CAPE-OPEN Compliance**:
  - Result extraction SHALL use CAPE-OPEN GetProp methods where possible
  - Property names SHALL follow CAPE-OPEN conventions
  - Flash results SHALL map to CAPE-OPEN phase equilibrium interfaces

### Maintainability

- **Code Readability**:
  - Use descriptive variable names (e.g., `vaporOutletFlow`, `massBalanceError`)
  - Comment DWSIM solver API calls with expected behavior
  - Use constants for tolerance values (e.g., `MASS_BALANCE_TOLERANCE = 0.01`)

- **Test Coverage**:
  - Achieve > 80% code coverage for calculation logic
  - Include tests for:
    - Successful convergence with known inputs
    - Non-convergence scenarios
    - Error handling (invalid flowsheet)
    - Mass balance validation
    - Timing measurement accuracy

- **Documentation**:
  - Document all DWSIM solver API methods used
  - Provide inline comments for non-obvious solver behavior
  - Create README documenting the calculation workflow and expected results

## Success Criteria

This specification is considered complete when:

1. **All Functional Requirements Met**: Every acceptance criterion in Requirements 1-7 is satisfied with passing tests
2. **Calculation Completes Without Crashes**: Solver invocation does not throw unhandled exceptions for valid inputs
3. **Solver Converges**: Three-phase separator calculation converges for the standard test case
4. **Mass Balance Validated**: Outlet flows match inlet flow within 1% relative error
5. **Results Physically Reasonable**: Outlet temperatures, pressures, and compositions are physically valid
6. **Test Suite Green**: All unit and integration tests pass consistently
7. **Performance Targets Met**: Calculation completes within 5 seconds for standard test case
8. **Code Review Approved**: Code adheres to architectural guidelines in structure.md

## Out of Scope

The following are explicitly **not** in scope for this specification:

- **Multiple Unit Operations**: Only single three-phase separator is tested; complex flowsheets are covered in Spec 2.1
- **Dynamic Simulation**: Only steady-state calculation is in scope; dynamic simulation is covered in Spec 9.1
- **Optimization**: Parameter studies and optimization are covered in Spec 6.2
- **Session Management**: Multi-session support is covered in Spec 2.2
- **RPC Communication**: IPC and JSON-RPC is covered in Phase 3
- **MCP Tools**: MCP tool implementation is covered in Phase 5
- **Other Unit Operations**: Only three-phase separator is tested; other units will follow similar patterns
- **Flash Calculations**: Standalone flash calculations are covered in Spec 6.1
- **Energy Balance**: Only mass balance is validated; energy balance validation is future enhancement

## Validation Test Plan

The following test case demonstrates the complete workflow and serves as the primary validation:

**Test Case: Execute Three-Phase Separator Calculation with Hydrocarbon-Water Mixture**

**Setup (from Spec 1.2)**:
1. Create and initialize FlowsheetContext
2. Add compounds: Methane, Ethane, Propane, Water
3. Configure Peng-Robinson property package
4. Create inlet material stream:
   - Temperature: 298.15 K (25°C)
   - Pressure: 500000 Pa (5 bar)
   - Molar flow: 100 mol/s
   - Composition: 40% Methane, 30% Ethane, 20% Propane, 10% Water (mole fractions)
5. Create three outlet material streams (vapor, liquid, water)
6. Add three-phase separator with pressure drop: 5000 Pa
7. Connect all streams to separator

**Calculation Execution (This Spec)**:
1. Invoke flowsheet solver via CalculationAdapter
2. Capture calculation timing
3. Check convergence status
4. Extract vapor outlet properties (temperature, pressure, flow, composition)
5. Extract liquid outlet properties (temperature, pressure, flow, composition)
6. Extract water outlet properties (temperature, pressure, flow, composition)
7. Calculate total outlet molar flow
8. Validate mass balance: |inlet - outlet| / inlet < 0.01
9. Validate per-component mass balance
10. Retrieve separator performance metrics
11. Capture any warnings or messages

**Expected Outcomes**:
- Solver converges successfully
- Calculation time < 5 seconds
- Outlet pressure = 500000 - 5000 = 495000 Pa (within tolerance)
- Total outlet molar flow ≈ 100 mol/s (mass balance)
- Vapor outlet contains primarily light hydrocarbons (methane, ethane)
- Liquid outlet contains primarily propane with some dissolved gases
- Water outlet contains primarily water with trace hydrocarbons
- Mass balance error < 1%
- No errors, possibly some informational warnings

**Golden Values** (approximate, for validation):
- Vapor flow: 60-80 mol/s (most of gas-phase hydrocarbons)
- Liquid flow: 10-30 mol/s (liquid hydrocarbons)
- Water flow: 5-15 mol/s (mostly water)
- Vapor composition: >50% methane
- Water outlet: >90% water
