# Requirements Document

## Introduction

The DWSIM Assembly Loader is the foundational component that validates the most critical technical assumption of the DWSIM MCP Server project: **can we programmatically load and invoke DWSIM's .NET Framework libraries in a headless, server environment?**

This component serves as a proof-of-concept that demonstrates successful loading, initialization, and basic invocation of DWSIM assemblies without requiring GUI dependencies, COM registration, or interactive user input. It validates that DWSIM can be used as a programmable simulation engine rather than just a desktop application.

This is the first and most critical spec in the fail-fast development strategy. Without successful completion of this component, the entire MCP server architecture cannot proceed, as all subsequent components depend on reliable DWSIM interoperability.

**Value to Project:**
- **Risk Mitigation**: Identifies blockers early before investing in RPC infrastructure, Python integration, or MCP tools
- **Technical Validation**: Proves DWSIM assemblies are compatible with server-side hosting
- **Foundation**: Establishes patterns for assembly loading, error handling, and configuration that all other components will reuse
- **Confidence**: Provides concrete evidence that the polyglot architecture is viable

## Alignment with Product Vision

This component directly supports the Product Vision outlined in product.md:

**Product Purpose Alignment:**
- Validates that DWSIM's simulation engine can be exposed to LLM agents programmatically
- Proves the feasibility of the "MCP server that exposes DWSIM's powerful chemical process simulation engine" vision
- Establishes that DWSIM can operate without GUI dependencies, enabling the "safe, composable tools and resources" approach

**Architecture Alignment (from tech.md):**
- Implements the foundation of the ".NET Framework 4.8 engine worker hosting DWSIM assemblies"
- Validates the "Clean separation enabling independent scaling and maintenance" between Python and .NET
- Proves the "DWSIM-Native" principle: "Leverage DWSIM's existing APIs and conventions rather than creating parallel abstractions"

**Product Principles:**
- **Safety First**: Demonstrates headless operation (no runaway GUI windows)
- **DWSIM-Native**: Direct assembly loading without middleware or wrappers
- **Observable by Default**: Establishes logging patterns for assembly loading diagnostics

**Technical Requirements Validation:**
- Confirms .NET Framework 4.8 compatibility
- Proves Windows 10/Server deployment viability
- Validates that DWSIM 6.x+ assemblies are programmable

## Requirements

### Requirement 1: Load DWSIM Core Assemblies

**User Story:** As a C# engine worker developer, I want to load DWSIM's core assemblies (DWSIM.Interfaces, DWSIM.Thermodynamics) into the application domain, so that I can programmatically access DWSIM types and methods.

#### Acceptance Criteria

1. WHEN the application starts THEN the system SHALL load DWSIM.Interfaces.dll without exceptions
2. WHEN the application starts THEN the system SHALL load DWSIM.Thermodynamics.dll without exceptions
3. WHEN the application starts THEN the system SHALL load DWSIM.SharedClasses.dll without exceptions
4. IF any required assembly is missing THEN the system SHALL throw FileNotFoundException with clear error message indicating which assembly is missing and expected path
5. IF assembly version mismatch occurs THEN the system SHALL log warning with actual vs expected version
6. WHEN assemblies are loaded THEN the system SHALL verify that key types (e.g., DWSIM.Thermodynamics.Streams.MaterialStream, DWSIM.SharedClasses.Flowsheet) are accessible
7. WHEN assembly loading completes THEN the system SHALL log success message with loaded assembly names and versions

### Requirement 2: Instantiate Core DWSIM Objects

**User Story:** As a C# engine worker developer, I want to instantiate core DWSIM objects (Flowsheet, MaterialStream), so that I can verify programmatic access to DWSIM functionality.

#### Acceptance Criteria

1. WHEN assemblies are loaded THEN the system SHALL successfully instantiate a DWSIM.SharedClasses.Flowsheet object
2. WHEN assemblies are loaded THEN the system SHALL successfully instantiate a DWSIM.Thermodynamics.Streams.MaterialStream object
3. WHEN instantiating Flowsheet THEN the system SHALL NOT require GUI context (no STA thread requirement for instantiation)
4. WHEN instantiating objects THEN the system SHALL handle exceptions gracefully with clear error messages
5. WHEN objects are instantiated THEN the system SHALL verify object is not null
6. WHEN objects are instantiated THEN the system SHALL retrieve and log object type names
7. IF object instantiation fails THEN the system SHALL log detailed exception information including inner exceptions

### Requirement 3: Handle Assembly Dependencies and Binding Redirects

**User Story:** As a deployment engineer, I want the application to resolve DWSIM assembly dependencies automatically, so that I don't need to manually configure binding redirects or GAC registration.

#### Acceptance Criteria

1. IF DWSIM assemblies reference different versions of dependencies (e.g., Newtonsoft.Json) THEN the system SHALL resolve version conflicts using binding redirects in App.config
2. WHEN assembly dependencies are missing THEN the system SHALL provide clear error messages listing missing dependencies
3. WHEN the application loads assemblies THEN the system SHALL search for DWSIM assemblies in configurable paths (environment variable or config file)
4. IF assembly loading fails due to dependency issues THEN the system SHALL log the full exception chain including FileLoadException details
5. WHEN assembly resolution occurs THEN the system SHALL log resolved assembly paths for diagnostics

### Requirement 4: Validate Headless Operation

**User Story:** As a system administrator, I want the application to run without any GUI dependencies, so that I can deploy it on Windows Server without desktop experience installed.

#### Acceptance Criteria

