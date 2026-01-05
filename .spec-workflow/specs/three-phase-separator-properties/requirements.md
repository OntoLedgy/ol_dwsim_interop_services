# Requirements Document

## Introduction

This specification implements basic property setting capabilities on a three-phase separator unit operation in DWSIM. Building on Spec 1.1 (dwsim-assembly-loader) which validated basic DWSIM assembly loading, this specification validates that we can configure DWSIM objects programmatically with realistic chemical engineering parameters.

The ability to set properties on DWSIM objects is foundational for all subsequent features. This capability enables the MCP server to accept simulation parameters from LLM agents and configure DWSIM flowsheets programmatically. Without this, agents cannot build or modify simulations.

This specification focuses specifically on a **three-phase separator** (vapor-liquid-liquid separator) which is a common unit operation in oil and gas processing. It tests the full property-setting workflow: compound selection, property package configuration, stream property specification, unit operation addition, and stream connections.

## Alignment with Product Vision

This specification directly supports several key aspects of the DWSIM MCP Server product vision outlined in product.md:

1. **Composable Operations**: Validates the atomic operations required for flowsheet building (add compounds, set property package, create streams, add units, connect streams). These operations will become individual MCP tools.

2. **CAPE-OPEN Domain Model**: Tests setting properties through CAPE-OPEN interfaces (ICapeThermoMaterialObject, ICapeThermoPropertyPackage), validating the CAPE-OPEN-as-domain-model architecture decision.

3. **Type Safety and Validation**: Validates that DWSIM objects accept and retain property values correctly, ensuring round-trip data integrity critical for LLM agent interactions.

4. **Fail-Fast Philosophy**: Positioned early in the specification plan to validate core assumptions about programmatic DWSIM configuration before building RPC infrastructure or MCP tools.

5. **Foundational for Phase 2+**: Enables subsequent specs:
   - Spec 1.3 (calculation execution) depends on properly configured separators
   - Spec 2.3 (CAPE-OPEN data mapping) requires validated property get/set patterns
   - Phase 5 (MCP tools) will expose these operations as tools

## Requirements

### Requirement 1: Compound Database Access and Selection

**User Story:** As a simulation developer, I want to programmatically add chemical compounds to a DWSIM flowsheet from the compound database, so that I can define the chemical system for my simulation.

#### Acceptance Criteria

1. WHEN the compound database is accessed THEN the system SHALL provide access to DWSIM's built-in compound library
2. WHEN a compound is added by name (e.g., "Methane", "Ethane", "Propane", "Water") THEN the system SHALL successfully add the compound to the flowsheet's compound list
3. WHEN multiple compounds are added THEN the system SHALL maintain all compounds in the flowsheet compound collection
4. WHEN an invalid compound name is provided THEN the system SHALL raise an appropriate exception with a clear error message
5. WHEN compounds are added THEN the system SHALL be able to retrieve the list of added compounds for verification

### Requirement 2: Property Package Configuration

**User Story:** As a simulation developer, I want to configure a thermodynamic property package for the flowsheet, so that DWSIM can calculate phase equilibria and thermophysical properties.

#### Acceptance Criteria

1. WHEN a property package is selected (e.g., "Peng-Robinson", "SRK", "NRTL") THEN the system SHALL successfully configure the flowsheet with that property package
2. WHEN the property package is set THEN the system SHALL be able to retrieve the configured property package name for verification (round-trip validation)
3. WHEN a property package is configured THEN it SHALL be associated with all material streams in the flowsheet
4. WHEN an unsupported property package name is provided THEN the system SHALL raise an appropriate exception

### Requirement 3: Material Stream Creation and Property Setting

**User Story:** As a simulation developer, I want to create material streams and set their thermodynamic properties, so that I can define the inlet conditions for my simulation.

#### Acceptance Criteria

