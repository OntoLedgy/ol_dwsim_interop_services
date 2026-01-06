# Tasks Document

## Implementation Tasks for DWSIM Assembly Loader

- [x] 1. Create DwsimLoadException custom exception class
  - File: `mcp_service/dwsim_worker/DwsimWorker/Engine/DwsimLoadException.cs`
  - Define custom exception type for assembly loading failures
  - Add ErrorCode enum with values: AssemblyNotFound, AssemblyLoadFailure, DependencyMissing, TypeLoadFailure, ValidationFailure
  - Include properties: AssemblyName, AttemptedPath, Code
  - Purpose: Provide structured error information for assembly loading failures
  - _Leverage: Standard .NET Exception base class_
  - _Requirements: 1.4, 2.4, 3.2_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer specializing in exception handling and error design | Task: Create a custom DwsimLoadException class following requirements 1.4, 2.4, and 3.2 for structured assembly loading error handling. Extend System.Exception with properties for AssemblyName, AttemptedPath, and ErrorCode enum. | Restrictions: Must follow .NET exception best practices, include all inner exception details, do not expose sensitive system information, follow naming conventions from structure.md (PascalCase for classes and properties) | _Leverage: System.Exception base class | _Requirements: 1.4, 2.4, 3.2 | Success: Exception class compiles without errors, includes all required properties and constructors, properly preserves inner exceptions, can be serialized for logging. After completing, use log-implementation tool with detailed artifacts (classes: name, methods, location), then mark task as complete in tasks.md.

- [x] 2. Create AssemblyInfo and LoadResult data models
  - Files:
    - `mcp_service/dwsim_worker/DwsimWorker/Engine/AssemblyInfo.cs`
    - `mcp_service/dwsim_worker/DwsimWorker/Engine/LoadResult.cs`
  - Implement AssemblyInfo immutable data class (Name, Version, Path, LoadedAt)
  - Implement LoadResult immutable class with Success, Message, LoadedAssemblies, Error, ExitCode
  - Add factory methods: LoadResult.SuccessResult() and LoadResult.FailureResult()
  - Purpose: Provide data structures for assembly loading results
  - _Leverage: None (foundational types)_
  - _Requirements: 1.7, 5.4_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with expertise in immutable data structures and domain modeling | Task: Create AssemblyInfo and LoadResult immutable data classes following requirements 1.7 and 5.4. Implement factory methods for success and failure cases. Use readonly properties and constructor-based initialization. | Restrictions: Must be immutable (no setters), use IReadOnlyList for collections, follow structure.md naming conventions (PascalCase), ensure thread-safety | _Leverage: None (foundational types) | _Requirements: 1.7, 5.4 | Success: Both classes are immutable and compile without errors, factory methods create appropriate instances, ExitCode properly maps to success/failure states (0 for success, 1-3 for failures), classes are thread-safe. After completing, use log-implementation tool with detailed artifacts (classes: name, methods, location), then mark task as complete in tasks.md.

- [x] 3. Create ValidationResult data model
  - File: `mcp_service/dwsim_worker/DwsimWorker/Engine/ValidationResult.cs`
  - Implement ValidationResult immutable class (Success, Message, ValidatedTypes, Error)
  - Add factory methods for success and failure cases
  - Purpose: Represent validation operation results
  - _Leverage: None (foundational type)_
  - _Requirements: 2.4_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with expertise in result pattern and domain modeling | Task: Create ValidationResult immutable data class following requirement 2.4 for representing validation operation outcomes. Include Success flag, Message, ValidatedTypes list, and optional Error. | Restrictions: Must be immutable, use `IReadOnlyList<string>` for ValidatedTypes, follow structure.md naming conventions, ensure null-safety for Error property | _Leverage: None (foundational type) | _Requirements: 2.4 | Success: ValidationResult is immutable and compiles correctly, factory methods work properly, can represent both success and failure cases clearly. After completing, use log-implementation tool with detailed artifacts (classes: name, methods, location), then mark task as complete in tasks.md.