1. WHEN the application runs THEN the system SHALL NOT display any windows, dialogs, or message boxes
2. WHEN the application runs THEN the system SHALL NOT require user interaction (no blocking prompts)
3. WHEN the application runs THEN the system SHALL complete and exit automatically
4. WHEN the application runs on Windows Server Core THEN the system SHALL execute successfully without GUI libraries
5. WHEN DWSIM objects are created THEN the system SHALL NOT trigger GUI initialization (no System.Windows.Forms dependencies)

### Requirement 5: Report Assembly Versions and Configuration

**User Story:** As a DevOps engineer, I want detailed diagnostics about loaded assemblies and configuration, so that I can troubleshoot deployment issues and verify compatibility.

#### Acceptance Criteria

1. WHEN the application starts THEN the system SHALL log .NET Framework version (should be 4.8)
2. WHEN the application starts THEN the system SHALL log target platform (x64 or x86)
3. WHEN assemblies are loaded THEN the system SHALL log each DWSIM assembly name, version, and file path
4. WHEN the application completes THEN the system SHALL log success or failure status
5. WHEN any errors occur THEN the system SHALL log detailed exception information including stack traces
6. WHEN the application runs THEN the system SHALL output all logs to console (stdout) for capture by orchestration tools

### Requirement 6: Verify No COM Registration Required

**User Story:** As a deployment engineer, I want to verify that DWSIM assemblies don't require COM registration, so that I can deploy without administrator privileges for COM registration.

#### Acceptance Criteria

1. WHEN the application runs THEN the system SHALL successfully load and use DWSIM assemblies without prior COM registration (regsvr32 or similar)
2. WHEN the application runs THEN the system SHALL NOT call CoCreateInstance or other COM interop APIs that require registered COM components
3. WHEN the application runs as a standard user (non-administrator) THEN the system SHALL successfully load assemblies (no registry access required)

## Non-Functional Requirements

### Code Architecture and Modularity

- **Single Responsibility Principle**:
  - Program.cs: Application entry point and orchestration only
  - AssemblyLoader.cs: Assembly loading and verification logic only
  - DwsimValidator.cs: DWSIM object instantiation and validation only
  - Logger.cs: Console logging utilities only

- **Modular Design**:
  - Assembly loading logic isolated in dedicated class for reuse in future worker components
  - Validation logic separated from loading logic for independent testing
  - Logger abstraction to enable future replacement with structured logging (Serilog)

- **Dependency Management**:
  - No dependencies on Python or MCP SDK at this stage
  - Only dependencies: DWSIM assemblies and .NET Framework 4.8 BCL
  - No third-party NuGet packages in MVP (keep it simple)

- **Clear Interfaces**:
  - AssemblyLoader exposes LoadAssemblies() method returning LoadResult status object
  - DwsimValidator exposes ValidateInstantiation() method returning ValidationResult
  - Logger exposes LogInfo, LogWarning, LogError methods

### Performance

- **Startup Time**: Assembly loading and validation SHALL complete within 5 seconds on typical hardware (4-core CPU, 8GB RAM, SSD)
- **Memory Footprint**: Application SHALL consume less than 200MB of RAM after loading all assemblies
- **Assembly Loading**: Each assembly SHALL load within 500ms

### Security

- **Path Traversal**: If assembly paths are configurable, the system SHALL validate paths to prevent directory traversal attacks
- **Code Execution**: The system SHALL only load assemblies from trusted paths (not arbitrary user-provided paths)
- **Exception Handling**: The system SHALL NOT expose sensitive system information in error messages (no full file paths in production logs)

### Reliability

- **Error Recovery**: If one assembly fails to load, the system SHALL report the error and exit gracefully (no crashes)
- **Deterministic Behavior**: The application SHALL produce consistent results across multiple runs with same configuration
- **Clean Shutdown**: The application SHALL dispose of all loaded assemblies and exit cleanly (no hanging processes)

### Usability

- **Clear Error Messages**: All error messages SHALL clearly indicate:
  - What went wrong (e.g., "Failed to load DWSIM.Interfaces.dll")
  - Why it went wrong (e.g., "File not found at path C:\\DWSIM\\Assemblies")
  - How to fix it (e.g., "Ensure DWSIM is installed or set DWSIM_PATH environment variable")

- **Diagnostic Output**: The application SHALL output structured diagnostic information suitable for log aggregation tools:
  - Timestamp for each log entry
  - Log level (INFO, WARNING, ERROR)
  - Clear, actionable messages

- **Exit Codes**: The application SHALL use standard exit codes:
  - 0: Success (all assemblies loaded and validated)
  - 1: Assembly loading failed
  - 2: Assembly validation failed (loaded but cannot instantiate objects)
  - 3: Configuration error

### Deployment

- **Portability**: The application SHALL run on:
  - Windows 10 version 1809 or later
  - Windows Server 2019 or later
  - Windows Server 2022

- **Dependencies**: The application SHALL require only:
  - .NET Framework 4.8 runtime (pre-installed on modern Windows)
  - DWSIM assemblies (either installed DWSIM or bundled assemblies)

- **Configuration**: The application SHALL support configuration via:
  - App.config file (for binding redirects)
  - Environment variable: DWSIM_PATH (path to DWSIM assemblies)
  - Command-line argument: --dwsim-path <path> (overrides environment variable)

## Success Metrics

**Critical Success Criteria (Must achieve 100%):**
1. DWSIM assemblies load without exceptions on clean Windows Server 2022 installation
2. Flowsheet and MaterialStream objects instantiate successfully
3. Application runs headless (no GUI components)
4. Application exits cleanly with exit code 0

**Validation Metrics:**
- Zero COM registration requirements
- Zero manual binding redirect configuration (all in App.config)
- Assembly loading latency < 500ms per assembly
- Total execution time < 5 seconds

**Quality Metrics:**
- All error messages include actionable remediation steps
- Logs are structured and parseable
- Exit codes correctly reflect failure modes
