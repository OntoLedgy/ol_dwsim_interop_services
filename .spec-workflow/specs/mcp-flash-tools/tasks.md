# Tasks Document: Thermodynamic Flash Calculation Tools

## Phase 1: Python Enums and Data Models

- [x] 1.1. Create flash calculation type enums in models/enums/flash_calculation_types.py
  - File: mcp_service/server/dwsim_mcp_server/models/enums/flash_calculation_types.py
  - Define `FlashCalculationTypes` enum with values: TEMPERATURE_PRESSURE, PRESSURE_ENTHALPY, PRESSURE_ENTROPY, TEMPERATURE_VAPOR_FRACTION, PRESSURE_VAPOR_FRACTION
  - Follow project naming conventions (PascalCase for enum class, str Enum pattern)
  - Purpose: Standardize flash calculation type identifiers across Python/C# layers
  - _Leverage: Existing enum patterns in mcp_service/server/dwsim_mcp_server/models/_
  - _Requirements: 1, 2, 3_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer specializing in type systems and enums | Task: Create FlashCalculationTypes enum following the design document specification for flash_calculation_types.py. Check if models/enums/ directory exists, create if needed. | Restrictions: Must use str, Enum pattern for JSON serialization compatibility, follow existing enum naming conventions | _Leverage: Existing enum patterns in the models directory | _Requirements: 1, 2, 3 | Success: Enum compiles, imports correctly, all five flash types defined | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 1.2. Create phase type enums in models/enums/phase_types.py
  - File: mcp_service/server/dwsim_mcp_server/models/enums/phase_types.py
  - Define `PhaseTypes` enum with values: VAPOR, LIQUID, LIQUID2, AQUEOUS, SOLID
  - Purpose: Standardize thermodynamic phase identifiers
  - _Leverage: Existing enum patterns in mcp_service/server/dwsim_mcp_server/models/_
  - _Requirements: 1.4, 4_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Create PhaseTypes enum following design document for phase_types.py | Restrictions: Must use str, Enum pattern, phase names must match DWSIM conventions | _Leverage: Existing enum patterns | _Requirements: 1.4, 4 | Success: Enum defines all five phase types correctly | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 1.3. Create physical quantity type enums in models/enums/physical_quantity_types.py
  - File: mcp_service/server/dwsim_mcp_server/models/enums/physical_quantity_types.py
  - Define `PhysicalQuantityTypes` enum mirroring C# PhysicalQuantities hierarchy
  - Include: TEMPERATURE, PRESSURE, MOLAR_ENTHALPY, MOLAR_ENTROPY, DENSITY, DYNAMIC_VISCOSITY, THERMAL_CONDUCTIVITY, MOLAR_HEAT_CAPACITY_CP, MOLAR_HEAT_CAPACITY_CV, MOLECULAR_WEIGHT, COMPRESSIBILITY_FACTOR, GIBBS_ENERGY, SURFACE_TENSION, MOLAR_FLOW_RATE, MASS_FLOW_RATE, VOLUMETRIC_FLOW_RATE
  - Purpose: Python mirror of C# PhysicalQuantities for type-safe property handling
  - _Leverage: C# PhysicalQuantities in DwsimWorker/Models/_
  - _Requirements: 4_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Create PhysicalQuantityTypes enum mirroring C# PhysicalQuantities hierarchy as specified in design document | Restrictions: Must align with C# naming, use str Enum pattern | _Leverage: C# PhysicalQuantities classes | _Requirements: 4 | Success: All 16 quantity types defined, names match C# equivalents | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 1.4. Create unit symbol enums in models/enums/unit_symbols.py
  - File: mcp_service/server/dwsim_mcp_server/models/enums/unit_symbols.py
  - Define separate enum classes: `TemperatureUnits`, `PressureUnits`, `MolarEnergyUnits`, `MolarEntropyUnits`, `DensityUnits`, `ViscosityUnits`
  - Each enum defines standard unit symbols (K, Pa, J/mol, etc.)
  - Purpose: Type-safe unit handling with SI units as defaults
  - _Leverage: C# UnitsOfMeasure patterns in DwsimWorker/Models/_
  - _Requirements: 1.6, 1.7, 2.4, 3.4_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Create unit symbol enums (TemperatureUnits, PressureUnits, MolarEnergyUnits, MolarEntropyUnits, DensityUnits, ViscosityUnits) as specified in design document | Restrictions: Unit symbols must match SI conventions, use str Enum pattern | _Leverage: C# UnitsOfMeasure patterns | _Requirements: 1.6, 1.7, 2.4, 3.4 | Success: All six unit enum classes defined with correct symbols | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

