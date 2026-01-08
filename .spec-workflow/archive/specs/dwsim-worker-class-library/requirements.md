# Requirements Document

## Introduction

This specification defines the requirements for converting DwsimWorker from a console application (Exe) to a class library (DLL). This refactoring is necessary to enable pythonnet in-process interop, eliminating the need for JSON-RPC communication and simplifying the architecture to a single-process model. The class library will be loaded directly by the Python MCP server via pythonnet, providing zero-overhead method calls to DWSIM simulation functionality.

## Alignment with Product Vision

This feature directly supports the architectural decision (documented in docs/architecture/interop-strategy.md) to adopt Python + pythonnet as the primary interop strategy. By converting DwsimWorker to a class library:

- **Simplifies Deployment**: Single process eliminates IPC complexity and process management overhead
- **Improves Performance**: Zero IPC overhead with direct in-process method calls
- **Right Language for Users**: Enables Python-native MCP server interface for chemical engineers
- **Faster Development**: Eliminates need to implement JSON-RPC server/client (saves 1-2 weeks)
- **Maintains Safety**: All existing exception handling, resource management, and testing remains intact

This aligns with the product vision of making DWSIM accessible to LLM agents through safe, performant, and user-friendly interfaces.

## Requirements

### Requirement 1: Convert Output Type to Class Library

**User Story:** As a Python developer, I want DwsimWorker to be a class library (DLL), so that I can load it via pythonnet and call its methods directly without IPC overhead.

#### Acceptance Criteria

1. WHEN DwsimWorker.csproj is built THEN it SHALL produce DwsimWorker.dll (not DwsimWorker.exe)
2. WHEN the project configuration is examined THEN `<OutputType>` SHALL be set to `Library`
3. WHEN the DLL is referenced by another .NET project THEN it SHALL successfully expose public types
4. WHEN pythonnet attempts to load the DLL THEN it SHALL load without errors

### Requirement 2: Remove Console Application Entry Point

**User Story:** As a maintainer, I want the console application entry point removed, so that the codebase only contains library code and no unused executable logic.

#### Acceptance Criteria

1. WHEN the project files are examined THEN Program.cs (or equivalent entry point file) SHALL NOT exist or SHALL be excluded from compilation
2. WHEN the project is built THEN no Main() method SHALL be compiled into the output
3. IF Program.cs is kept for reference THEN it SHALL be moved to a separate documentation or archive location

### Requirement 3: Expose Public API Surface

**User Story:** As a Python developer using pythonnet, I want all necessary classes and methods to be public, so that I can call them from Python.

#### Acceptance Criteria

1. WHEN SessionManager class is inspected THEN it SHALL have public access modifier
2. WHEN Adapter classes (StreamAdapter, CalculationAdapter, etc.) are inspected THEN they SHALL have public access modifiers
3. WHEN DTO classes (MaterialStreamDto, CalculationResult, etc.) are inspected THEN they SHALL have public access modifiers and public properties
4. WHEN Exception types are inspected THEN they SHALL have public access modifiers
5. IF any class or method is intended for external use THEN it SHALL be marked public
6. IF any class or method is internal-only THEN it MAY remain internal or private

### Requirement 4: Preserve Existing Functionality

**User Story:** As a developer maintaining DwsimWorker, I want all existing functionality preserved, so that the conversion does not break any existing code or tests.

#### Acceptance Criteria

1. WHEN all existing unit tests are run THEN they SHALL pass without modification
2. WHEN SessionManager functionality is tested THEN it SHALL create/close sessions as before
3. WHEN Adapter functionality is tested THEN it SHALL perform DWSIM operations as before
4. WHEN DTO serialization is tested THEN it SHALL serialize/deserialize correctly as before
5. WHEN CAPE-OPEN conversion is tested THEN it SHALL convert data correctly as before
6. IF any test fails after conversion THEN the issue SHALL be fixed before proceeding

### Requirement 5: Add XML Documentation Comments

**User Story:** As a Python developer using pythonnet, I want XML documentation on public APIs, so that I can understand how to use the classes and methods.

#### Acceptance Criteria

1. WHEN SessionManager public methods are examined THEN they SHALL have XML documentation comments (`/// <summary>`)
2. WHEN Adapter classes public methods are examined THEN they SHALL have XML documentation comments
3. WHEN DTO classes are examined THEN public properties SHALL have XML documentation comments
4. WHEN method parameters are examined THEN they SHALL have `<param>` documentation
5. WHEN method return values are examined THEN they SHALL have `<returns>` documentation
6. IF a method throws exceptions THEN it SHALL document them with `<exception>` tags

### Requirement 6: Build Configuration Consistency

**User Story:** As a developer, I want build configurations to work correctly, so that Debug and Release builds produce valid DLL files.

#### Acceptance Criteria

1. WHEN Debug configuration is built THEN it SHALL produce DwsimWorker.dll with debug symbols
2. WHEN Release configuration is built THEN it SHALL produce optimized DwsimWorker.dll
3. WHEN either configuration is built THEN no build warnings related to entry points SHALL appear
4. WHEN the output path is examined THEN DLL SHALL be in expected bin\Debug or bin\Release directory

## Non-Functional Requirements

### Code Architecture and Modularity
- **Single Responsibility Principle**: Each class maintains its current single responsibility
- **Modular Design**: Existing modularity (SessionManager, Adapters, Converters, Utilities) is preserved
- **Dependency Management**: No new dependencies introduced; existing DWSIM references unchanged
- **Clear Interfaces**: Public API surface clearly documented and intentional

### Performance
- **Build Time**: Conversion SHALL NOT significantly increase build time (< 5% increase acceptable)
- **Runtime Performance**: No performance regression; DLL loading via pythonnet should be faster than IPC
- **Memory Footprint**: No increase in memory usage compared to console application baseline

### Security
- **Access Control**: Only intentionally public APIs exposed; internal implementation details remain private
- **Dependency Integrity**: All existing dependency references (DWSIM assemblies, Newtonsoft.Json, Serilog) maintained with same versions

### Reliability
- **Test Coverage**: All existing tests pass without modification
- **Error Handling**: All existing exception handling preserved
- **Resource Management**: All existing IDisposable patterns and resource cleanup preserved
- **Thread Safety**: Existing thread safety mechanisms (STA threading, session isolation) unchanged

### Usability
- **pythonnet Compatibility**: DLL structure compatible with pythonnet's assembly loading mechanism
- **Discoverability**: Public API well-documented and easy to discover via IntelliSense or reflection
- **Versioning**: Assembly version information maintained for compatibility tracking