- [x] 4. Create PathResolver utility class
  - File: `mcp_service/dwsim_worker/DwsimWorker/Utilities/PathResolver.cs`
  - Implement static methods: ResolveDwsimPath(), GetEnvironmentPath(), GetConfigPath(), GetDefaultInstallPath()
  - Add ValidatePath(string path) and FindAssemblies(string basePath) methods
  - Implement fallback strategy: environment variable → App.config → default install paths
  - Purpose: Resolve DWSIM assembly paths using multiple strategies
  - _Leverage: System.Configuration for App.config reading_
  - _Requirements: 3.3, 3.4_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer specializing in configuration management and file system operations | Task: Create PathResolver utility class following requirements 3.3 and 3.4 to resolve DWSIM assembly paths. Implement multiple resolution strategies with fallback: DWSIM_PATH environment variable, App.config appSetting, default Windows installation paths (C:\\Program Files\\DWSIM). Validate resolved paths contain expected DLL files. | Restrictions: Must be static utility class, validate all paths before returning, handle missing directories gracefully, log resolution attempts using Serilog, follow structure.md conventions | _Leverage: System.Environment for environment variables, System.Configuration.ConfigurationManager for App.config | _Requirements: 3.3, 3.4 | Success: All resolution strategies work correctly, fallback mechanism functions properly, ValidatePath correctly identifies valid DWSIM installations, clear exceptions thrown when no valid path found. After completing, use log-implementation tool with detailed artifacts (classes: name, methods, location), then mark task as complete in tasks.md.

- [x] 5. Create AssemblyLoaderConfig with builder pattern
  - Files:
    - `mcp_service/dwsim_worker/DwsimWorker/Engine/AssemblyLoaderConfig.cs`
    - `mcp_service/dwsim_worker/DwsimWorker/Engine/AssemblyLoaderConfigBuilder.cs`
  - Implement AssemblyLoaderConfig immutable class (AssemblyPath, ValidateAfterLoad, LoadTimeout, RequiredAssemblies)
  - Implement AssemblyLoaderConfigBuilder with fluent API
  - Add Create() factory method and Build() method
  - Purpose: Provide flexible configuration for AssemblyLoader
  - _Leverage: Builder pattern_
  - _Requirements: 3.3_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with expertise in builder pattern and fluent APIs | Task: Create AssemblyLoaderConfig and AssemblyLoaderConfigBuilder classes following requirement 3.3. Config should be immutable with properties: AssemblyPath, ValidateAfterLoad (default true), LoadTimeout (default 30s), RequiredAssemblies list. Builder should provide fluent API: WithAssemblyPath(), WithValidationEnabled(), WithTimeout(), WithRequiredAssemblies(). | Restrictions: Config must be immutable, builder must validate inputs before building, follow fluent API patterns, use sensible defaults, follow structure.md naming conventions | _Leverage: Builder pattern | _Requirements: 3.3 | Success: Config is immutable and properly constructed via builder, fluent API works correctly, defaults are sensible, validation in builder prevents invalid configurations. After completing, use log-implementation tool with detailed artifacts (classes: name, methods, location), then mark task as complete in tasks.md.

- [x] 6. Create DwsimValidator class
  - File: `mcp_service/dwsim_worker/DwsimWorker/Engine/DwsimValidator.cs`
  - Implement constructor accepting ILogger
  - Implement ValidateInstantiation(), ValidateFlowsheetCreation(), ValidateMaterialStreamCreation() methods
  - Instantiate DWSIM.SharedClasses.Flowsheet and DWSIM.Thermodynamics.Streams.MaterialStream
  - Return ValidationResult for each validation method
  - Purpose: Validate loaded DWSIM assemblies by instantiating objects
  - _Leverage: Serilog ILogger_
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer with expertise in reflection and assembly validation | Task: Create DwsimValidator class following requirements 2.1-2.7 to validate loaded DWSIM assemblies by instantiating core objects (Flowsheet, MaterialStream). Use reflection or direct instantiation (Activator.CreateInstance or new). Handle TypeLoadException, MissingMethodException, TargetInvocationException gracefully. Return ValidationResult with success/failure and validated type names. | Restrictions: Must not require GUI context (no STA thread for instantiation per requirement 2.3), handle all exceptions gracefully returning ValidationResult (not throwing), log validation steps with Serilog, verify objects are not null, follow structure.md conventions | _Leverage: Serilog.ILogger from Program.cs | _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7 | Success: Can instantiate Flowsheet and MaterialStream successfully, validation methods return detailed ValidationResult, handles failures gracefully, logs validation attempts and results. After completing, use log-implementation tool with detailed artifacts (classes: name, methods, location), then mark task as complete in tasks.md.

