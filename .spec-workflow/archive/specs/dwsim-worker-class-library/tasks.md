# Tasks Document

## Overview

This is a minimal conversion requiring only 4 core implementation tasks. The changes are surgical and preserve all existing functionality.

## Implementation Tasks

- [x] 1. Convert project output type from Exe to Library
  - **Files**: `mcp_service/dwsim_worker/DwsimWorker/DwsimWorker.csproj`
  - **Description**: Change `<OutputType>Exe</OutputType>` to `<OutputType>Library</OutputType>` in project configuration
  - **Purpose**: Enable DLL compilation instead of executable for pythonnet loading
  - **_Leverage**: Existing .csproj structure, no new dependencies
  - **_Requirements**: 1.1, 1.2
  - **_Prompt**: Implement the task for spec dwsim-worker-class-library, first run spec-workflow-guide to get the workflow guide then implement the task: Role: .NET Build Engineer specializing in MSBuild and project configurations | Task: Modify DwsimWorker.csproj to change OutputType from Exe to Library following requirements 1.1 and 1.2. Open the file, locate the `<OutputType>` element (line 8), and change its value from `Exe` to `Library`. | Restrictions: Do not modify any other project settings, do not change target framework, do not alter package references, do not modify build configurations | Success: DwsimWorker.csproj has `<OutputType>Library</OutputType>`, file builds successfully producing DwsimWorker.dll instead of DwsimWorker.exe | Instructions: After implementing, mark this task as in-progress [-] in tasks.md, then complete the implementation, log it using log-implementation tool with detailed artifacts (project configuration changes, build output verification), and mark as completed [x] in tasks.md

- [x] 2. Exclude Program.cs from compilation
  - **Files**: `mcp_service/dwsim_worker/DwsimWorker/DwsimWorker.csproj`
  - **Description**: Remove `<Compile Include="Program.cs" />` line from ItemGroup or change to `<None Include="Program.cs" />`
  - **Purpose**: Remove console application entry point from library output
  - **_Leverage**: Existing .csproj ItemGroup structure
  - **_Requirements**: 1.2, 2.1
  - **_Prompt**: Implement the task for spec dwsim-worker-class-library, first run spec-workflow-guide to get the workflow guide then implement the task: Role: .NET Build Engineer with expertise in project file management | Task: Exclude Program.cs from compilation following requirements 1.2 and 2.1. Open DwsimWorker.csproj, locate line 68 `<Compile Include="Program.cs" />`, and either remove it entirely or change to `<None Include="Program.cs" />` to keep the file for reference. Recommended: change to `<None>` to preserve git history. | Restrictions: Do not delete the actual Program.cs file from disk, do not modify other <Compile> entries, ensure Properties/AssemblyInfo.cs remains included | Success: Program.cs is excluded from compilation, no Main() method in output DLL, file still exists on disk for reference | Instructions: After implementing, mark this task as in-progress [-] in tasks.md, then complete the implementation, log it using log-implementation tool with detailed artifacts (project file modification, compilation exclusion verification), and mark as completed [x] in tasks.md

