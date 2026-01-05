# Tasks Document

## Overview

This document breaks down the Spec 1.2 (three-phase-separator-properties) design into atomic implementation tasks. Each task produces testable deliverables and includes a detailed prompt for implementation guidance.

Tasks are ordered for logical implementation progression:
1. Foundation (models, exceptions, configuration)
2. Core engine (FlowsheetContext)
3. Adapters (compound, property package, stream, unit operation, connection)
4. Integration tests (golden workflow)
5. Performance validation

---

## Phase 1: Foundation

- [x] 1. Create base exception class and domain exceptions
  - Files:
    - `DwsimWorker/Exceptions/DwsimException.cs` (new)
    - `DwsimWorker/Exceptions/PropertySetException.cs` (new)
    - `DwsimWorker/Exceptions/CompoundNotFoundException.cs` (new)
    - `DwsimWorker/Exceptions/InvalidPropertyValueException.cs` (new)
    - `DwsimWorker/Exceptions/StreamNotFoundException.cs` (new)
    - `DwsimWorker/Exceptions/UnitNotFoundException.cs` (new)
  - Purpose: Establish exception hierarchy for all Spec 1.2 error scenarios
  - _Leverage: `DwsimWorker/Engine/DwsimLoadException.cs` (existing pattern from Spec 1.1)_
  - _Requirements: NFR Security (Error Handling), Error Handling section of design.md_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: C# Developer specializing in exception handling and error design

      Task: Create a base DwsimException class and 5 domain-specific exception classes following the existing DwsimLoadException pattern. Each exception should have:
      - Meaningful properties capturing error context (PropertyName, ProvidedValue, ValidRange, etc.)
      - Clear, actionable error messages following the pattern "What failed: context. Valid range: range"
      - Proper exception chaining with InnerException support

      Restrictions:
      - Do NOT modify existing DwsimLoadException.cs
      - Follow one-file-per-class convention (each exception in its own .cs file)
      - All exceptions must inherit from DwsimException base class
      - Must be in DwsimWorker.Exceptions namespace

      _Leverage:
      - Review `DwsimWorker/Engine/DwsimLoadException.cs` for exception pattern
      - Follow structure.md C# naming conventions

      _Requirements: Requirements.md NFR Security (Error Handling), design.md Exception Hierarchy section

      Success:
      - All 6 exception files created with correct inheritance
      - Each exception has context-capturing properties
      - Error messages are clear and actionable
      - Project compiles without errors

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

- [x] 2. Create data models for stream properties and composition
  - Files:
    - `DwsimWorker/Models/StreamProperties.cs` (new)
    - `DwsimWorker/Models/Composition.cs` (new)
    - `DwsimWorker/Models/UnitOpConfig.cs` (new)
    - `DwsimWorker/Models/ConnectionInfo.cs` (new)
  - Purpose: Define immutable data models for property passing and validation
  - _Leverage: Immutable pattern from `DwsimWorker/Engine/AssemblyLoaderConfig.cs`_
  - _Requirements: Req 3 (Material Stream Properties), Req 6 (Flowsheet Consistency)_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: C# Developer specializing in data modeling and validation

      Task: Create 4 data model classes following the immutable config pattern from AssemblyLoaderConfig. Each model should:
      - StreamProperties: TemperatureK, PressurePa, MolarFlowMolPerSec, Composition; with IsValid(out errorMessage) method
      - Composition: IReadOnlyList<double> MoleFractions with validation (sum to 1.0 ± 1e-6)
      - UnitOpConfig: IReadOnlyDictionary<string, object> Parameters with TryGetParameter<T> helper
      - ConnectionInfo: StreamId, UnitId, PortName, ConnectedAt (immutable record-like class)

      Restrictions:
      - Follow one-file-per-class convention
      - Models must be immutable (readonly properties, validation in constructor)
      - Use DwsimWorker.Models namespace
      - Validation must check physical bounds (temp > 0K, pressure > 0, flow >= 0)

      _Leverage:
      - `DwsimWorker/Engine/AssemblyLoaderConfig.cs` for immutable pattern
      - `DwsimWorker/Engine/AssemblyInfo.cs` for record-like pattern

      _Requirements: Requirements.md Req 3 (AC 2-5), design.md Data Models section

      Success:
      - All 4 model files created
      - StreamProperties.IsValid() validates all physical constraints
      - Composition validates sum to 1.0
      - All models are immutable
      - Project compiles without errors

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