1. WHEN a material stream is created THEN the system SHALL instantiate a DWSIM MaterialStream object (implementing ICapeThermoMaterialObject)
2. WHEN stream properties are set (temperature, pressure, molar flow, composition) THEN the system SHALL accept all standard SI unit values:
   - Temperature in Kelvin (K)
   - Pressure in Pascal (Pa)
   - Molar flow in mol/s
   - Composition as mole fractions (dimensionless, sum = 1.0)
3. WHEN stream properties are set THEN the system SHALL be able to retrieve all set properties with correct values (round-trip validation)
4. WHEN composition is set THEN the mole fractions SHALL correspond to the compounds added in Requirement 1, in the correct order
5. WHEN invalid property values are provided (e.g., negative temperature, composition sum ≠ 1.0) THEN the system SHALL raise appropriate validation exceptions
6. WHEN multiple streams are created (inlet, vapor outlet, liquid outlet, water outlet) THEN each stream SHALL maintain independent state

### Requirement 4: Three-Phase Separator Unit Operation Addition

**User Story:** As a simulation developer, I want to add a three-phase separator unit operation to the flowsheet, so that I can configure the separation equipment.

#### Acceptance Criteria

1. WHEN a three-phase separator is requested THEN the system SHALL instantiate a DWSIM three-phase separator unit operation object
2. WHEN the separator is added to the flowsheet THEN it SHALL be registered in the flowsheet's unit operation collection
3. WHEN the separator is created THEN it SHALL have standard inlet and outlet ports (1 inlet, 3 outlets: vapor, light liquid, heavy liquid/water)
4. WHEN separator operating parameters are set (e.g., pressure drop, separation efficiency) THEN the system SHALL accept and retain those values
5. WHEN separator properties are retrieved THEN the system SHALL return the configured values (round-trip validation)
6. WHEN the separator is queried THEN the system SHALL provide access to its properties and configuration

### Requirement 5: Stream Connection to Unit Operations

**User Story:** As a simulation developer, I want to connect material streams to the inlet and outlet ports of the three-phase separator, so that I can define the process topology.

#### Acceptance Criteria

1. WHEN an inlet stream is connected to the separator inlet port THEN the system SHALL establish the connection successfully
2. WHEN outlet streams are connected to the separator outlet ports (vapor, liquid, water) THEN the system SHALL establish all connections successfully
3. WHEN connections are established THEN the system SHALL be able to retrieve the connection topology for verification
4. WHEN a stream is connected to an invalid port THEN the system SHALL raise an appropriate exception
5. WHEN a port is already connected and a new connection is attempted THEN the system SHALL either replace the connection or raise an exception (document the behavior)
6. WHEN connections are queried THEN the system SHALL return source and target information for each connection

### Requirement 6: Flowsheet Consistency and State Validation

**User Story:** As a simulation developer, I want to validate that the configured flowsheet is in a consistent state before attempting calculations, so that I can detect configuration errors early.

#### Acceptance Criteria

1. WHEN all configuration steps are complete THEN the flowsheet SHALL have:
   - At least one compound defined
   - A property package configured
   - At least one material stream
   - At least one unit operation
   - Stream connections established
2. WHEN the flowsheet state is queried THEN the system SHALL return a representation of all objects (compounds, streams, units)
3. WHEN property values are retrieved after setting THEN they SHALL match the originally set values within acceptable floating-point tolerance (1e-6 for dimensionless, 1e-3 for temperatures, 1e-3 for pressures)
4. WHEN the separator is fully configured THEN no exceptions SHALL be raised during property get/set operations
5. WHEN the flowsheet is in a valid state THEN the system SHALL be ready for calculation (validated in Spec 1.3)

## Non-Functional Requirements

### Code Architecture and Modularity

- **Single Responsibility Principle**:
  - Compound management code shall be separate from property package configuration
  - Stream property setting shall be isolated from unit operation management
  - Connection logic shall be independent from object creation
  - Each test case shall test one specific capability

- **Modular Design**:
  - Use adapters to wrap DWSIM API calls (e.g., `FlowsheetAdapter`, `StreamAdapter`, `UnitOpAdapter`)
  - Extract property get/set logic into reusable helper methods
  - Create test fixtures for common setups (flowsheet with compounds, configured property package)