## Phase 2: Python Measurement Models

- [x] 2.1. Create ranges model in models/measurements/ranges.py
  - File: mcp_service/server/dwsim_mcp_server/models/measurements/ranges.py
  - Create `Ranges` Pydantic model with min_value, max_value fields
  - Add `contains(value)` method for validation
  - Mirrors C# DwsimWorker.Models.Ranges struct
  - Purpose: Range validation for physical quantity measurements
  - _Leverage: Pydantic BaseModel patterns in existing models_
  - _Requirements: 1.6, 1.7, 5.5_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Create Ranges Pydantic model as specified in design document for measurement range validation. Create models/measurements/ directory if needed. | Restrictions: Must mirror C# Ranges struct, use Pydantic v2 syntax | _Leverage: Existing Pydantic models | _Requirements: 1.6, 1.7, 5.5 | Success: Ranges model validates correctly, contains() method works | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 2.2. Create units of measure model in models/measurements/units_of_measure.py
  - File: mcp_service/server/dwsim_mcp_server/models/measurements/units_of_measure.py
  - Create `UnitsOfMeasure` Pydantic model with unit_name, quantity_type, valid_range fields
  - Add `is_value_valid(value)` method
  - Mirrors C# DwsimWorker.Models.UnitsOfMeasure class
  - Purpose: Link units to physical quantities with range validation
  - _Leverage: PhysicalQuantityTypes enum, Ranges model_
  - _Requirements: 1.6, 1.7, 5.5_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Create UnitsOfMeasure Pydantic model linking units to physical quantities as specified in design document | Restrictions: Must import from PhysicalQuantityTypes and Ranges, mirror C# class | _Leverage: PhysicalQuantityTypes enum, Ranges model | _Requirements: 1.6, 1.7, 5.5 | Success: UnitsOfMeasure validates quantity-unit compatibility | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 2.3. Create measurements model in models/measurements/measurements.py
  - File: mcp_service/server/dwsim_mcp_server/models/measurements/measurements.py
  - Create `Measurements` Pydantic model with quantity_type, value, unit fields
  - Add field_validator to ensure unit's quantity_type matches measurement's quantity_type
  - Mirrors C# DwsimWorker.Models.Measurements class
  - Purpose: Core measurement class combining value + quantity + unit
  - _Leverage: PhysicalQuantityTypes enum, UnitsOfMeasure model_
  - _Requirements: 1.6, 1.7, 2.4, 3.4_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Create Measurements Pydantic model with cross-field validation as specified in design document | Restrictions: Must validate unit-quantity compatibility, use Pydantic v2 field_validator | _Leverage: PhysicalQuantityTypes, UnitsOfMeasure | _Requirements: 1.6, 1.7, 2.4, 3.4 | Success: Measurements validates unit-quantity match, rejects mismatches | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 2.4. Create physical properties model in models/measurements/physical_properties.py
  - File: mcp_service/server/dwsim_mcp_server/models/measurements/physical_properties.py
  - Create `PhysicalProperties` Pydantic model with name, measurement fields
  - Add value and unit_name properties for convenience access
  - Mirrors C# DwsimWorker.Models.PhysicalProperties class
  - Purpose: Named property container for flash results
  - _Leverage: Measurements model_
  - _Requirements: 4_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Create PhysicalProperties Pydantic model for named properties as specified in design document | Restrictions: Must use Optional for measurement, add convenience properties | _Leverage: Measurements model | _Requirements: 4 | Success: PhysicalProperties provides easy access to value and unit | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