- [x] 7. Create AssemblyLoader main class
  - File: `mcp_service/dwsim_worker/DwsimWorker/Engine/AssemblyLoader.cs`
  - Implement constructor accepting ILogger and AssemblyLoaderConfig
  - Implement LoadDwsimAssemblies() main method returning LoadResult
  - Implement LoadAssembly(string assemblyName, string path) helper method
  - Coordinate with PathResolver to find assemblies, load each required assembly, handle exceptions, call DwsimValidator for validation
  - Purpose: Orchestrate assembly loading and validation
  - _Leverage: PathResolver, DwsimValidator, LoadResult, Serilog ILogger_
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 3.1, 3.2, 3.4, 3.5_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Developer specializing in assembly loading and AppDomain management | Task: Create AssemblyLoader class following requirements 1.1-1.7, 3.1-3.5 as the main orchestrator for loading DWSIM assemblies. Implement LoadDwsimAssemblies() to: 1) Use PathResolver to find DWSIM path, 2) Load each required assembly (DWSIM.Interfaces, DWSIM.Thermodynamics, DWSIM.SharedClasses, CapeOpen) using Assembly.LoadFrom or Assembly.Load, 3) Handle FileNotFoundException, FileLoadException, BadImageFormatException, TypeLoadException, 4) Call DwsimValidator if config.ValidateAfterLoad is true, 5) Construct LoadResult with all loaded assemblies or error. Log each step with Serilog including assembly names, versions, paths. | Restrictions: Must load assemblies in correct dependency order, catch specific exceptions (not general Exception), wrap exceptions in DwsimLoadException with context, log all attempts and failures, respect config.LoadTimeout, follow structure.md naming conventions | _Leverage: PathResolver for path resolution, DwsimValidator for validation, LoadResult for results, Serilog.ILogger for logging, AssemblyLoaderConfig for configuration | _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 3.1, 3.2, 3.4, 3.5 | Success: LoadDwsimAssemblies successfully loads all required assemblies from resolved path, returns LoadResult with detailed information, handles all error scenarios gracefully with clear error messages, validation executes when enabled, logs comprehensively for troubleshooting. After completing, use log-implementation tool with detailed artifacts (classes: name, methods, location, plus any integration patterns), then mark task as complete in tasks.md.

- [x] 8. Update App.config with binding redirects
  - File: `mcp_service/dwsim_worker/DwsimWorker/App.config`
  - Add appSettings section with DwsimPath placeholder (commented out)
  - Add runtime assemblyBinding section with Newtonsoft.Json binding redirect (oldVersion 0.0.0.0-13.0.0.0 → newVersion 13.0.3.0)
  - Purpose: Configure assembly dependency resolution and settings
  - _Leverage: Existing App.config_
  - _Requirements: 3.1, 3.2_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: .NET Configuration Specialist with expertise in App.config and binding redirects | Task: Update App.config following requirements 3.1 and 3.2 to add binding redirects for DWSIM dependencies. Add appSettings section with optional DwsimPath setting (commented out with example). Add runtime/assemblyBinding section with dependentAssembly for Newtonsoft.Json redirecting oldVersion="0.0.0.0-13.0.0.0" to newVersion="13.0.3.0" (version must match DwsimWorker.csproj PackageReference). | Restrictions: Must use correct XML structure for binding redirects, include publicKeyToken for Newtonsoft.Json (30ad4fe6b2a6aeed), ensure binding redirect matches installed version, do not remove existing configuration | _Leverage: Existing App.config structure | _Requirements: 3.1, 3.2 | Success: App.config is valid XML, binding redirect correctly resolves Newtonsoft.Json version conflicts, appSettings section allows path override, configuration loads without errors. After completing, use log-implementation tool with detailed artifacts (filesModified), then mark task as complete in tasks.md.

