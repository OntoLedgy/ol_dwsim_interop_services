# Tasks Document

## Phase 1: Data Models

- [x] 1. Create ConvergenceStatus model and ConvergenceState enum
  - File: `DwsimWorker/Models/ConvergenceStatus.cs`, `DwsimWorker/Models/ConvergenceState.cs`
  - Define ConvergenceState enum: NotStarted, InProgress, Converged, NotConverged, Error
  - Create ConvergenceStatus class with State, Message, Iterations, ResidualError, UnitConvergence
  - Add constructor and factory methods
  - Purpose: Represent solver convergence state
  - _Leverage: DwsimWorker/Models/Composition.cs (immutable model pattern)_
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer specializing in .NET Framework and domain modeling | Task: Create ConvergenceStatus model and ConvergenceState enum following requirement 2.x for solver convergence tracking, using the immutable model pattern from DwsimWorker/Models/Composition.cs | Restrictions: Do not add DWSIM dependencies to model classes, follow one-file-per-class rule, use readonly properties | Success: ConvergenceStatus compiles, enum has all states, properties are immutable, follows existing model patterns. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 2. Create SolverMessage model and SolverMessageLevel enum
  - File: `DwsimWorker/Models/SolverMessage.cs`, `DwsimWorker/Models/SolverMessageLevel.cs`
  - Define SolverMessageLevel enum: Debug, Info, Warning, Error
  - Create SolverMessage class with Level, Message, Source, Timestamp
  - Purpose: Capture solver diagnostic messages
  - _Leverage: DwsimWorker/Models/ConnectionInfo.cs (immutable record pattern)_
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create SolverMessage model and SolverMessageLevel enum following requirements 6.x for capturing solver diagnostics | Restrictions: Keep models simple and immutable, no external dependencies | Success: SolverMessage compiles with all properties, timestamp defaults to UtcNow if not provided. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 3. Create CalculationTiming model
  - File: `DwsimWorker/Models/CalculationTiming.cs`
  - Properties: TotalTime (TimeSpan), StartedAt, CompletedAt (DateTime)
  - Optional breakdown: InitializationTime, SolverTime, ResultExtractionTime
  - Add TotalMilliseconds convenience property
  - Purpose: Track calculation performance metrics
  - _Leverage: DwsimWorker/Models/ConnectionInfo.cs_
  - _Requirements: 7.1, 7.2, 7.3_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create CalculationTiming model following requirements 7.x for performance tracking | Restrictions: Use TimeSpan for durations, DateTime for timestamps, all timing values non-negative | Success: CalculationTiming compiles, TotalMilliseconds computed correctly, follows immutable pattern. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 4. Create PhaseProperties model
  - File: `DwsimWorker/Models/PhaseProperties.cs`
  - Properties: PhaseName, MolarFlowMolPerSec, MassFlowKgPerSec, PhaseFraction
  - Include Composition property for phase composition
  - Optional physical properties: DensityKgPerM3, ViscosityPaS, MolecularWeightKgPerKmol
  - Purpose: Store phase-specific calculated properties
  - _Leverage: DwsimWorker/Models/Composition.cs, DwsimWorker/Models/StreamProperties.cs_
  - _Requirements: 3.2, 3.4_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create PhaseProperties model following requirements 3.2 and 3.4 for phase-specific property storage | Restrictions: Reuse existing Composition class, nullable for optional properties | Success: PhaseProperties compiles with all properties, integrates with Composition class. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 5. Create StreamResult model
  - File: `DwsimWorker/Models/StreamResult.cs`
  - Properties: StreamId, StreamName, TemperatureK, PressurePa, MolarFlowMolPerSec, MassFlowKgPerSec
  - Include OverallComposition, VaporFraction, LiquidFraction
  - Dictionary of Phases (keyed by phase name)
  - Purpose: Complete calculated properties for a single stream
  - _Leverage: DwsimWorker/Models/PhaseProperties.cs, DwsimWorker/Models/Composition.cs_
  - _Requirements: 3.1, 3.2, 3.3, 3.5_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create StreamResult model following requirements 3.x for complete stream result storage | Restrictions: Use IReadOnlyDictionary for Phases, immutable model | Success: StreamResult compiles with all properties, phases dictionary properly typed. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 6. Create MassBalanceResult and ComponentMassBalance models
  - File: `DwsimWorker/Models/MassBalanceResult.cs`, `DwsimWorker/Models/ComponentMassBalance.cs`
  - MassBalanceResult: IsValid, InletMolarFlow, OutletMolarFlow, AbsoluteError, RelativeErrorPercent, TolerancePercent
  - ComponentMassBalance: CompoundName, InletMoles, OutletMoles, RelativeErrorPercent, IsValid
  - Add factory methods: Valid(), Invalid()
  - Purpose: Mass balance validation results
  - _Leverage: DwsimWorker/Engine/PropertySetResult.cs (factory pattern)_
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create MassBalanceResult and ComponentMassBalance models following requirements 4.x for mass balance validation | Restrictions: Use factory methods for creation, include per-component validation | Success: Both models compile, factory methods work correctly, RelativeErrorPercent calculated properly. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 7. Create CalculationResult model
  - File: `DwsimWorker/Models/CalculationResult.cs`
  - Properties: Success, ConvergenceStatus, Message, Timing, StreamResults, MassBalance, Messages, Error
  - Factory methods: SuccessResult(), FailureResult(), NotConvergedResult()
  - Purpose: Main result object encapsulating all calculation outputs
  - _Leverage: DwsimWorker/Engine/PropertySetResult.cs (factory pattern), all models from tasks 1-6_
  - _Requirements: 1.2, 2.1, 3.1, 4.1, 6.4, 7.3_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create CalculationResult model as the main result container following multiple requirements | Restrictions: Aggregate all sub-models, use factory methods, immutable | Success: CalculationResult compiles, all factory methods work, properly references sub-models. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