## Phase 3: Flash Input/Output Models

- [x] 3.1. Create flash input models in models/mcp_inputs/flash_inputs.py
  - File: mcp_service/server/dwsim_mcp_server/models/mcp_inputs/flash_inputs.py
  - Create `FlashTPInputs`, `FlashPHInputs`, `FlashPSInputs` Pydantic models
  - Add composition sum validator (must sum to 1.0 ± 0.001)
  - Add Field descriptions for MCP schema generation
  - Purpose: Validated input models for flash MCP tools
  - _Leverage: Measurements model, existing FlashStreamInput pattern_
  - _Requirements: 1.1, 1.2, 2.1, 3.1, 5.5_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Create FlashTPInputs, FlashPHInputs, FlashPSInputs Pydantic models with validation as specified in design document | Restrictions: Must validate composition sum, use Field for descriptions, follow existing mcp_inputs patterns | _Leverage: Measurements model, existing FlashStreamInput | _Requirements: 1.1, 1.2, 2.1, 3.1, 5.5 | Success: All three input models validate correctly, reject invalid composition | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 3.2. Create flash result models in models/responses/flash_results.py
  - File: mcp_service/server/dwsim_mcp_server/models/responses/flash_results.py
  - Create `PhaseResults` model with phase_type, fraction, composition, properties fields
  - Create `FlashResults` model with calculation_type, temperature, pressure, converged, phases, message fields
  - Use PhaseTypes and FlashCalculationTypes enums
  - Purpose: Structured output models for flash calculation results
  - _Leverage: PhaseTypes, FlashCalculationTypes enums, PhysicalProperties model_
  - _Requirements: 1.4, 4.1, 4.2_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Create PhaseResults and FlashResults Pydantic models as specified in design document | Restrictions: Must use enums for type safety, include convergence info | _Leverage: PhaseTypes, FlashCalculationTypes enums, PhysicalProperties | _Requirements: 1.4, 4.1, 4.2 | Success: Result models serialize to JSON matching expected schema | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

## Phase 4: C# Physical Quantities