- [x] 3. Create FlowsheetContextConfig and builder
  - Files:
    - `DwsimWorker/Engine/FlowsheetContextConfig.cs` (new)
    - `DwsimWorker/Engine/FlowsheetContextConfigBuilder.cs` (new)
  - Purpose: Provide immutable configuration for FlowsheetContext initialization
  - _Leverage: `DwsimWorker/Engine/AssemblyLoaderConfig.cs`, `DwsimWorker/Engine/AssemblyLoaderConfigBuilder.cs`_
  - _Requirements: NFR Maintainability (Configuration Flexibility)_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: C# Developer specializing in configuration management and builder pattern

      Task: Create FlowsheetContextConfig (immutable config) and FlowsheetContextConfigBuilder (fluent builder) following the exact pattern from AssemblyLoaderConfig/Builder. Config should include:
      - AssemblyLoaderConfig AssemblyConfig (for DWSIM assembly loading)
      - bool ValidateAfterInit (default: true)
      - string FlowsheetName (default: "Flowsheet1")
      - static CreateDefault() factory method

      Restrictions:
      - Follow exact same pattern as AssemblyLoaderConfig/Builder
      - Config must be immutable with private constructor
      - Builder must support fluent API (.WithFlowsheetName().WithValidation().Build())
      - Place in DwsimWorker.Engine namespace

      _Leverage:
      - `DwsimWorker/Engine/AssemblyLoaderConfig.cs` - exact pattern to follow
      - `DwsimWorker/Engine/AssemblyLoaderConfigBuilder.cs` - builder pattern

      _Requirements: design.md FlowsheetContextConfig section

      Success:
      - Both files created following existing pattern exactly
      - CreateDefault() returns sensible defaults
      - Builder supports fluent configuration
      - Project compiles without errors

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

- [x] 4. Create result types for adapter operations
  - Files:
    - `DwsimWorker/Engine/PropertySetResult.cs` (new)
    - `DwsimWorker/Engine/ConnectionResult.cs` (new)
  - Purpose: Provide result-based error handling for adapter operations (avoiding exceptions for expected failures)
  - _Leverage: `DwsimWorker/Engine/LoadResult.cs`, `DwsimWorker/Engine/ValidationResult.cs`_
  - _Requirements: design.md Result Types section_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: C# Developer specializing in functional programming patterns and result types

      Task: Create 2 result classes following the exact pattern from LoadResult and ValidationResult:
      - PropertySetResult: Success, Message, StreamId (for create) or PropertyName (for set), Error; with static factory methods SuccessResult(), FailureResult()
      - ConnectionResult: Success, Message, ConnectionInfo (on success), Error; with static factory methods

      Restrictions:
      - Follow exact pattern from LoadResult.cs
      - Use static factory methods (no public constructor)
      - Must be in DwsimWorker.Engine namespace
      - Success property must be boolean

      _Leverage:
      - `DwsimWorker/Engine/LoadResult.cs` - exact pattern to follow
      - `DwsimWorker/Engine/ValidationResult.cs` - additional pattern reference

      _Requirements: design.md Model 6: Result Types section

      Success:
      - Both result files created with factory methods
      - Pattern matches existing LoadResult exactly
      - Project compiles without errors

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

---

## Phase 2: Core Engine

- [x] 5. Create FlowsheetContext class
  - Files:
    - `DwsimWorker/Engine/FlowsheetContext.cs` (new)
  - Purpose: Encapsulate DWSIM flowsheet and all associated state (compounds, streams, units, connections)
  - _Leverage: `DwsimWorker/Engine/AssemblyLoader.cs` for initialization pattern, `DwsimWorker/Engine/DwsimValidator.cs` for validation_
  - _Requirements: Req 6 (Flowsheet Consistency), design.md Component 1: FlowsheetContext_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: C# Developer specializing in DWSIM API integration and resource management

      Task: Create FlowsheetContext class that:
      1. Accepts ILogger and FlowsheetContextConfig via constructor
      2. Initialize() method: calls AssemblyLoader.LoadDwsimAssemblies(), creates DWSIM.SharedClasses.Flowsheet instance
      3. Maintains internal state:
         - List<string> _compounds
         - Dictionary<string, MaterialStream> _streams
         - Dictionary<string, UnitOperation> _units
         - List<ConnectionInfo> _connections
      4. Implements IDisposable for proper cleanup
      5. Provides accessor methods: GetFlowsheet(), AddCompound(), GetCompounds(), AddStream(), GetStream(), AddUnit(), GetUnit()

      Restrictions:
      - Must use AssemblyLoader from Spec 1.1 (do not bypass)
      - Flowsheet instance must only be accessed after Initialize() called
      - Must implement IDisposable properly (dispose DWSIM objects)
      - NOT thread-safe (document this)
      - Place in DwsimWorker.Engine namespace

      _Leverage:
      - `DwsimWorker/Engine/AssemblyLoader.cs` - for assembly loading
      - `DwsimWorker/Engine/DwsimValidator.cs` - for type finding pattern (FindType method)
      - DWSIM.SharedClasses.Flowsheet - target type to instantiate

      _Requirements: Requirements.md Req 6 (all AC), design.md Component 1

      Success:
      - FlowsheetContext created with all methods
      - Initialize() loads assemblies and creates Flowsheet
      - Dispose() cleans up resources
      - GetFlowsheet() returns valid DWSIM Flowsheet instance
      - All state management methods work correctly
      - Project compiles without errors

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