- [x] 9. Integrate AssemblyLoader into Program.cs
  - File: `mcp_service/dwsim_worker/DwsimWorker/Program.cs`
  - Create AssemblyLoaderConfig with default settings in Main method
  - Instantiate AssemblyLoader with Log.Logger and config
  - Call LoadDwsimAssemblies() and handle LoadResult
  - Map LoadResult.ExitCode to return value from Main
  - Log assembly loading results using Serilog
  - Purpose: Integrate assembly loader into application startup
  - _Leverage: Existing Serilog configuration in Program.cs, AssemblyLoader, LoadResult_
  - _Requirements: 5.4, 5.5_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: C# Application Developer with expertise in console application architecture | Task: Integrate AssemblyLoader into Program.cs Main method following requirements 5.4 and 5.5. Before existing TODO comments, add: 1) Create AssemblyLoaderConfig using default settings or builder, 2) Instantiate AssemblyLoader with Log.Logger and config, 3) Call LoadDwsimAssemblies() and store LoadResult, 4) Log result.Success and result.Message, 5) If result.Success is false, log result.Error details and return result.ExitCode, 6) If success, log loaded assemblies with names and versions, 7) Continue to existing code if successful. Replace existing infinite wait with proper initialization. | Restrictions: Must use existing Serilog Log.Logger (don't create new logger), handle LoadResult properly, return correct exit codes (0 for success, 1-3 for failures per requirement 5.5), maintain existing exception handling structure, don't break existing Serilog configuration | _Leverage: Existing Serilog configuration (Log.Logger), AssemblyLoader class, LoadResult model, AssemblyLoaderConfig | _Requirements: 5.4, 5.5 | Success: Application loads DWSIM assemblies on startup, logs detailed information about loaded assemblies or failures, exits with appropriate code on failure, continues to initialization on success. After completing, use log-implementation tool with detailed artifacts (filesModified, integrations showing Program.cs → AssemblyLoader flow), then mark task as complete in tasks.md.

- [x] 10. Create AssemblyLoader unit tests
  - File: `mcp_service/dwsim_worker/DwsimWorker.Tests/Engine/AssemblyLoaderTests.cs`
  - Test LoadDwsimAssemblies with valid path (returns success LoadResult)
  - Test LoadDwsimAssemblies with invalid path (returns failure LoadResult with AssemblyNotFound)
  - Test LoadDwsimAssemblies with partial load (one assembly missing, returns failure with details)
  - Test LoadAssembly with version mismatch (logs warning, continues if binding redirect exists)
  - Mock ILogger for testing logging calls
  - Purpose: Ensure AssemblyLoader reliability
  - _Leverage: xUnit framework (already in DwsimWorker.Tests.csproj), Moq for mocking ILogger_
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer with expertise in C# unit testing and xUnit framework | Task: Create comprehensive unit tests for AssemblyLoader following requirements 1.1-1.5. Use xUnit for test framework and Moq for mocking ILogger. Create tests: 1) LoadDwsimAssemblies_WithValidPath_ReturnsSuccess, 2) LoadDwsimAssemblies_WithInvalidPath_ReturnsFailureWithAssemblyNotFound, 3) LoadDwsimAssemblies_WithMissingAssembly_ReturnsFailureWithDetails, 4) LoadAssembly_WithVersionMismatch_LogsWarning. For valid path tests, you may need test DWSIM assemblies or mock Assembly loading. | Restrictions: Must use xUnit [Fact] attributes, mock ILogger to verify logging calls, test both success and failure paths, ensure tests are isolated and don't depend on real DWSIM installation, follow naming convention MethodName_Scenario_ExpectedResult, don't test external dependencies directly | _Leverage: xUnit framework from DwsimWorker.Tests.csproj, Moq NuGet package for ILogger mocking | _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5 | Success: All tests pass, both success and failure scenarios covered, logging calls verified with mocks, tests run independently and consistently, edge cases tested. After completing, use log-implementation tool with detailed artifacts (filesCreated, functions tested), then mark task as complete in tasks.md.