- [x] 4.1. Create MolarEnthalpy physical quantity class
  - File: mcp_service/dwsim_worker/DwsimWorker/Models/MolarEnthalpy.cs
  - Create sealed class `MolarEnthalpy` extending `PhysicalQuantities`
  - Override QuantityName property to return "MolarEnthalpy"
  - Purpose: Support enthalpy measurements in flash calculations
  - _Leverage: Existing PhysicalQuantities base class pattern_
  - _Requirements: 2, 4.1_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create MolarEnthalpy sealed class extending PhysicalQuantities as specified in design document | Restrictions: Must follow existing PhysicalQuantities pattern exactly, sealed class | _Leverage: Existing PhysicalQuantities base class | _Requirements: 2, 4.1 | Success: Class compiles, inherits correctly from PhysicalQuantities | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 4.2. Create MolarEntropy physical quantity class
  - File: mcp_service/dwsim_worker/DwsimWorker/Models/MolarEntropy.cs
  - Create sealed class `MolarEntropy` extending `PhysicalQuantities`
  - Override QuantityName property to return "MolarEntropy"
  - Purpose: Support entropy measurements in flash calculations
  - _Leverage: Existing PhysicalQuantities base class pattern_
  - _Requirements: 3, 4.1_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create MolarEntropy sealed class extending PhysicalQuantities as specified in design document | Restrictions: Must follow existing PhysicalQuantities pattern exactly, sealed class | _Leverage: Existing PhysicalQuantities base class | _Requirements: 3, 4.1 | Success: Class compiles, inherits correctly from PhysicalQuantities | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 4.3. Create Density physical quantity class
  - File: mcp_service/dwsim_worker/DwsimWorker/Models/Density.cs
  - Create sealed class `Density` extending `PhysicalQuantities`
  - Override QuantityName property to return "Density"
  - Purpose: Support density measurements in phase properties
  - _Leverage: Existing PhysicalQuantities base class pattern_
  - _Requirements: 4.1_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create Density sealed class extending PhysicalQuantities as specified in design document | Restrictions: Must follow existing PhysicalQuantities pattern exactly | _Leverage: Existing PhysicalQuantities base class | _Requirements: 4.1 | Success: Class compiles, inherits correctly | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 4.4. Create DynamicViscosity physical quantity class
  - File: mcp_service/dwsim_worker/DwsimWorker/Models/DynamicViscosity.cs
  - Create sealed class `DynamicViscosity` extending `PhysicalQuantities`
  - Override QuantityName property to return "DynamicViscosity"
  - Purpose: Support viscosity measurements in phase properties
  - _Leverage: Existing PhysicalQuantities base class pattern_
  - _Requirements: 4.1_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create DynamicViscosity sealed class extending PhysicalQuantities as specified in design document | Restrictions: Must follow existing PhysicalQuantities pattern exactly | _Leverage: Existing PhysicalQuantities base class | _Requirements: 4.1 | Success: Class compiles, inherits correctly | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 4.5. Create ThermalConductivity physical quantity class
  - File: mcp_service/dwsim_worker/DwsimWorker/Models/ThermalConductivity.cs
  - Create sealed class `ThermalConductivity` extending `PhysicalQuantities`
  - Override QuantityName property to return "ThermalConductivity"
  - Purpose: Support thermal conductivity measurements in phase properties
  - _Leverage: Existing PhysicalQuantities base class pattern_
  - _Requirements: 4.1_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create ThermalConductivity sealed class extending PhysicalQuantities as specified in design document | Restrictions: Must follow existing PhysicalQuantities pattern exactly | _Leverage: Existing PhysicalQuantities base class | _Requirements: 4.1 | Success: Class compiles, inherits correctly | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 4.6. Create MolarHeatCapacity physical quantity class
  - File: mcp_service/dwsim_worker/DwsimWorker/Models/MolarHeatCapacity.cs
  - Create sealed class `MolarHeatCapacity` extending `PhysicalQuantities`
  - Override QuantityName property to return "MolarHeatCapacity"
  - Purpose: Support heat capacity (Cp, Cv) measurements in phase properties
  - _Leverage: Existing PhysicalQuantities base class pattern_
  - _Requirements: 4.3_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create MolarHeatCapacity sealed class extending PhysicalQuantities as specified in design document | Restrictions: Must follow existing PhysicalQuantities pattern exactly | _Leverage: Existing PhysicalQuantities base class | _Requirements: 4.3 | Success: Class compiles, inherits correctly | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

## Phase 5: C# ThermodynamicsAdapter

- [x] 5.1. Create ThermodynamicsAdapter class with FlashTP method
  - File: mcp_service/dwsim_worker/DwsimWorker/Adapters/ThermodynamicsAdapter.cs
  - Create `ThermodynamicsAdapter` class following existing adapter patterns
  - Implement `FlashTP(compounds, composition, temperature, pressure)` method returning `FlashResultDto`
  - Use FlowsheetContext for session state access
  - Leverage PropertyPackageAdapter for property package access
  - Purpose: C# adapter for DWSIM temperature-pressure flash calculations
  - _Leverage: StreamAdapter.FlashStream() patterns, PropertyPackageAdapter, CapeOpenConverter_
  - _Requirements: 1, 6_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create ThermodynamicsAdapter class with FlashTP method following existing adapter patterns as specified in design document | Restrictions: Must use FlowsheetContext, follow adapter patterns from StreamAdapter, use CapeOpenConverter for results | _Leverage: StreamAdapter.FlashStream(), PropertyPackageAdapter, CapeOpenConverter | _Requirements: 1, 6 | Success: FlashTP compiles, follows adapter patterns, returns FlashResultDto | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 5.2. Add FlashPH method to ThermodynamicsAdapter
  - File: mcp_service/dwsim_worker/DwsimWorker/Adapters/ThermodynamicsAdapter.cs (continue)
  - Implement `FlashPH(compounds, composition, pressure, enthalpy)` method returning `FlashResultDto`
  - Handle enthalpy flash specification via DWSIM API
  - Purpose: Support pressure-enthalpy flash calculations
  - _Leverage: FlashTP implementation patterns, DWSIM CalcEquilibrium API_
  - _Requirements: 2, 6_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Add FlashPH method to ThermodynamicsAdapter following FlashTP pattern | Restrictions: Must reuse patterns from FlashTP, handle enthalpy specification correctly | _Leverage: FlashTP implementation, DWSIM API | _Requirements: 2, 6 | Success: FlashPH compiles, returns correct results for PH flash | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 5.3. Add FlashPS method to ThermodynamicsAdapter
  - File: mcp_service/dwsim_worker/DwsimWorker/Adapters/ThermodynamicsAdapter.cs (continue)
  - Implement `FlashPS(compounds, composition, pressure, entropy)` method returning `FlashResultDto`
  - Handle entropy flash specification via DWSIM API
  - Purpose: Support pressure-entropy flash calculations
  - _Leverage: FlashTP/FlashPH implementation patterns, DWSIM CalcEquilibrium API_
  - _Requirements: 3, 6_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Add FlashPS method to ThermodynamicsAdapter following FlashTP/FlashPH patterns | Restrictions: Must reuse patterns from FlashTP/PH, handle entropy specification correctly | _Leverage: FlashTP/PH implementations, DWSIM API | _Requirements: 3, 6 | Success: FlashPS compiles, returns correct results for PS flash | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