- [x] 6. Create unit tests for FlowsheetContext
  - Files:
    - `DwsimWorker.Tests/Engine/FlowsheetContextTests.cs` (new)
  - Purpose: Validate FlowsheetContext initialization and state management
  - _Leverage: `DwsimWorker.Tests/Engine/AssemblyLoaderTests.cs`, `DwsimWorker.Tests/TestConfiguration.cs`_
  - _Requirements: Req 6 (Flowsheet Consistency)_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: QA Engineer specializing in xUnit testing and .NET Framework

      Task: Create comprehensive unit tests for FlowsheetContext:
      1. Initialize_WithValidConfig_LoadsAssemblies
      2. Initialize_WithValidConfig_CreatesFlowsheet
      3. GetFlowsheet_BeforeInitialize_ThrowsInvalidOperationException
      4. GetFlowsheet_AfterInitialize_ReturnsFlowsheetInstance
      5. AddCompound_ValidName_AddsToList
      6. GetCompounds_AfterAdding_ReturnsAllCompounds
      7. AddStream_ValidStream_AddsToRegistry
      8. GetStream_ValidId_ReturnsStream
      9. GetStream_InvalidId_ReturnsNull
      10. Dispose_CleansUpResources

      Restrictions:
      - Follow existing test patterns from AssemblyLoaderTests.cs
      - Use TestConfiguration.CreateLogger() for logging
      - Each test must be independent (use IDisposable for cleanup)
      - Tests require DWSIM to be installed (integration test flavor)

      _Leverage:
      - `DwsimWorker.Tests/Engine/AssemblyLoaderTests.cs` - test pattern
      - `DwsimWorker.Tests/TestConfiguration.cs` - test setup

      _Requirements: Requirements.md Req 6 (all AC)

      Success:
      - Test file created with 10+ test methods
      - All tests follow xUnit conventions
      - Tests properly dispose FlowsheetContext
      - Tests pass when DWSIM is installed

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

---

## Phase 3: Adapters

- [ ] 7. Create CapeOpenPropertyConverter utility
  - Files:
    - `DwsimWorker/Converters/CapeOpenPropertyConverter.cs` (new)
  - Purpose: Map between user-friendly property names and CAPE-OPEN standard names
  - _Leverage: CAPE-OPEN 1.0/1.1 specification, design.md CAPE-OPEN Property Mapping table_
  - _Requirements: NFR Compatibility (CAPE-OPEN Compliance)_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: C# Developer with CAPE-OPEN thermodynamics knowledge

      Task: Create static CapeOpenPropertyConverter class with methods:
      - string ToCapeOpenName(string userFriendlyName) - converts "temperature" -> CAPE-OPEN property name
      - string ToUserFriendlyName(string capeOpenName) - reverse mapping
      - bool IsValidPropertyName(string name) - validates property name exists
      - IReadOnlyList<string> GetSupportedProperties() - lists all supported properties

      Property mapping from design.md:
      - temperature -> "temperature" (unit: K)
      - pressure -> "pressure" (unit: Pa)
      - molarFlow -> "totalFlow" (unit: mol/s)
      - composition -> "fraction" (unit: mole fractions)

      Restrictions:
      - Static utility class (no instance state)
      - Place in DwsimWorker.Converters namespace
      - Case-insensitive property name matching

      _Leverage:
      - design.md CAPE-OPEN Property Mapping table
      - DWSIM ICapeThermoMaterialObject interface documentation

      _Requirements: NFR Compatibility (CAPE-OPEN Compliance), design.md Implementation Notes

      Success:
      - Converter class created with all methods
      - Bidirectional mapping works correctly
      - Case-insensitive matching
      - Project compiles without errors

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