- [x] 11. Create PathResolver unit tests
  - File: `mcp_service/dwsim_worker/DwsimWorker.Tests/Utilities/PathResolverTests.cs`
  - Test ResolveDwsimPath with environment variable set (returns env path)
  - Test ResolveDwsimPath with App.config setting (returns config path)
  - Test ResolveDwsimPath with default install location (returns default path)
  - Test ResolveDwsimPath with no valid path (throws DirectoryNotFoundException)
  - Test ValidatePath with valid directory containing DLLs (returns true)
  - Test ValidatePath with invalid directory (returns false)
  - Test FindAssemblies returns expected DLL files
  - Purpose: Ensure PathResolver reliability across different configurations
  - _Leverage: xUnit framework, temporary directories for test paths_
  - _Requirements: 3.3, 3.4, 3.5_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer with expertise in file system testing and configuration testing | Task: Create comprehensive unit tests for PathResolver following requirements 3.3-3.5. Test all resolution strategies: environment variable (DWSIM_PATH), App.config (appSettings["DwsimPath"]), default paths. Use temporary directories and mock assemblies (empty .dll files) for testing. Tests: 1) ResolveDwsimPath_WithEnvironmentVariable_ReturnsEnvPath, 2) ResolveDwsimPath_WithConfigSetting_ReturnsConfigPath, 3) ResolveDwsimPath_WithDefaultInstall_ReturnsDefaultPath, 4) ResolveDwsimPath_WithNoValidPath_ThrowsException, 5) ValidatePath_WithValidPath_ReturnsTrue, 6) ValidatePath_WithInvalidPath_ReturnsFalse, 7) FindAssemblies_ReturnsExpectedDlls. | Restrictions: Must clean up temporary directories after tests, don't depend on actual DWSIM installation, test each resolution strategy independently, use xUnit [Fact] attributes, follow naming conventions | _Leverage: xUnit framework, System.IO for temporary directories, create mock .dll files for validation tests | _Requirements: 3.3, 3.4, 3.5 | Success: All resolution strategies tested and working correctly, fallback mechanism validated, ValidatePath correctly identifies valid/invalid paths, tests are isolated and reliable. After completing, use log-implementation tool with detailed artifacts (filesCreated, functions tested), then mark task as complete in tasks.md.

- [x] 12. Create DwsimValidator unit tests
  - File: `mcp_service/dwsim_worker/DwsimWorker.Tests/Engine/DwsimValidatorTests.cs`
  - Test ValidateInstantiation with loaded assemblies (returns success ValidationResult)
  - Test ValidateInstantiation without assemblies loaded (returns failure ValidationResult)
  - Test ValidateFlowsheetCreation (creates object, returns success with type name)
  - Test ValidateMaterialStreamCreation (creates object, returns success with type name)
  - Mock ILogger for testing logging calls
  - Purpose: Ensure DwsimValidator correctly validates assembly functionality
  - _Leverage: xUnit framework, Moq for ILogger, actual DWSIM assemblies or test setup_
  - _Requirements: 2.1, 2.2, 2.4, 2.5, 2.6_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer with expertise in integration testing and object instantiation validation | Task: Create unit tests for DwsimValidator following requirements 2.1, 2.2, 2.4-2.6. These tests may require actual DWSIM assemblies to be loaded (integration-level tests). Tests: 1) ValidateInstantiation_WithLoadedAssemblies_ReturnsSuccess, 2) ValidateInstantiation_WithoutAssemblies_ReturnsFailure, 3) ValidateFlowsheetCreation_Success, 4) ValidateMaterialStreamCreation_Success. Mock ILogger to verify logging. These tests validate that DWSIM objects can be instantiated without GUI context (requirement 2.3). | Restrictions: Must verify objects are not null (requirement 2.5), verify type names are logged (requirement 2.6), ensure no GUI dependencies trigger (no STA thread requirement per 2.3), mock ILogger, handle cases where DWSIM assemblies aren't available (skip tests with [Fact(Skip="")] if needed), follow naming conventions | _Leverage: xUnit framework, Moq for ILogger, DWSIM assemblies (if available in test environment) | _Requirements: 2.1, 2.2, 2.4, 2.5, 2.6 | Success: Validation methods tested thoroughly, success and failure cases covered, no GUI dependencies triggered, logging verified, type names correctly reported in ValidationResult. After completing, use log-implementation tool with detailed artifacts (filesCreated, functions tested), then mark task as complete in tasks.md.