- **Dependency Management**:
  - Minimize direct DWSIM assembly coupling; use interfaces where possible
  - Isolate CAPE-OPEN interface usage to dedicated converter classes
  - Use dependency injection for adapters and helpers

- **Clear Interfaces**:
  - Define DTOs for property values (TemperatureValue, PressureValue, CompositionValue)
  - Establish clear contracts for property get/set operations
  - Document expected units and value ranges

### Performance

- **Property Setting Latency**:
  - Setting a single property (temperature, pressure, flow) SHALL complete in < 50ms
  - Setting full stream properties (temperature, pressure, flow, composition) SHALL complete in < 200ms
  - Adding a compound SHALL complete in < 100ms
  - Configuring a property package SHALL complete in < 500ms
  - Adding and connecting a unit operation SHALL complete in < 300ms

- **Memory Footprint**:
  - A configured flowsheet with 4 compounds, 4 streams, and 1 separator SHALL consume < 50MB of memory
  - Creating multiple flowsheets sequentially SHALL not cause memory leaks (verify with profiling)

### Security

- **Input Validation**:
  - All property values SHALL be validated for physical reasonableness before setting:
    - Temperature > 0 K and < 10000 K
    - Pressure > 0 Pa and < 1e9 Pa (10000 bar)
    - Molar flow ≥ 0 mol/s
    - Composition mole fractions ≥ 0 and ≤ 1, sum = 1.0 ± 1e-6
  - Compound names SHALL be sanitized (no special characters, max length 100)
  - Property package names SHALL be validated against a known list

- **Error Handling**:
  - All exceptions SHALL include descriptive error messages indicating the invalid value and valid range
  - No sensitive information SHALL be exposed in exception messages
  - Exceptions SHALL be typed (e.g., `InvalidTemperatureException`, `CompoundNotFoundException`)

### Reliability

- **Deterministic Behavior**:
  - Setting the same property values SHALL always produce the same flowsheet state
  - Property get/set operations SHALL be idempotent (setting the same value twice has no additional effect)
  - Object creation order SHALL not affect final configuration validity

- **Error Recovery**:
  - If a property set operation fails, the flowsheet SHALL remain in a consistent state (partial updates not allowed)
  - Failed operations SHALL not corrupt existing flowsheet data
  - Tests SHALL verify rollback behavior on errors

- **Resource Cleanup**:
  - Flowsheet objects SHALL be properly disposed after use to prevent resource leaks
  - All COM objects (if applicable) SHALL be released correctly
  - Test teardown SHALL verify no leaked handles or memory

### Usability

- **Clear Error Messages**:
  - Exception messages SHALL clearly indicate:
    - What operation failed (e.g., "Failed to set stream temperature")
    - What value was provided (e.g., "Provided: -50 K")
    - What the valid range is (e.g., "Valid range: 0 K to 10000 K")
    - Suggested corrective action (e.g., "Provide a positive temperature in Kelvin")

- **Discoverability**:
  - Property names SHALL follow CAPE-OPEN standard conventions (e.g., "temperature", "pressure", "vaporFraction")
  - Unit operation types SHALL use standard DWSIM names (e.g., "Separator", "Mixer", "Heater")
  - Compound names SHALL match DWSIM's compound database exactly

- **Testability**:
  - All property get/set operations SHALL be unit-testable with mock DWSIM objects (if feasible)
  - Integration tests SHALL cover the full workflow: compound addition → property package → stream creation → unit addition → connection
  - Golden tests SHALL validate against known-good property values

### Compatibility

- **DWSIM Version Compatibility**:
  - Code SHALL be compatible with DWSIM 6.x+ assemblies
  - Tested against DWSIM 8.x (latest stable as of 2024)
  - Property names and unit operation types SHALL be validated against the specific DWSIM version

- **.NET Framework Requirements**:
  - Code SHALL target .NET Framework 4.8
  - All dependencies SHALL be compatible with .NET Framework 4.8
  - No .NET Core/.NET 5+ APIs SHALL be used