## Phase 2: Exceptions

- [x] 8. Create CalculationException class
  - File: `DwsimWorker/Exceptions/CalculationException.cs`
  - Inherit from DwsimException
  - Properties: ConvergenceStatus, Messages (IReadOnlyList<SolverMessage>)
  - Purpose: Exception for calculation failures with diagnostic context
  - _Leverage: DwsimWorker/Exceptions/DwsimException.cs, DwsimWorker/Exceptions/PropertySetException.cs_
  - _Requirements: 6.2_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create CalculationException extending DwsimException following requirement 6.2 | Restrictions: Follow existing exception hierarchy pattern, include diagnostic context | Success: CalculationException compiles, inherits correctly, includes ConvergenceStatus and Messages. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 9. Create CalculationTimeoutException class
  - File: `DwsimWorker/Exceptions/CalculationTimeoutException.cs`
  - Inherit from CalculationException
  - Properties: Timeout, ElapsedTime (TimeSpan)
  - Purpose: Specific exception for timeout scenarios
  - _Leverage: DwsimWorker/Exceptions/CalculationException.cs (task 8)_
  - _Requirements: 7.1 (timeout handling)_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create CalculationTimeoutException for timeout handling | Restrictions: Must inherit from CalculationException, include timeout details | Success: CalculationTimeoutException compiles with Timeout and ElapsedTime properties. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

## Phase 3: Validators

- [x] 10. Create MassBalanceValidator class
  - File: `DwsimWorker/Utilities/MassBalanceValidator.cs`
  - Create Utilities folder if not exists
  - Static class with validation methods
  - Method: Validate(IEnumerable<StreamResult> inlets, IEnumerable<StreamResult> outlets) → MassBalanceResult
  - Method: ValidateComponentBalances(...) → IReadOnlyList<ComponentMassBalance>
  - Configurable Tolerance parameter (default 1%)
  - Purpose: Validate mass conservation in simulation results
  - _Leverage: DwsimWorker/Models/MassBalanceResult.cs, DwsimWorker/Models/StreamResult.cs_
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer specializing in numerical validation | Task: Create MassBalanceValidator following requirements 4.x for mass balance checking | Restrictions: Stateless validator, inject logger, configurable tolerance, handle edge cases (zero flow) | Success: MassBalanceValidator compiles, validates overall and per-component balance, tolerance is configurable. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

## Phase 4: StreamAdapter Extension