- [x] 13. Create integration test for end-to-end assembly loading
  - File: `mcp_service/dwsim_worker/DwsimWorker.Tests/Integration/AssemblyLoadingIntegrationTests.cs`
  - Test full workflow: PathResolver → AssemblyLoader → DwsimValidator
  - Verify actual DWSIM assemblies load successfully
  - Verify Flowsheet and MaterialStream can be instantiated
  - Verify LoadResult contains all expected assemblies with correct versions
  - Test with environment variable configuration
  - Purpose: Validate end-to-end assembly loading with real DWSIM assemblies
  - _Leverage: xUnit framework, actual DWSIM installation or bundled assemblies_
  - _Requirements: All (end-to-end validation)_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Integration Test Engineer with expertise in end-to-end testing and system validation | Task: Create integration test covering full assembly loading workflow using real DWSIM assemblies. Test: 1) Set DWSIM_PATH environment variable to valid DWSIM installation, 2) Create AssemblyLoader with default config, 3) Call LoadDwsimAssemblies(), 4) Verify LoadResult.Success is true, 5) Verify all required assemblies loaded (DWSIM.Interfaces, DWSIM.Thermodynamics, DWSIM.SharedClasses, CapeOpen if available), 6) Verify assembly versions are logged, 7) Verify validation succeeded with type names. This test requires actual DWSIM installation. | Restrictions: Must use [Fact(Skip="")] or [Trait("Category", "Integration")] for CI skip if DWSIM not available, cleanup environment variables after test, verify no GUI windows appear during test (headless operation per requirement 4.1), test should run automatically without user interaction (per 4.2), verify exit code would be 0 (per 5.5) | _Leverage: xUnit framework, actual DWSIM assemblies, System.Environment for environment variables | _Requirements: All requirements (comprehensive end-to-end validation) | Success: Integration test passes with real DWSIM assemblies, full workflow validated from path resolution to validation, no GUI dependencies appear, test demonstrates headless operation, all assertions pass. After completing, use log-implementation tool with detailed artifacts (filesCreated, integration validated), then mark task as complete in tasks.md.

- [x] 14. Create manual testing script and documentation
  - Files:
    - `mcp_service/dwsim_worker/README.md` (update)
    - `mcp_service/dwsim_worker/test-assembly-loading.bat` (new)
  - Write README section explaining assembly loading requirements
  - Document environment variable configuration (DWSIM_PATH)
  - Document App.config configuration (appSettings["DwsimPath"])
  - Create batch script for manual testing (sets DWSIM_PATH, runs DwsimWorker.exe, checks exit code)
  - Add troubleshooting guide for common errors
  - Purpose: Enable manual testing and provide user documentation
  - _Leverage: Existing README structure_
  - _Requirements: 5.1, 5.2, 5.3, 5.6_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Technical Writer with expertise in developer documentation and testing procedures | Task: Create comprehensive documentation for assembly loading following requirements 5.1-5.3, 5.6. Update mcp_service/dwsim_worker/README.md with: 1) Assembly Loading Requirements section explaining DWSIM dependencies, 2) Configuration section covering environment variables and App.config, 3) Troubleshooting section with common errors and fixes. Create test-assembly-loading.bat script that: 1) Sets DWSIM_PATH to default or custom location, 2) Runs DwsimWorker.exe, 3) Checks %ERRORLEVEL% and prints success/failure message. Document all exit codes (0=success, 1=load failure, 2=validation failure, 3=config error per requirement 5.5). Include examples for Windows Server deployment (requirement 5.1). | Restrictions: Must provide clear, actionable error messages (per requirement 5.6), document all configuration options, include examples for different deployment scenarios, ensure scripts work on Windows 10/Server 2019+ (per requirement 5.1), follow markdown formatting for README | _Leverage: Existing README.md structure if available | _Requirements: 5.1, 5.2, 5.3, 5.6 | Success: README clearly documents all configuration options and troubleshooting steps, batch script successfully tests assembly loading, documentation enables users to deploy without issues, all exit codes documented. After completing, use log-implementation tool with detailed artifacts (filesCreated, filesModified), then mark task as complete in tasks.md.