- [ ] 8. Create CompoundAdapter
  - Files:
    - `DwsimWorker/Adapters/CompoundAdapter.cs` (new)
  - Purpose: Handle compound database access and compound addition to flowsheet
  - _Leverage: `DwsimWorker/Engine/FlowsheetContext.cs`, DWSIM compound database API_
  - _Requirements: Req 1 (Compound Database Access)_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: C# Developer specializing in DWSIM compound management

      Task: Create CompoundAdapter class with:
      1. Constructor: CompoundAdapter(ILogger logger, FlowsheetContext context)
      2. Result<bool> AddCompound(string compoundName) - adds compound from DWSIM database to flowsheet
      3. Result<IReadOnlyList<string>> GetCompounds() - returns list of compounds in flowsheet
      4. bool ValidateCompoundName(string compoundName) - checks if compound exists in DWSIM database

      Implementation notes:
      - Use DWSIM's compound database (ChemSepDatabase or similar)
      - Log compound additions: logger.Information("compound_added", compoundName=...)
      - Return Result with success=false for invalid compound names (don't throw)

      Restrictions:
      - Place in DwsimWorker.Adapters namespace
      - Use structured logging pattern from AssemblyLoader
      - Do NOT throw exceptions for expected failures (use Result pattern)

      _Leverage:
      - `DwsimWorker/Engine/FlowsheetContext.cs` - for flowsheet access
      - `DwsimWorker/Engine/AssemblyLoader.cs` - for logging pattern
      - DWSIM.Thermodynamics.Databases namespace for compound lookup

      _Requirements: Requirements.md Req 1 (all AC)

      Success:
      - CompoundAdapter created with all methods
      - Can add Methane, Ethane, Propane, Water to flowsheet
      - Invalid compound names return Result with success=false
      - Structured logging implemented
      - Project compiles without errors

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

- [ ] 9. Create PropertyPackageAdapter
  - Files:
    - `DwsimWorker/Adapters/PropertyPackageAdapter.cs` (new)
  - Purpose: Configure thermodynamic property package for flowsheet
  - _Leverage: `DwsimWorker/Engine/FlowsheetContext.cs`, DWSIM property package API_
  - _Requirements: Req 2 (Property Package Configuration)_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: C# Developer specializing in DWSIM thermodynamics configuration

      Task: Create PropertyPackageAdapter class with:
      1. Constructor: PropertyPackageAdapter(ILogger logger, FlowsheetContext context)
      2. Result<bool> SetPropertyPackage(string packageName) - configures property package (e.g., "Peng-Robinson", "SRK")
      3. Result<string> GetPropertyPackageName() - returns currently configured package name
      4. Result<IReadOnlyList<string>> GetAvailablePackages() - lists available property packages

      Implementation notes:
      - Supported packages: "Peng-Robinson", "SRK", "NRTL", "UNIFAC"
      - Property package must be associated with flowsheet
      - Log package configuration: logger.Information("property_package_set", packageName=...)

      Restrictions:
      - Place in DwsimWorker.Adapters namespace
      - Validate package name against known list
      - Return Result with success=false for unsupported packages

      _Leverage:
      - `DwsimWorker/Engine/FlowsheetContext.cs` - for flowsheet access
      - DWSIM.Thermodynamics.PropertyPackages namespace

      _Requirements: Requirements.md Req 2 (all AC)

      Success:
      - PropertyPackageAdapter created with all methods
      - Can set Peng-Robinson property package
      - GetPropertyPackageName returns configured package
      - Invalid package names return Result with success=false
      - Project compiles without errors

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

- [ ] 10. Create StreamAdapter
  - Files:
    - `DwsimWorker/Adapters/StreamAdapter.cs` (new)
  - Purpose: Create material streams and set/get thermodynamic properties using CAPE-OPEN interfaces
  - _Leverage: `DwsimWorker/Engine/FlowsheetContext.cs`, `DwsimWorker/Converters/CapeOpenPropertyConverter.cs`, DWSIM MaterialStream API_
  - _Requirements: Req 3 (Material Stream Creation and Property Setting)_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: C# Developer specializing in DWSIM streams and CAPE-OPEN interfaces

      Task: Create StreamAdapter class with:
      1. Constructor: StreamAdapter(ILogger logger, FlowsheetContext context)
      2. Result<string> CreateStream(string name, StreamProperties properties) - creates stream with properties, returns streamId
      3. Result<bool> SetProperty(string streamId, string propertyName, object value) - sets individual property
      4. Result<object> GetProperty(string streamId, string propertyName) - gets individual property
      5. Result<bool> SetProperties(string streamId, StreamProperties properties) - sets all properties at once
      6. Result<StreamProperties> GetProperties(string streamId) - gets all properties
      7. private bool ValidatePropertyValue(string propertyName, object value) - validates value against physical constraints

      Implementation notes:
      - Use ICapeThermoMaterialObject.SetProp() and GetProp() via CAPE-OPEN interfaces
      - Use CapeOpenPropertyConverter for property name mapping
      - Generate unique streamId (GUID or sequential)
      - Validate: temp > 0K, pressure > 0Pa, flow >= 0, composition sums to 1.0
      - Log property sets: logger.Information("property_set", streamId=..., propertyName=..., value=...)

      Restrictions:
      - Place in DwsimWorker.Adapters namespace
      - Use CAPE-OPEN interfaces where possible (future compatibility)
      - Validate all inputs before calling DWSIM API
      - Return Result with success=false for invalid values (don't throw for expected failures)

      _Leverage:
      - `DwsimWorker/Engine/FlowsheetContext.cs` - for flowsheet access
      - `DwsimWorker/Converters/CapeOpenPropertyConverter.cs` - for property name mapping
      - `DwsimWorker/Models/StreamProperties.cs` - for property model
      - DWSIM.Thermodynamics.Streams.MaterialStream implementing ICapeThermoMaterialObject

      _Requirements: Requirements.md Req 3 (all AC)

      Success:
      - StreamAdapter created with all methods
      - Can create stream with temperature, pressure, flow, composition
      - Property round-trip works (set then get returns same value within tolerance)
      - Invalid values return Result with success=false and clear error message
      - Project compiles without errors

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

- [ ] 11. Create UnitOpAdapter
  - Files:
    - `DwsimWorker/Adapters/UnitOpAdapter.cs` (new)
  - Purpose: Add unit operations to flowsheet and configure their parameters
  - _Leverage: `DwsimWorker/Engine/FlowsheetContext.cs`, DWSIM unit operation API_
  - _Requirements: Req 4 (Three-Phase Separator Unit Operation Addition)_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: C# Developer specializing in DWSIM unit operations

      Task: Create UnitOpAdapter class with:
      1. Constructor: UnitOpAdapter(ILogger logger, FlowsheetContext context)
      2. Result<string> AddThreePhaseSeparator(string name, UnitOpConfig config) - adds separator to flowsheet, returns unitId
      3. Result<bool> SetParameter(string unitId, string parameterName, object value) - sets unit parameter
      4. Result<object> GetParameter(string unitId, string parameterName) - gets unit parameter
      5. Result<IReadOnlyDictionary<string, string>> GetPorts(string unitId) - returns port names and types (inlet, vapor outlet, liquid outlet, water outlet)

      Implementation notes:
      - Use DWSIM.UnitOperations.Separators.ThreePhaseSeparator (or Separator3Phase)
      - Three-phase separator has: 1 inlet, 3 outlets (vapor, light liquid, heavy liquid)
      - Support parameter: "PressureDrop" (Pa)
      - Generate unique unitId (GUID or sequential)
      - Log unit additions: logger.Information("unit_added", unitId=..., unitType=..., name=...)

      Restrictions:
      - Place in DwsimWorker.Adapters namespace
      - This task only implements three-phase separator (other units in future specs)
      - Return Result with success=false for invalid parameters

      _Leverage:
      - `DwsimWorker/Engine/FlowsheetContext.cs` - for flowsheet access
      - `DwsimWorker/Models/UnitOpConfig.cs` - for configuration
      - DWSIM.UnitOperations namespace

      _Requirements: Requirements.md Req 4 (all AC)

      Success:
      - UnitOpAdapter created with all methods
      - Can add three-phase separator to flowsheet
      - GetPorts returns inlet and 3 outlet port names
      - SetParameter/GetParameter work for pressure drop
      - Project compiles without errors

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

- [ ] 12. Create ConnectionAdapter
  - Files:
    - `DwsimWorker/Adapters/ConnectionAdapter.cs` (new)
  - Purpose: Connect material streams to unit operation ports
  - _Leverage: `DwsimWorker/Engine/FlowsheetContext.cs`, DWSIM connection API_
  - _Requirements: Req 5 (Stream Connection to Unit Operations)_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: C# Developer specializing in DWSIM flowsheet topology

      Task: Create ConnectionAdapter class with:
      1. Constructor: ConnectionAdapter(ILogger logger, FlowsheetContext context)
      2. Result<bool> ConnectStream(string streamId, string unitId, string portName) - connects stream to unit port
      3. Result<bool> DisconnectStream(string streamId) - disconnects stream from its port
      4. Result<ConnectionInfo> GetConnection(string streamId) - gets connection info for stream
      5. Result<IReadOnlyList<ConnectionInfo>> GetAllConnections() - lists all connections

      Implementation notes:
      - DWSIM streams connect to unit operations via ports
      - Port names from UnitOpAdapter.GetPorts() (e.g., "Inlet1", "VaporOut", "LiquidOut", "WaterOut")
      - Store connection info in FlowsheetContext
      - Log connections: logger.Information("stream_connected", streamId=..., unitId=..., portName=...)
      - Handle case where port is already connected (return error or replace)

      Restrictions:
      - Place in DwsimWorker.Adapters namespace
      - Validate stream and unit exist before connecting
      - Validate port name is valid for the unit
      - Return Result with success=false for invalid connections

      _Leverage:
      - `DwsimWorker/Engine/FlowsheetContext.cs` - for state management
      - `DwsimWorker/Models/ConnectionInfo.cs` - for connection data
      - `DwsimWorker/Adapters/UnitOpAdapter.cs` - for port validation

      _Requirements: Requirements.md Req 5 (all AC)

      Success:
      - ConnectionAdapter created with all methods
      - Can connect inlet stream to separator inlet
      - Can connect outlet streams to separator outlets
      - GetConnection returns correct ConnectionInfo
      - GetAllConnections lists all connections
      - Project compiles without errors

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

---

## Phase 4: Adapter Unit Tests

- [ ] 13. Create CompoundAdapter unit tests
  - Files:
    - `DwsimWorker.Tests/Adapters/CompoundAdapterTests.cs` (new)
  - Purpose: Validate compound addition and retrieval functionality
  - _Leverage: `DwsimWorker.Tests/Engine/FlowsheetContextTests.cs`, `DwsimWorker.Tests/TestConfiguration.cs`_
  - _Requirements: Req 1 (Compound Database Access)_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: QA Engineer specializing in xUnit testing

      Task: Create unit tests for CompoundAdapter:
      1. AddCompound_ValidName_ReturnsSuccess (test with "Methane")
      2. AddCompound_InvalidName_ReturnsFailure (test with "InvalidCompound123")
      3. AddCompound_MultipleCompounds_AllAdded (test Methane, Ethane, Propane, Water)
      4. GetCompounds_AfterAdding_ReturnsAllCompounds
      5. GetCompounds_EmptyFlowsheet_ReturnsEmptyList
      6. ValidateCompoundName_ValidName_ReturnsTrue
      7. ValidateCompoundName_InvalidName_ReturnsFalse

      Restrictions:
      - Follow test patterns from existing test files
      - Use FlowsheetContext in test setup
      - Clean up resources in Dispose()
      - Tests require DWSIM installation

      _Leverage:
      - `DwsimWorker.Tests/Engine/AssemblyLoaderTests.cs` - test pattern
      - `DwsimWorker.Tests/TestConfiguration.cs` - test setup

      _Requirements: Requirements.md Req 1 (all AC)

      Success:
      - Test file created with 7+ test methods
      - Tests cover success and failure scenarios
      - All tests pass with DWSIM installed

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

- [ ] 14. Create StreamAdapter unit tests
  - Files:
    - `DwsimWorker.Tests/Adapters/StreamAdapterTests.cs` (new)
  - Purpose: Validate stream creation and property round-trip
  - _Leverage: `DwsimWorker.Tests/TestConfiguration.cs`_
  - _Requirements: Req 3 (Material Stream Properties), NFR Reliability (Round-trip validation)_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: QA Engineer specializing in property validation testing

      Task: Create unit tests for StreamAdapter:
      1. CreateStream_ValidProperties_ReturnsSuccess
      2. CreateStream_InvalidTemperature_ReturnsFailure (negative temp)
      3. CreateStream_InvalidPressure_ReturnsFailure (negative pressure)
      4. CreateStream_InvalidComposition_ReturnsFailure (sum != 1.0)
      5. SetProperty_Temperature_RoundTripValidation
      6. SetProperty_Pressure_RoundTripValidation
      7. SetProperty_MolarFlow_RoundTripValidation
      8. SetProperty_Composition_RoundTripValidation
      9. GetProperty_InvalidStreamId_ReturnsFailure
      10. GetProperties_ValidStream_ReturnsAllProperties

      Round-trip validation pattern:
      - Set property to value
      - Get property back
      - Assert within tolerance (1e-6 for dimensionless, 1e-3 for temp/pressure)

      Test data:
      - Temperature: 298.15 K
      - Pressure: 500000 Pa
      - MolarFlow: 100 mol/s
      - Composition: [0.4, 0.3, 0.2, 0.1] (for 4 compounds)

      Restrictions:
      - Must test round-trip validation with tolerances
      - Setup must add compounds and property package before creating streams

      _Leverage:
      - `DwsimWorker.Tests/TestConfiguration.cs` - test setup
      - design.md floating-point tolerance values

      _Requirements: Requirements.md Req 3 (all AC), NFR Reliability

      Success:
      - Test file created with 10+ test methods
      - Round-trip tests pass within tolerance
      - Invalid input tests return failure (not throw)
      - All tests pass with DWSIM installed

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

- [ ] 15. Create UnitOpAdapter and ConnectionAdapter unit tests
  - Files:
    - `DwsimWorker.Tests/Adapters/UnitOpAdapterTests.cs` (new)
    - `DwsimWorker.Tests/Adapters/ConnectionAdapterTests.cs` (new)
  - Purpose: Validate unit operation addition and stream connections
  - _Leverage: `DwsimWorker.Tests/TestConfiguration.cs`_
  - _Requirements: Req 4 (Three-Phase Separator), Req 5 (Stream Connections)_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: QA Engineer specializing in unit operation testing

      Task: Create unit tests for UnitOpAdapter and ConnectionAdapter:

      UnitOpAdapterTests:
      1. AddThreePhaseSeparator_ValidConfig_ReturnsSuccess
      2. AddThreePhaseSeparator_ReturnsUnitId
      3. GetPorts_ThreePhaseSeparator_Returns4Ports (1 inlet, 3 outlets)
      4. SetParameter_PressureDrop_RoundTripValidation
      5. GetParameter_InvalidUnitId_ReturnsFailure

      ConnectionAdapterTests:
      1. ConnectStream_ValidConnection_ReturnsSuccess
      2. ConnectStream_InvalidStreamId_ReturnsFailure
      3. ConnectStream_InvalidUnitId_ReturnsFailure
      4. ConnectStream_InvalidPortName_ReturnsFailure
      5. GetConnection_ConnectedStream_ReturnsConnectionInfo
      6. GetConnection_UnconnectedStream_ReturnsFailure
      7. GetAllConnections_AfterConnecting_ReturnsAllConnections
      8. DisconnectStream_ConnectedStream_ReturnsSuccess

      Restrictions:
      - Setup must include compounds, property package, streams before testing connections
      - Follow existing test patterns

      _Leverage:
      - `DwsimWorker.Tests/TestConfiguration.cs` - test setup

      _Requirements: Requirements.md Req 4 (all AC), Req 5 (all AC)

      Success:
      - Both test files created
      - UnitOpAdapterTests has 5+ methods
      - ConnectionAdapterTests has 8+ methods
      - All tests pass with DWSIM installed

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

---

## Phase 5: Integration and Performance

- [ ] 16. Create golden integration test for three-phase separator workflow
  - Files:
    - `DwsimWorker.Tests/Integration/ThreePhaseSeparatorWorkflowTests.cs` (new)
  - Purpose: Validate complete end-to-end workflow from requirements validation test plan
  - _Leverage: All adapters, `DwsimWorker.Tests/TestConfiguration.cs`_
  - _Requirements: All requirements (Req 1-6), Validation Test Plan from requirements.md_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: Integration Test Engineer

      Task: Create golden integration test that executes the complete workflow from requirements.md Validation Test Plan:

      Test: ConfigureThreePhaseSeparator_CompleteWorkflow_Succeeds
      Steps:
      1. Initialize FlowsheetContext
      2. Add compounds: Methane, Ethane, Propane, Water
      3. Set Peng-Robinson property package
      4. Create inlet stream:
         - Name: "Inlet"
         - Temperature: 298.15 K
         - Pressure: 500000 Pa (5 bar)
         - MolarFlow: 100 mol/s
         - Composition: [0.4, 0.3, 0.2, 0.1] (40% Methane, 30% Ethane, 20% Propane, 10% Water)
      5. Create 3 outlet streams: "VaporOut", "LiquidOut", "WaterOut"
      6. Add three-phase separator: "Separator1"
      7. Connect inlet to separator inlet
      8. Connect outlets to separator outlets
      9. Set separator pressure drop: 5000 Pa
      10. Verify all round-trip validations pass
      11. Assert no exceptions during entire workflow
      12. Assert flowsheet state is valid

      Additional tests:
      - PropertyRoundTrip_AllProperties_MatchWithinTolerance
      - FlowsheetState_AfterConfiguration_IsConsistent

      Restrictions:
      - This is the PRIMARY validation test - it must pass for spec to be complete
      - Use exact values from requirements.md validation test plan
      - Log each step for debugging

      _Leverage:
      - All adapters (CompoundAdapter, PropertyPackageAdapter, StreamAdapter, UnitOpAdapter, ConnectionAdapter)
      - `DwsimWorker.Tests/TestConfiguration.cs`
      - Requirements.md Validation Test Plan section

      _Requirements: All requirements (1-6), Success Criteria from requirements.md

      Success:
      - Integration test file created
      - Golden workflow test passes completely
      - All round-trip validations pass
      - No exceptions during workflow
      - This test validates Spec 1.2 is complete

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

- [ ] 17. Create performance tests
  - Files:
    - `DwsimWorker.Tests/Performance/PropertySetPerformanceTests.cs` (new)
  - Purpose: Validate NFR performance targets are met
  - _Leverage: `DwsimWorker.Tests/TestConfiguration.cs`, System.Diagnostics.Stopwatch_
  - _Requirements: NFR Performance targets from requirements.md_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: Performance Test Engineer

      Task: Create performance tests validating NFR targets:

      Tests with target latencies:
      1. AddCompound_Latency_Under100ms
      2. SetPropertyPackage_Latency_Under500ms
      3. CreateStream_Latency_Under200ms
      4. SetProperty_SingleProperty_Latency_Under50ms
      5. AddThreePhaseSeparator_Latency_Under300ms
      6. ConnectStream_Latency_Under100ms
      7. FullWorkflow_4Compounds4Streams1Separator_Under2Seconds

      Implementation:
      - Use Stopwatch to measure elapsed time
      - Run each operation multiple times (e.g., 5) and take average
      - Assert elapsed time is under target
      - Log actual times for monitoring

      Memory test:
      - MemoryFootprint_ConfiguredFlowsheet_Under50MB
      - Use GC.GetTotalMemory() before and after

      Restrictions:
      - Performance tests may be marked as [Trait("Category", "Performance")]
      - Allow for CI environment variance (targets are P95, not absolute)

      _Leverage:
      - `DwsimWorker.Tests/TestConfiguration.cs`
      - Requirements.md NFR Performance section

      _Requirements: Requirements.md NFR Performance (all targets)

      Success:
      - Performance test file created
      - All latency tests pass within NFR targets
      - Memory test validates footprint
      - Tests can run in CI

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

---

## Phase 6: Documentation and Cleanup

- [ ] 18. Update DwsimWorker project file and add XML documentation
  - Files:
    - `DwsimWorker/DwsimWorker.csproj` (modify)
    - All new .cs files (add XML documentation if missing)
  - Purpose: Ensure project compiles, references are correct, documentation complete
  - _Leverage: Existing .csproj configuration_
  - _Requirements: NFR Maintainability (Documentation)_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: .NET Build Engineer

      Task: Finalize project configuration and documentation:
      1. Update DwsimWorker.csproj:
         - Add any missing file references (new folders: Adapters, Models, Converters, Exceptions)
         - Ensure all new .cs files are included in compilation
         - Add reference to DWSIM.UnitOperations if needed
      2. Verify all public classes have XML documentation:
         - <summary> for all public classes
         - <param> for all public method parameters
         - <returns> for all public methods
         - <exception> for methods that throw
      3. Create or update README in DwsimWorker folder documenting:
         - Spec 1.2 additions (Adapters layer, models)
         - How to run property tests

      Restrictions:
      - Do not modify existing working code (only add documentation)
      - Follow existing documentation style from Spec 1.1 files

      _Leverage:
      - Existing `DwsimWorker/DwsimWorker.csproj`
      - `DwsimWorker/Engine/AssemblyLoader.cs` for documentation style

      _Requirements: NFR Maintainability (Code Readability, Documentation)

      Success:
      - Project compiles without errors
      - All public members have XML documentation
      - README updated with Spec 1.2 information
      - dotnet build succeeds

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

- [ ] 19. Final validation and test run
  - Files:
    - None (validation task)
  - Purpose: Run all tests, verify all requirements are met, document any issues
  - _Leverage: All test files created in this spec_
  - _Requirements: All requirements, Success Criteria_
  - _Prompt: |
      Implement the task for spec three-phase-separator-properties, first run spec-workflow-guide to get the workflow guide then implement the task:

      Role: QA Lead performing final validation

      Task: Execute final validation checklist:

      1. Build validation:
         - Run: dotnet build DwsimWorker.sln
         - Verify: No errors, no warnings (or document acceptable warnings)

      2. Test execution:
         - Run: dotnet test DwsimWorker.Tests
         - Verify: All tests pass
         - Document: Test count, pass rate

      3. Requirements traceability:
         - Req 1 (Compounds): CompoundAdapter tests pass
         - Req 2 (Property Package): PropertyPackageAdapter tests pass
         - Req 3 (Streams): StreamAdapter tests pass with round-trip validation
         - Req 4 (Separator): UnitOpAdapter tests pass
         - Req 5 (Connections): ConnectionAdapter tests pass
         - Req 6 (Flowsheet Consistency): Integration test passes

      4. Golden test validation:
         - ThreePhaseSeparatorWorkflowTests.ConfigureThreePhaseSeparator_CompleteWorkflow_Succeeds MUST pass
         - This is the primary success criterion for Spec 1.2

      5. Performance validation:
         - All performance tests pass within targets

      6. Documentation check:
         - README updated
         - XML documentation complete

      Output: Final validation report documenting:
      - Build status
      - Test results (total, passed, failed)
      - Requirements coverage matrix
      - Any known issues or limitations
      - Ready for Spec 1.3 confirmation

      Restrictions:
      - Do not modify code in this task (validation only)
      - If tests fail, create follow-up tasks to fix

      _Leverage:
      - All test files
      - dotnet CLI

      _Requirements: All requirements, Success Criteria from requirements.md

      Success:
      - All tests pass
      - Golden integration test passes
      - Performance targets met
      - Documentation complete
      - Spec 1.2 is COMPLETE and ready for Spec 1.3

      After completion: Mark task [ ] as [-] in tasks.md before starting. After implementation, use log-implementation tool to record artifacts, then mark [-] as [x]._

---

## Summary

**Total Tasks**: 19

**Task Dependencies**:
- Tasks 1-4 (Foundation) can be done in parallel, no dependencies
- Task 5 (FlowsheetContext) depends on Tasks 1-4
- Task 6 depends on Task 5
- Task 7 (Converter) can be done after Task 2
- Tasks 8-12 (Adapters) depend on Tasks 5 and 7
- Tasks 13-15 (Adapter Tests) depend on corresponding adapters
- Task 16 (Integration Test) depends on all adapters (8-12)
- Task 17 (Performance) depends on all adapters (8-12)
- Task 18 (Documentation) can be done after all code is complete
- Task 19 (Final Validation) must be last

**Critical Path**: 1 → 5 → 10 → 16 → 19

**Estimated Scope**: ~30 new files, ~3000-4000 lines of code