- [x] 3. Add XML documentation comments to public APIs
  - **Files**: `mcp_service/dwsim_worker/DwsimWorker/Engine/SessionManager.cs`, `mcp_service/dwsim_worker/DwsimWorker/Adapters/*.cs`, `mcp_service/dwsim_worker/DwsimWorker/Converters/*.cs`, `mcp_service/dwsim_worker/DwsimWorker/Models/*.cs`, `mcp_service/dwsim_worker/DwsimWorker/Contracts/**/*.cs`
  - **Description**: Enhance existing XML documentation comments with pythonnet usage examples and parameter descriptions
  - **Purpose**: Provide IntelliSense hints for Python developers using pythonnet
  - **_Leverage**: Existing XML documentation structure (SessionManager already has /// comments)
  - **_Requirements**: 5.1, 5.2
  - **_Prompt**: Implement the task for spec dwsim-worker-class-library, first run spec-workflow-guide to get the workflow guide then implement the task: Role: Documentation Engineer with expertise in XML documentation and API documentation best practices | Task: Add/enhance XML documentation comments for all public classes and methods following requirements 5.1 and 5.2. Focus on: SessionManager public methods, Adapter classes (StreamAdapter, CalculationAdapter, etc.), DTO classes properties, Converter methods. Use `/// <summary>`, `/// <param>`, `/// <returns>`, and `/// <exception>` tags. Include pythonnet usage examples where helpful. | Restrictions: Do not modify method signatures, do not add documentation to internal/private members, follow existing documentation style, keep descriptions concise and technical | Success: All public APIs have XML documentation, parameter and return types documented, exceptions documented, examples included for key methods | Instructions: After implementing, mark this task as in-progress [-] in tasks.md, then complete the implementation, log it using log-implementation tool with detailed artifacts (classes documented, methods documented, examples added), and mark as completed [x] in tasks.md

- [x] 4. Enable XML documentation file generation
  - **Files**: `mcp_service/dwsim_worker/DwsimWorker/DwsimWorker.csproj`
  - **Description**: Add `<DocumentationFile>` element to PropertyGroup to generate DwsimWorker.xml alongside DLL
  - **Purpose**: Generate XML file for pythonnet IntelliSense support
  - **_Leverage**: MSBuild documentation file generation
  - **_Requirements**: 5.2
  - **_Prompt**: Implement the task for spec dwsim-worker-class-library, first run spec-workflow-guide to get the workflow guide then implement the task: Role: .NET Build Engineer with expertise in MSBuild configuration and documentation generation | Task: Enable XML documentation file generation following requirement 5.2. Open DwsimWorker.csproj, add `<DocumentationFile>bin\$(Configuration)\DwsimWorker.xml</DocumentationFile>` to the first `<PropertyGroup>` (after line 16). This generates XML docs in both Debug and Release builds. | Restrictions: Do not modify existing PropertyGroup conditions, do not add separate PropertyGroups for Debug/Release, use $(Configuration) variable for path | Success: DwsimWorker.xml is generated in bin\Debug and bin\Release during build, XML file contains documentation for all public APIs | Instructions: After implementing, mark this task as in-progress [-] in tasks.md, then complete the implementation, log it using log-implementation tool with detailed artifacts (project configuration change, XML file generation verification), and mark as completed [x] in tasks.md

- [x] 5. Build and verify all unit tests pass
  - **Files**: `mcp_service/dwsim_worker/DwsimWorker.sln` (build), `mcp_service/dwsim_worker/DwsimWorker.Tests/` (test project)
  - **Description**: Build the solution using build.bat and run all unit tests to verify no regressions
  - **Purpose**: Validate that conversion to library does not break any existing functionality
  - **_Leverage**: Existing build.bat script, existing test suite (89 tests), existing test infrastructure
  - **_Requirements**: 4.1, 4.2, 4.3, 4.4
  - **_Prompt**: Implement the task for spec dwsim-worker-class-library, first run spec-workflow-guide to get the workflow guide then implement the task: Role: QA Engineer with expertise in .NET testing and regression testing | Task: Build DwsimWorker and run full test suite following requirements 4.1-4.4. Execute: `cd "D:\S\C#\dwsim_interop_services\mcp_service\dwsim_worker" && ./build.bat 2>&1`. Verify build produces DwsimWorker.dll (not .exe). Run tests with `dotnet test DwsimWorker.Tests/DwsimWorker.Tests.csproj`. All 89 tests must pass. | Restrictions: Do not modify test code, do not skip failing tests, do not change build configuration, ensure tests run in clean environment | Success: Build completes successfully producing DwsimWorker.dll, all unit tests pass (89/89), no build warnings related to Main method or entry point, DwsimWorker.xml generated | Instructions: After implementing, mark this task as in-progress [-] in tasks.md, then complete the implementation, log it using log-implementation tool with detailed artifacts (build output, test results, DLL verification), and mark as completed [x] in tasks.md

- [x] 6. Create pythonnet integration test script
  - **Files**: `mcp_service/dwsim_worker/test_pythonnet_loading.py` (new file)
  - **Description**: Create Python script that loads DwsimWorker.dll via pythonnet and instantiates SessionManager
  - **Purpose**: Validate DLL is pythonnet-compatible and APIs are accessible from Python
  - **_Leverage**: pythonnet clr module, existing SessionManager public API
  - **_Requirements**: 1.4, 3.1
  - **_Prompt**: implement the task for spec dwsim-worker-class-library by first initialising the pyhton environment for this project using uv, then first run spec-workflow-guide to get the workflow guide then implement the task: Role: Integration Engineer with expertise in pythonnet and cross-platform interop | Task: Create integration test script following requirements 1.4 and 3.1. Create `test_pythonnet_loading.py` in dwsim_worker directory. Script should: 1) Import clr from pythonnet, 2) Add reference to DwsimWorker.dll (bin\Debug\DwsimWorker.dll), 3) Import SessionManager, 4) Verify it can be instantiated (mock ILogger and config if needed), 5) Print success message. Handle exceptions clearly. | Restrictions: Do not install pythonnet yet (just create script), do not modify C# code, use absolute paths or detect bin directory, handle missing dependencies gracefully | Success: Script created with clear comments, attempts to load DwsimWorker.dll and import SessionManager, provides clear success/failure messages, includes dependency check instructions | Instructions: After implementing, mark this task as in-progress [-] in tasks.md, then complete the implementation, log it using log-implementation tool with detailed artifacts (script created, import structure, test approach), and mark as completed [x] in tasks.md

## Post-Implementation Verification

After all tasks completed:

1. **Build Verification**:
   - `bin\Debug\DwsimWorker.dll` exists (not .exe)
   - `bin\Debug\DwsimWorker.xml` exists
   - `bin\Debug\DwsimWorker.pdb` exists

2. **Test Verification**:
   - All 89 unit tests pass
   - No build warnings about Main method
   - No entry point errors

3. **pythonnet Readiness** (Future - not this spec):
   - Integration test script ready for execution
   - Next spec (3.2: pythonnet-bridge) will install pythonnet and execute test

## Dependencies

**Before Implementation:**
- .NET Framework 4.8 SDK installed
- Visual Studio 2019/2022 or MSBuild available
- Existing DwsimWorker codebase (completed specs 1.1-2.3)

**After Implementation:**
- DwsimWorker.dll loadable by pythonnet
- Ready for Spec 3.2 (pythonnet bridge implementation)

## Estimated Effort

- Task 1: 2 minutes (1 line change)
- Task 2: 2 minutes (1 line change)
- Task 3: 30-45 minutes (documentation enhancement)
- Task 4: 2 minutes (1 line addition)
- Task 5: 5 minutes (build and test execution)
- Task 6: 15 minutes (create Python test script)

**Total**: ~1 hour

## Rollback Plan

If issues arise:
1. Revert .csproj changes (git restore)
2. Build and verify original .exe still works
3. Investigate specific failure before re-attempting

All changes are isolated to 2 files (.csproj and test script), making rollback trivial.