- [x] 15. Performance testing and optimization
  - File: `mcp_service/dwsim_worker/DwsimWorker.Tests/Performance/AssemblyLoadingPerformanceTests.cs`
  - Create performance benchmark tests
  - Measure assembly loading time (target: less than 5 seconds total, less than 500ms per assembly)
  - Measure memory footprint after loading (target: less than 200MB)
  - Test concurrent assembly loading (multiple instances)
  - Identify and document performance bottlenecks
  - Purpose: Validate performance requirements and establish baselines
  - _Leverage: xUnit framework, System.Diagnostics.Stopwatch, System.Diagnostics.Process for memory measurement_
  - _Requirements: Performance requirements (5s total, 500ms per assembly, less than 200MB RAM)_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Performance Engineer with expertise in .NET profiling and benchmarking | Task: Create performance tests to validate assembly loading meets performance requirements: total time less than 5 seconds, per-assembly time less than 500ms, memory footprint less than 200MB. Tests: 1) MeasureAssemblyLoadingTime_WithinTargets (use Stopwatch to measure LoadDwsimAssemblies duration, assert less than 5000ms), 2) MeasurePerAssemblyTime_WithinTarget (measure each assembly individually, assert less than 500ms each), 3) MeasureMemoryFootprint_WithinTarget (use Process.GetCurrentProcess().WorkingSet64 before and after loading, assert delta less than 200MB), 4) TestConcurrentInstances_NoConflicts (run multiple DwsimWorker instances simultaneously, verify no file locking issues). Log performance metrics. | Restrictions: Must use [Trait("Category", "Performance")] for CI separation, measure on typical hardware (4-core, 8GB RAM, SSD) or document test environment, account for cold start vs warm start, ensure tests don't fail intermittently due to system load, log detailed metrics for analysis | _Leverage: xUnit framework, System.Diagnostics.Stopwatch for timing, System.Diagnostics.Process for memory measurement | _Requirements: Performance (5s total, 500ms/assembly, less than 200MB) | Success: All performance tests pass on target hardware, assembly loading meets performance targets, memory usage within limits, concurrent instances work without conflicts, performance baselines established and documented. After completing, use log-implementation tool with detailed artifacts (filesCreated, performance metrics), then mark task as complete in tasks.md.

- [x] 16. Final integration, validation, and cleanup
  - Review all implemented components for consistency
  - Run all unit tests and integration tests
  - Verify assembly loading works on clean Windows Server 2022 VM
  - Test with different DWSIM versions if available
  - Update XML documentation comments for all public APIs
  - Verify code follows structure.md conventions (naming, one file per class, etc.)
  - Clean up any debug code or commented code
  - Ensure all TODO comments are addressed or documented
  - Purpose: Final validation and quality assurance
  - _Leverage: All implemented components_
  - _Requirements: All requirements_
  - _Prompt: Implement the task for spec dwsim-assembly-loader, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Senior Developer and QA Lead with expertise in code quality and system integration | Task: Perform comprehensive final integration and validation of DWSIM Assembly Loader. Steps: 1) Run all unit tests (dotnet test), ensure 100% pass rate, 2) Run integration tests with real DWSIM, 3) Verify code follows structure.md: one file per class, PascalCase naming, proper folder organization (Engine/, Utilities/), 4) Check all public classes/methods have XML documentation comments (///), 5) Test on clean Windows Server 2022 VM with DWSIM installed, verify exit code 0, 6) Test error scenarios: missing assemblies, wrong path, verify appropriate exit codes and error messages, 7) Review all files for code quality: no commented code, no debug statements, no TODO comments (or document them), 8) Verify all requirements met (checklist review of requirements.md), 9) Run code coverage analysis, target >80%, 10) Document any known limitations or issues. | Restrictions: Must not break any existing functionality, ensure backward compatibility, maintain all test passing, verify headless operation (no GUI), confirm all exit codes work correctly, ensure error messages are clear and actionable | _Leverage: All implemented components (AssemblyLoader, DwsimValidator, PathResolver, etc.), dotnet test CLI, code coverage tools | _Requirements: All requirements (comprehensive final validation) | Success: All tests pass, code meets quality standards, assembly loading works reliably on clean Windows Server, all requirements validated and met, no critical issues found, documentation complete and accurate. After completing, use log-implementation tool with comprehensive artifacts documenting all components, integration points, and validation results, then mark task as complete in tasks.md.