## Phase 6: Python ThermodynamicsService

- [x] 6.1. Create ThermodynamicsService class with flash_tp method
  - File: mcp_service/server/dwsim_mcp_server/services/thermodynamics_service.py
  - Create `ThermodynamicsService` class following FlowsheetService patterns
  - Implement `flash_tp(payload: FlashTPInputs) -> FlashResults` method
  - Use LimitedSessionClient for session operations
  - Bridge to ThermodynamicsAdapter via pythonnet
  - Purpose: Python service layer for temperature-pressure flash calculations
  - _Leverage: FlowsheetService patterns, LimitedSessionClient, pythonnet bridge_
  - _Requirements: 1, 5_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Create ThermodynamicsService class with flash_tp method following FlowsheetService patterns as specified in design document | Restrictions: Must use LimitedSessionClient, follow service layer patterns, handle pythonnet bridge correctly | _Leverage: FlowsheetService, LimitedSessionClient | _Requirements: 1, 5 | Success: flash_tp method works end-to-end through pythonnet to DWSIM | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 6.2. Add flash_ph method to ThermodynamicsService
  - File: mcp_service/server/dwsim_mcp_server/services/thermodynamics_service.py (continue)
  - Implement `flash_ph(payload: FlashPHInputs) -> FlashResults` method
  - Bridge to ThermodynamicsAdapter.FlashPH via pythonnet
  - Purpose: Python service layer for pressure-enthalpy flash calculations
  - _Leverage: flash_tp implementation patterns_
  - _Requirements: 2, 5_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Add flash_ph method to ThermodynamicsService following flash_tp pattern | Restrictions: Must reuse patterns from flash_tp | _Leverage: flash_tp implementation | _Requirements: 2, 5 | Success: flash_ph method works end-to-end | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 6.3. Add flash_ps method to ThermodynamicsService
  - File: mcp_service/server/dwsim_mcp_server/services/thermodynamics_service.py (continue)
  - Implement `flash_ps(payload: FlashPSInputs) -> FlashResults` method
  - Bridge to ThermodynamicsAdapter.FlashPS via pythonnet
  - Purpose: Python service layer for pressure-entropy flash calculations
  - _Leverage: flash_tp/flash_ph implementation patterns_
  - _Requirements: 3, 5_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Add flash_ps method to ThermodynamicsService following flash_tp/flash_ph patterns | Restrictions: Must reuse patterns from flash_tp/ph | _Leverage: flash_tp/ph implementations | _Requirements: 3, 5 | Success: flash_ps method works end-to-end | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

## Phase 7: MCP Tools