- [x] 11. Extend StreamAdapter with GetCalculatedProperties method
  - File: `DwsimWorker/Adapters/StreamAdapter.cs` (modify existing)
  - Add method: GetCalculatedProperties(string streamId) → StreamResult
  - Extract temperature, pressure, flow, composition from DWSIM stream after calculation
  - Handle case where stream not yet calculated
  - Purpose: Retrieve calculated stream properties after solver runs
  - _Leverage: Existing StreamAdapter methods, DwsimWorker/Models/StreamResult.cs_
  - _Requirements: 3.1, 3.2, 3.3_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with DWSIM API knowledge | Task: Extend StreamAdapter with GetCalculatedProperties method following requirements 3.x | Restrictions: Do not break existing methods, return Result type, handle uncalculated streams | Success: GetCalculatedProperties works on calculated streams, returns failure for uncalculated, integrates with StreamResult model. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 12. Add GetPhaseProperties method to StreamAdapter
  - File: `DwsimWorker/Adapters/StreamAdapter.cs` (continue from task 11)
  - Add method: GetPhaseProperties(string streamId, string phaseName) → PhaseProperties
  - Access DWSIM stream Phases collection (indices: 0=Overall, 2=Vapor, 3=Liquid1, 4=Liquid2)
  - Extract phase-specific: molar flow, mass flow, composition, density, viscosity
  - Purpose: Retrieve phase-specific calculated properties
  - _Leverage: DwsimWorker/Models/PhaseProperties.cs, DWSIM MaterialStream.Phases API_
  - _Requirements: 3.2, 3.5_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with DWSIM API expertise | Task: Add GetPhaseProperties method to StreamAdapter following requirements 3.2 and 3.5 | Restrictions: Handle missing phases gracefully, use correct DWSIM phase indices | Success: GetPhaseProperties returns correct phase data, handles non-existent phases with appropriate result. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 13. Add GetAllPhaseProperties method to StreamAdapter
  - File: `DwsimWorker/Adapters/StreamAdapter.cs` (continue from task 12)
  - Add method: GetAllPhaseProperties(string streamId) → IReadOnlyDictionary<string, PhaseProperties>
  - Iterate over all existing phases in stream
  - Return dictionary keyed by phase name ("Vapor", "Liquid1", "Liquid2")
  - Purpose: Retrieve all phase results at once for efficiency
  - _Leverage: GetPhaseProperties method (task 12)_
  - _Requirements: 3.2_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Add GetAllPhaseProperties method to StreamAdapter | Restrictions: Reuse GetPhaseProperties, only include existing phases | Success: GetAllPhaseProperties returns dictionary of all present phases, excludes non-existent phases. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

## Phase 5: CalculationAdapter