- **CAPE-OPEN Compliance**:
  - Property get/set operations SHALL use CAPE-OPEN interfaces where possible (ICapeThermoMaterialObject.SetProp, GetProp)
  - Property names SHALL follow CAPE-OPEN 1.0/1.1 conventions
  - Unit conversions SHALL adhere to CAPE-OPEN unit standards

### Maintainability

- **Code Readability**:
  - Use descriptive variable names (e.g., `inletStream`, `separatorOutletVapor`, not `stream1`, `out1`)
  - Comment complex DWSIM API calls explaining parameters and expected behavior
  - Use constants for magic numbers (e.g., `const double ATMOSPHERIC_PRESSURE_PA = 101325.0`)

- **Test Coverage**:
  - Achieve > 80% code coverage for property setting logic
  - Include tests for:
    - Happy path (valid values)
    - Boundary conditions (min/max values)
    - Error cases (invalid values, null references)
    - Round-trip validation (set → get → verify)

- **Documentation**:
  - Document all DWSIM API methods used with XML comments
  - Provide inline comments for non-obvious CAPE-OPEN interface usage
  - Create a README documenting the test workflow and expected results

## Success Criteria

This specification is considered complete when:

1. **All Functional Requirements Met**: Every acceptance criterion in Requirements 1-6 is satisfied with passing tests
2. **Round-Trip Validation Passes**: All property values can be set and retrieved with values matching within acceptable tolerance
3. **No Initialization Errors**: Flowsheet, streams, and separator can be fully configured without exceptions
4. **Test Suite Green**: All unit and integration tests pass consistently
5. **Code Review Approved**: Code adheres to architectural guidelines in structure.md (one file per class, adapters for DWSIM API, etc.)
6. **Ready for Spec 1.3**: The configured separator is ready for calculation execution (validated in next spec)

## Out of Scope

The following are explicitly **not** in scope for this specification:

- **Calculation Execution**: Running the flowsheet solver is covered in Spec 1.3
- **Result Extraction**: Retrieving calculated properties from outlet streams is covered in Spec 1.3
- **Error Recovery**: Handling solver convergence failures is covered in Spec 1.3
- **Session Management**: Multi-session support is covered in Spec 2.2
- **Data Serialization**: DTO conversion and JSON serialization is covered in Spec 2.3
- **RPC Communication**: IPC and JSON-RPC is covered in Phase 3
- **MCP Tools**: MCP tool implementation is covered in Phase 5
- **Other Unit Operations**: This spec focuses on three-phase separator only; other units are covered in future specs
- **Dynamic Simulation**: Only steady-state configuration is in scope
- **Optimization**: Parameter studies and optimization are covered in Phase 6

## Validation Test Plan

The following test case demonstrates the complete workflow and serves as the primary validation:

**Test Case: Configure Three-Phase Separator with Hydrocarbon-Water Mixture**

1. Create a new DWSIM Flowsheet object
2. Add compounds to the flowsheet:
   - Methane (CH4)
   - Ethane (C2H6)
   - Propane (C3H8)
   - Water (H2O)
3. Configure Peng-Robinson property package
4. Create inlet material stream:
   - Temperature: 298.15 K (25°C)
   - Pressure: 500000 Pa (5 bar)
   - Molar flow: 100 mol/s
   - Composition: 40% Methane, 30% Ethane, 20% Propane, 10% Water (mole fractions)
5. Create three outlet material streams (vapor, liquid, water)
6. Add three-phase separator to flowsheet
7. Connect inlet stream to separator inlet
8. Connect outlet streams to separator outlets (vapor, liquid, water)
9. Set separator operating parameters:
   - Pressure drop: 5000 Pa (0.05 bar)
10. Verify all property values by retrieving and comparing (round-trip validation)
11. Assert no exceptions raised during configuration
12. Assert flowsheet state is valid and ready for calculation

**Expected Outcome**: All steps complete successfully, all round-trip validations pass, flowsheet ready for Spec 1.3 calculation execution.
