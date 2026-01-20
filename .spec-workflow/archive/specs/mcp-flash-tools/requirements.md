# Requirements Document: Thermodynamic Flash Calculation Tools

## Introduction

This specification defines the requirements for MCP tools that expose standalone thermodynamic flash calculations (phase equilibrium) to LLM agents. Flash calculations are fundamental thermodynamic operations that determine how a mixture separates into phases (vapor, liquid, aqueous) at given conditions without requiring a full flowsheet simulation.

These tools enable property calculations, phase equilibrium predictions, and thermodynamic analysis directly through the MCP interface, providing faster results for point calculations compared to full flowsheet simulations.

## Alignment with Product Vision

This feature directly supports several key product goals from product.md:

1. **Comprehensive Tool Taxonomy**: Adds the thermodynamics tools (`flash_tp`, `flash_ph`, `flash_ps`) to the MCP server's toolset, filling a critical gap for standalone property calculations.

2. **CAPE-OPEN Domain Model**: Integrates with DWSIM's `ICapeThermoEquilibriumServer` and `ICapeThermoEquilibriumRoutine` interfaces, maintaining the CAPE-OPEN standard as the primary data model.

3. **Composable Operations**: Flash tools perform single, well-defined thermodynamic calculations that can be composed with other tools or used independently for quick property lookups.

4. **LLM-Friendly Interface**: Provides structured inputs/outputs with standardized CAPE-OPEN terminology that LLMs can reason about effectively.

5. **Performance**: Enables sub-second property calculations without the overhead of flowsheet setup and solving.

## Requirements

### Requirement 1: Temperature-Pressure Flash Calculation (flash_tp)

**User Story:** As an LLM agent building a chemical process, I want to perform a temperature-pressure flash calculation on a mixture, so that I can determine phase distribution and properties at specified conditions without constructing a full flowsheet.

#### Acceptance Criteria

1. WHEN the `flash_tp` tool is called with valid sessionId, compounds, composition, temperature, and pressure THEN the system SHALL return phase fractions, phase compositions, and phase properties for each equilibrium phase.

2. IF the composition array length does not match the compounds array length THEN the system SHALL return a validation error with clear message indicating the mismatch.

3. IF the specified property package is not configured for the session THEN the system SHALL return an error indicating property package must be set first.

4. WHEN the flash calculation converges successfully THEN the system SHALL return:
   - Number of phases present (vapor, liquid, aqueous)
   - Mole fraction of each phase
   - Molar composition of each phase
   - Key properties for each phase (density, molecular weight, enthalpy, entropy)

5. IF the flash calculation does not converge THEN the system SHALL return an error with convergence diagnostic information (iteration count, residual error).

6. WHEN temperature is provided THEN the system SHALL accept values in Kelvin (SI unit) and validate that temperature > 0 K.

7. WHEN pressure is provided THEN the system SHALL accept values in Pascals (SI unit) and validate that pressure > 0 Pa.

8. WHEN the tool is called THEN the system SHALL complete the calculation in less than 1 second for simple mixtures (< 10 compounds).

### Requirement 2: Pressure-Enthalpy Flash Calculation (flash_ph)

**User Story:** As an LLM agent analyzing heat exchanger performance, I want to perform a pressure-enthalpy flash calculation, so that I can determine the outlet state when enthalpy and pressure are known.

#### Acceptance Criteria

1. WHEN the `flash_ph` tool is called with valid sessionId, compounds, composition, pressure, and enthalpy THEN the system SHALL return temperature, phase fractions, phase compositions, and phase properties.

2. IF the enthalpy value is outside the valid range for the mixture at the given pressure THEN the system SHALL return an error indicating the thermodynamic constraint violation.

3. WHEN the flash calculation converges successfully THEN the system SHALL return the equilibrium temperature in addition to phase information.

4. WHEN enthalpy is provided THEN the system SHALL accept values in J/mol (SI unit).

5. IF the input state results in a single phase THEN the system SHALL return that phase with 100% fraction and appropriate subcooled/superheated indicator.

### Requirement 3: Pressure-Entropy Flash Calculation (flash_ps)

**User Story:** As an LLM agent analyzing isentropic processes (turbines, compressors), I want to perform a pressure-entropy flash calculation, so that I can determine the outlet state for ideal expansion/compression.

#### Acceptance Criteria

1. WHEN the `flash_ps` tool is called with valid sessionId, compounds, composition, pressure, and entropy THEN the system SHALL return temperature, phase fractions, phase compositions, and phase properties.

2. IF the entropy value is outside the valid range for the mixture at the given pressure THEN the system SHALL return an error indicating the thermodynamic constraint violation.

3. WHEN the flash calculation converges successfully THEN the system SHALL return the equilibrium temperature in addition to phase information.

4. WHEN entropy is provided THEN the system SHALL accept values in J/(mol·K) (SI unit).

### Requirement 4: Property Calculations on Flash Results