- [x] 7.1. Create analysis.py with flash_tp MCP tool
  - File: mcp_service/server/dwsim_mcp_server/tools/analysis.py
  - Create `build_analysis_tools()` function returning list of Tool definitions
  - Define `flash_tp` tool with proper MCP schema
  - Create `handle_analysis_tool()` dispatcher function
  - Purpose: Expose flash_tp as MCP tool for LLM agents
  - _Leverage: simulation.py tool patterns, ThermodynamicsService_
  - _Requirements: 1, NFR (LLM-Friendly)_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Create analysis.py with build_analysis_tools() and handle_analysis_tool() following simulation.py patterns, define flash_tp tool | Restrictions: Must follow MCP tool definition patterns, include comprehensive descriptions for LLM agents | _Leverage: simulation.py patterns, ThermodynamicsService | _Requirements: 1, NFR (LLM-Friendly) | Success: flash_tp tool appears in MCP tool list, handles calls correctly | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 7.2. Add flash_ph tool to analysis.py
  - File: mcp_service/server/dwsim_mcp_server/tools/analysis.py (continue)
  - Add `flash_ph` tool definition to build_analysis_tools()
  - Wire flash_ph calls to ThermodynamicsService in handle_analysis_tool()
  - Purpose: Expose flash_ph as MCP tool
  - _Leverage: flash_tp tool pattern_
  - _Requirements: 2_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Add flash_ph tool definition to analysis.py following flash_tp pattern | Restrictions: Must follow same patterns as flash_tp | _Leverage: flash_tp tool implementation | _Requirements: 2 | Success: flash_ph tool appears in tool list and works correctly | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 7.3. Add flash_ps tool to analysis.py
  - File: mcp_service/server/dwsim_mcp_server/tools/analysis.py (continue)
  - Add `flash_ps` tool definition to build_analysis_tools()
  - Wire flash_ps calls to ThermodynamicsService in handle_analysis_tool()
  - Purpose: Expose flash_ps as MCP tool
  - _Leverage: flash_tp/flash_ph tool patterns_
  - _Requirements: 3_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Add flash_ps tool definition to analysis.py following flash_tp/ph patterns | Restrictions: Must follow same patterns as flash_tp/ph | _Leverage: flash_tp/ph tool implementations | _Requirements: 3 | Success: flash_ps tool appears in tool list and works correctly | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 7.4. Register analysis tools in MCP server
  - File: mcp_service/server/dwsim_mcp_server/server.py (modify existing)
  - Import build_analysis_tools and handle_analysis_tool from analysis.py
  - Add analysis tools to server tool list
  - Wire analysis tool handling in server dispatcher
  - Purpose: Make flash tools available through MCP protocol
  - _Leverage: Existing tool registration patterns in server.py_
  - _Requirements: 1, 2, 3_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Python Developer | Task: Register analysis tools in MCP server following existing tool registration patterns | Restrictions: Must not break existing tools, follow established patterns | _Leverage: Existing server.py tool registration | _Requirements: 1, 2, 3 | Success: All three flash tools discoverable and callable via MCP | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

## Phase 8: Unit Testing