- [x] 14. Create CalculationAdapter class with constructor and dependencies
  - File: `DwsimWorker/Adapters/CalculationAdapter.cs`
  - Constructor: ILogger, FlowsheetContext, StreamAdapter, ConnectionAdapter, MassBalanceValidator
  - Store dependencies as private readonly fields
  - Add private fields for tracking calculation state
  - Purpose: Main adapter for calculation orchestration
  - _Leverage: DwsimWorker/Adapters/StreamAdapter.cs (adapter pattern), DwsimWorker/Adapters/ConnectionAdapter.cs_
  - _Requirements: 1.1 (setup for solver invocation)_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Create CalculationAdapter class structure with constructor and dependencies | Restrictions: Follow existing adapter patterns, inject all dependencies, no direct DWSIM references in constructor | Success: CalculationAdapter compiles with all dependencies injected, follows naming conventions. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 15. Implement RunCalculation method (basic solver invocation)
  - File: `DwsimWorker/Adapters/CalculationAdapter.cs` (continue from task 14)
  - Add method: RunCalculation() → CalculationResult
  - Validate topology via ConnectionAdapter before calculation
  - Get flowsheet from FlowsheetContext
  - Invoke DWSIM solver (FlowsheetSolver.SolveFlowsheet or flowsheet.RequestCalculation)
  - Capture timing with Stopwatch
  - Return CalculationResult with basic status
  - Purpose: Core solver invocation logic
  - _Leverage: DwsimWorker/Engine/FlowsheetContext.cs, DWSIM.FlowsheetSolver API_
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with DWSIM solver expertise | Task: Implement RunCalculation method following requirements 1.x for solver invocation | Restrictions: Validate topology first, capture timing, handle exceptions gracefully | Success: RunCalculation invokes DWSIM solver, captures timing, returns appropriate result for success/failure. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 16. Add convergence status handling to CalculationAdapter
  - File: `DwsimWorker/Adapters/CalculationAdapter.cs` (continue from task 15)
  - Add method: GetConvergenceStatus() → ConvergenceStatus
  - Check flowsheet.Solved property
  - Capture flowsheet.ErrorMessage if not converged
  - Check per-unit convergence if available
  - Integrate with RunCalculation to populate ConvergenceStatus in result
  - Purpose: Determine and report solver convergence
  - _Leverage: DwsimWorker/Models/ConvergenceStatus.cs, DWSIM Flowsheet API_
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Add convergence status handling following requirements 2.x | Restrictions: Check both overall and per-unit convergence, capture error messages | Success: GetConvergenceStatus returns correct state, integrated with RunCalculation result. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 17. Add solver message capture to CalculationAdapter
  - File: `DwsimWorker/Adapters/CalculationAdapter.cs` (continue from task 16)
  - Capture DWSIM solver messages during calculation
  - Convert to List<SolverMessage> with appropriate levels
  - Include in CalculationResult
  - Purpose: Capture diagnostic information from solver
  - _Leverage: DwsimWorker/Models/SolverMessage.cs, DWSIM event/message API_
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Add solver message capture following requirements 6.x | Restrictions: Capture all message levels, include source if available | Success: Messages captured during calculation, included in result, empty list (not null) when no messages. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 18. Add result extraction to CalculationAdapter
  - File: `DwsimWorker/Adapters/CalculationAdapter.cs` (continue from task 17)
  - Add method: GetStreamResult(string streamId) → Result<StreamResult>
  - Add method: GetAllStreamResults() → Result<IReadOnlyList<StreamResult>>
  - Use StreamAdapter.GetCalculatedProperties for extraction
  - Integrate with RunCalculation to populate StreamResults in result
  - Purpose: Extract calculated properties from all streams
  - _Leverage: DwsimWorker/Adapters/StreamAdapter.cs (extended methods), DwsimWorker/Models/StreamResult.cs_
  - _Requirements: 3.1, 3.2, 3.3, 3.4_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Add result extraction methods following requirements 3.x | Restrictions: Use StreamAdapter for extraction, handle partial failures | Success: GetStreamResult and GetAllStreamResults work correctly, results included in CalculationResult. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 19. Add mass balance validation to CalculationAdapter
  - File: `DwsimWorker/Adapters/CalculationAdapter.cs` (continue from task 18)
  - Identify inlet and outlet streams from connections
  - Call MassBalanceValidator.ValidateMassBalance
  - Include MassBalanceResult in CalculationResult
  - Log warning if mass balance fails
  - Purpose: Validate physical consistency of results
  - _Leverage: DwsimWorker/Validators/MassBalanceValidator.cs, DwsimWorker/Adapters/ConnectionAdapter.cs_
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Add mass balance validation following requirements 4.x | Restrictions: Identify inlet/outlet from topology, log warnings for failures | Success: Mass balance validated after calculation, result includes MassBalanceResult. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 20. Add timeout support to CalculationAdapter
  - File: `DwsimWorker/Adapters/CalculationAdapter.cs` (continue from task 19)
  - Add overload: RunCalculation(TimeSpan timeout) → CalculationResult
  - Use CancellationTokenSource with timeout
  - Run calculation on Task.Run for cancellation support
  - Return CalculationTimeoutException details on timeout
  - Purpose: Prevent runaway calculations
  - _Leverage: DwsimWorker/Exceptions/CalculationTimeoutException.cs_
  - _Requirements: 7.1 (timeout handling from NFRs)_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with async/Task expertise | Task: Add timeout support to RunCalculation | Restrictions: Use CancellationTokenSource, handle timeout gracefully, include elapsed time in result | Success: RunCalculation with timeout works, returns timeout error when exceeded. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 21. Add GetUnitMetrics method to CalculationAdapter
  - File: `DwsimWorker/Adapters/CalculationAdapter.cs` (continue from task 20)
  - Add method: GetUnitMetrics(string unitId) → Result<IDictionary<string, object>>
  - Extract separator-specific metrics: actual pressure drop, etc.
  - Return null/not-available for unavailable metrics (not exception)
  - Purpose: Retrieve unit operation performance data
  - _Leverage: DwsimWorker/Adapters/UnitOpAdapter.cs, DWSIM Separator API_
  - _Requirements: 5.1, 5.2, 5.3_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer | Task: Add GetUnitMetrics method following requirements 5.x | Restrictions: Return null for unavailable metrics, use Result pattern | Success: GetUnitMetrics returns available metrics, handles unavailable gracefully. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

## Phase 6: Unit Tests