**User Story:** As an LLM agent, I want to retrieve detailed thermodynamic properties from flash results, so that I can perform engineering calculations and analysis.

#### Acceptance Criteria

1. WHEN a flash calculation succeeds THEN the system SHALL provide the following properties for each phase:
   - Density (kg/m³)
   - Molecular weight (kg/mol)
   - Viscosity (Pa·s)
   - Thermal conductivity (W/(m·K))
   - Enthalpy (J/mol)
   - Entropy (J/(mol·K))
   - Gibbs energy (J/mol)
   - Compressibility factor (Z)

2. IF a specific property cannot be calculated for a phase THEN the system SHALL return null for that property with a warning message rather than failing the entire calculation.

3. WHEN requesting vapor phase properties THEN the system SHALL also include:
   - Vapor pressure at the temperature (Pa)
   - Heat capacity at constant pressure (J/(mol·K))
   - Heat capacity at constant volume (J/(mol·K))

4. WHEN requesting liquid phase properties THEN the system SHALL also include:
   - Surface tension (N/m) when applicable
   - Bubble point temperature (K) at the pressure
   - Dew point temperature (K) at the pressure

### Requirement 5: Session and Property Package Integration

**User Story:** As an LLM agent, I want flash calculations to use the session's configured property package and compounds, so that calculations are consistent with my simulation context.

#### Acceptance Criteria

1. WHEN a flash tool is called THEN the system SHALL use the property package configured via `set_property_package` for that session.

2. IF no property package has been set for the session THEN the system SHALL return an error with message "Property package must be configured before flash calculations. Use set_property_package tool."

3. WHEN compounds are specified in the flash request THEN the system SHALL validate that all compounds exist in the DWSIM compound database.

4. IF a compound name is not found in the database THEN the system SHALL return an error listing the invalid compound and suggesting similar compound names if available.

5. WHEN composition is provided as mole fractions THEN the system SHALL validate that values sum to 1.0 (within tolerance of ±0.001).

6. IF composition does not sum to 1.0 THEN the system SHALL either normalize automatically (with warning) OR return validation error based on configuration.

### Requirement 6: CAPE-OPEN Interface Integration

**User Story:** As a developer maintaining the MCP server, I want flash calculations to use DWSIM's CAPE-OPEN interfaces, so that the implementation follows industry standards and enables future interoperability.

#### Acceptance Criteria

1. WHEN performing flash calculations THEN the system SHALL use DWSIM's `ICapeThermoEquilibriumServer` interface methods.

2. WHEN returning results THEN the system SHALL format properties according to CAPE-OPEN standard property names (e.g., "temperature", "pressure", "enthalpy", not DWSIM-specific names).

3. WHEN serializing flash results THEN the system SHALL produce JSON that is compatible with CAPE-OPEN data exchange format.

4. WHEN calling flash routines THEN the system SHALL use `ICapeThermoEquilibriumRoutine.CalcEquilibrium` method with appropriate flash specification.

## Non-Functional Requirements

### Code Architecture and Modularity

- **Single Responsibility Principle**: Flash tool implementations should be in dedicated files (`flash.py` for Python MCP tools, `FlashAdapter.cs` for C# DWSIM interaction).
- **Modular Design**: Flash calculations should be callable from both MCP tools and the Python service layer for reuse.
- **Dependency Management**: Flash tools should depend only on the existing session management and property package infrastructure.
- **Clear Interfaces**: Input/output DTOs should be well-defined Pydantic models for validation and type safety.

### Performance

- **Latency Target**: Single flash calculation should complete in < 1 second for mixtures with < 10 compounds.
- **Throughput**: System should support multiple concurrent flash calculations across different sessions.
- **Memory**: Flash calculations should not significantly increase session memory footprint beyond input/output data.

### Security

- **Input Validation**: All inputs (temperature, pressure, composition) must be validated for reasonable physical ranges.
- **Resource Limits**: Flash calculations should respect session timeout limits configured in the server.
- **No File System Access**: Flash tools should not write to or read from the file system.

### Reliability

- **Convergence Handling**: Non-converging calculations should return informative errors, not crash.
- **Graceful Degradation**: If specific properties cannot be calculated, return partial results with warnings.
- **Error Recovery**: Failed flash calculations should not corrupt session state or affect other operations.

### Usability

- **Clear Error Messages**: All errors should include actionable guidance (e.g., "Temperature must be positive. Provided: -100 K").
- **LLM-Friendly Descriptions**: Tool descriptions should be comprehensive enough for LLM agents to use correctly without prior training.
- **Consistent Units**: All inputs and outputs use SI units with clear documentation.
- **Example Values**: Tool schemas should include example values for each parameter.

### Observability

- **Logging**: Flash calculations should log input parameters, calculation time, and convergence status.
- **Tracing**: Flash tool calls should be included in OpenTelemetry traces with timing spans.
- **Metrics**: Track flash calculation count, success rate, and latency distribution.