- [x] 8.1. Create unit tests for Python enum models
  - File: mcp_service/server/tests/unit/test_flash_enums.py
  - Test all enum classes: FlashCalculationTypes, PhaseTypes, PhysicalQuantityTypes, unit enums
  - Verify string serialization works correctly
  - Purpose: Ensure enum models work correctly for JSON serialization
  - _Leverage: Existing test patterns in tests/unit/_
  - _Requirements: All_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer | Task: Create unit tests for all Python enum models | Restrictions: Must test string serialization, follow existing test patterns | _Leverage: Existing unit test patterns | _Requirements: All | Success: All enum tests pass, cover serialization | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 8.2. Create unit tests for Python measurement models
  - File: mcp_service/server/tests/unit/test_measurements.py
  - Test Ranges, UnitsOfMeasure, Measurements, PhysicalProperties models
  - Test validation logic (range checking, quantity-unit matching)
  - Purpose: Ensure measurement models validate correctly
  - _Leverage: Existing test patterns in tests/unit/_
  - _Requirements: 1.6, 1.7, 5.5_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer | Task: Create unit tests for measurement models testing validation logic | Restrictions: Must test both valid and invalid cases, cross-field validation | _Leverage: Existing test patterns | _Requirements: 1.6, 1.7, 5.5 | Success: All validation tests pass, edge cases covered | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 8.3. Create unit tests for flash input/output models
  - File: mcp_service/server/tests/unit/test_flash_models.py
  - Test FlashTPInputs, FlashPHInputs, FlashPSInputs validation
  - Test composition sum validation
  - Test FlashResults and PhaseResults serialization
  - Purpose: Ensure flash I/O models validate and serialize correctly
  - _Leverage: Existing test patterns in tests/unit/_
  - _Requirements: 1.1, 1.2, 2.1, 3.1, 5.5_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer | Task: Create unit tests for flash input/output models | Restrictions: Must test composition validation, result serialization | _Leverage: Existing test patterns | _Requirements: 1.1, 1.2, 2.1, 3.1, 5.5 | Success: All flash model tests pass | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 8.4. Create unit tests for C# ThermodynamicsAdapter
  - File: mcp_service/dwsim_worker/DwsimWorker.Tests/ThermodynamicsAdapterTests.cs
  - Test FlashTP, FlashPH, FlashPS with known test cases
  - Test error handling for non-converging cases
  - Purpose: Ensure C# adapter calculates correctly
  - _Leverage: Existing test patterns in DwsimWorker.Tests/_
  - _Requirements: 1, 2, 3, NFR (Convergence Handling)_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer | Task: Create unit tests for ThermodynamicsAdapter using known thermodynamic test cases | Restrictions: Must use real DWSIM assemblies, test golden cases from design document | _Leverage: Existing DwsimWorker.Tests patterns | _Requirements: 1, 2, 3, NFR | Success: All adapter tests pass with correct results | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

## Phase 9: Integration Testing

- [x] 9.1. Create integration tests for flash_tp workflow
  - File: mcp_service/server/tests/integration/test_flash_integration.py
  - Test complete workflow: create_session → set_property_package → add_compound → flash_tp
  - Use methane test case from design document
  - Verify round-trip through pythonnet to DWSIM and back
  - Purpose: Validate end-to-end flash_tp functionality
  - _Leverage: Existing integration test patterns, test_simulation_integration.py_
  - _Requirements: 1, 5_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Integration Engineer | Task: Create integration tests for flash_tp workflow | Restrictions: Must test full stack through pythonnet, use golden test cases | _Leverage: test_simulation_integration.py patterns | _Requirements: 1, 5 | Success: Integration tests pass, validate correct thermodynamic results | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 9.2. Add integration tests for flash_ph and flash_ps
  - File: mcp_service/server/tests/integration/test_flash_integration.py (continue)
  - Add test cases for flash_ph with water-steam transition
  - Add test cases for flash_ps with ideal gas behavior
  - Purpose: Validate all flash calculation types
  - _Leverage: flash_tp integration test patterns_
  - _Requirements: 2, 3_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Integration Engineer | Task: Add integration tests for flash_ph and flash_ps following flash_tp patterns | Restrictions: Must use appropriate test cases for each flash type | _Leverage: flash_tp integration test | _Requirements: 2, 3 | Success: All flash type integration tests pass | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_

- [x] 9.3. Add error handling integration tests
  - File: mcp_service/server/tests/integration/test_flash_integration.py (continue)
  - Test property package not configured error
  - Test invalid compound name error
  - Test composition mismatch error
  - Test non-convergence handling
  - Purpose: Validate error scenarios work correctly end-to-end
  - _Leverage: Error handling patterns from design document_
  - _Requirements: 5.2, 5.3, 5.4, NFR (Error Messages)_
  - _Prompt: Implement the task for spec mcp-flash-tools, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Integration Engineer | Task: Add error handling integration tests for all error scenarios in design document | Restrictions: Must test each error scenario independently | _Leverage: Error scenarios from design | _Requirements: 5.2, 5.3, 5.4, NFR | Success: All error scenarios return appropriate error messages | Instructions: Mark task [-] in tasks.md before starting, use log-implementation tool after completion with artifacts, then mark [x] when complete_