- [x] 22. Create unit tests for data models
  - File: `DwsimWorker.Tests/Models/CalculationModelsTests.cs`
  - Test ConvergenceStatus construction and properties
  - Test SolverMessage creation with default timestamp
  - Test CalculationTiming.TotalMilliseconds calculation
  - Test MassBalanceResult factory methods
  - Test CalculationResult factory methods
  - Purpose: Verify model correctness
  - _Leverage: DwsimWorker.Tests/Models/ existing patterns_
  - _Requirements: All model-related requirements_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer with xUnit expertise | Task: Create comprehensive unit tests for all data models | Restrictions: Test factory methods, immutability, edge cases | Success: All model tests pass, good coverage of factory methods and properties. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 23. Create unit tests for MassBalanceValidator
  - File: `DwsimWorker.Tests/Utilities/MassBalanceValidatorTests.cs`
  - Test ValidateMassBalance with balanced flows (valid)
  - Test ValidateMassBalance with unbalanced flows (invalid)
  - Test ValidateComponentMassBalance
  - Test tolerance configuration
  - Test edge cases: zero flow, single component
  - Purpose: Verify validation logic
  - _Leverage: DwsimWorker.Tests/Adapters/ existing patterns_
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer | Task: Create unit tests for MassBalanceValidator following requirements 4.x | Restrictions: Test both valid and invalid scenarios, test tolerance boundary | Success: All validator tests pass, edge cases covered. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 24. Create unit tests for CalculationAdapter
  - File: `DwsimWorker.Tests/Adapters/CalculationAdapterTests.cs`
  - Test constructor and dependency injection
  - Test RunCalculation returns failure for invalid topology
  - Test GetConvergenceStatus returns correct states
  - Test result extraction methods
  - Use mocked FlowsheetContext where possible
  - Purpose: Verify adapter logic in isolation
  - _Leverage: DwsimWorker.Tests/Adapters/StreamAdapterTests.cs_
  - _Requirements: 1.x, 2.x, 3.x_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer | Task: Create unit tests for CalculationAdapter | Restrictions: Mock dependencies where feasible, test error scenarios | Success: Adapter tests pass, cover main code paths. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

## Phase 7: Integration Tests

- [x] 25. Create golden integration test for three-phase separator calculation
  - File: `DwsimWorker.Tests/Integration/ThreePhaseSeparatorCalculationTests.cs`
  - Setup: Create flowsheet with 4 compounds, Peng-Robinson, inlet stream, 3 outlets, separator (from Spec 1.2)
  - Execute: Run calculation via CalculationAdapter
  - Verify: Convergence, timing < 5s, mass balance < 1%, all streams have results
  - Golden values: Check outlet flows and compositions are reasonable
  - Purpose: End-to-end validation of calculation workflow
  - _Leverage: DwsimWorker.Tests/Integration/ThreePhaseSeparatorWorkflowTests.cs (Spec 1.2 setup)_
  - _Requirements: All functional requirements_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer with integration testing expertise | Task: Create golden integration test following all requirements | Restrictions: Use real DWSIM assemblies, skip if not available, verify physical reasonableness | Success: Integration test passes with DWSIM, validates convergence, timing, mass balance. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

- [x] 26. Create convergence scenario tests
  - File: `DwsimWorker.Tests/Integration/ConvergenceScenarioTests.cs`
  - Test standard case converges
  - Test edge compositions (pure component)
  - Test that non-convergence is handled gracefully (not exception)
  - Purpose: Validate convergence handling across scenarios
  - _Leverage: DwsimWorker.Tests/Integration/ThreePhaseSeparatorCalculationTests.cs_
  - _Requirements: 2.1, 2.2, 2.3_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer | Task: Create convergence scenario tests | Restrictions: Handle DWSIM availability, test multiple scenarios | Success: Convergence tests pass, non-convergence handled gracefully. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

## Phase 8: Performance Tests

- [x] 27. Create calculation performance tests
  - File: `DwsimWorker.Tests/Performance/CalculationPerformanceTests.cs`
  - Measure calculation time for standard three-phase separator
  - Assert: Total time < 5 seconds
  - Measure result extraction time separately
  - Assert: Extraction time < 500 ms
  - Purpose: Validate performance targets from NFRs
  - _Leverage: DwsimWorker.Tests/Performance/PropertySetPerformanceTests.cs_
  - _Requirements: 7.1, 7.4 (performance NFRs)_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Performance Engineer | Task: Create performance tests for calculation | Restrictions: Use Stopwatch for timing, clear pass/fail criteria | Success: Performance tests pass, meet timing targets. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._

## Phase 9: Documentation

- [x] 28. Update project documentation
  - Files: All classes have comprehensive XML documentation
  - Add XML doc comments to all public classes and methods
  - Document DWSIM solver API usage in CalculationAdapter
  - Update DwsimWorker README with calculation workflow
  - Purpose: Ensure code is maintainable and discoverable
  - _Leverage: Existing documentation patterns_
  - _Requirements: Maintainability NFRs_
  - _Prompt: Implement the task for spec three-phase-separator-calculation, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Technical Writer | Task: Add comprehensive documentation | Restrictions: Follow XML doc comment conventions, document DWSIM API usage | Success: All public APIs documented, README updated with calculation workflow. After completion, mark task [-] as in_progress in tasks.md before starting, log implementation with log-implementation tool, then mark [x] as complete._
