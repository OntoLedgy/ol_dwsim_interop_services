# Building and Testing

## Build Command

Always use this exact command to build:
```bash
cd "D:\S\C#\dwsim_interop_services\mcp_service\dwsim_worker" && ./build.bat 2>&1
```

Key points:
- Change to the mcp_service\dwsim_worker directory first
- Use ./build.bat (not cmd /c build.bat or just build.bat)
- Include 2>&1 to capture all output including errors
- **NEVER** use `dotnet test` or `dotnet build` directly - always use build.bat

## Running Tests

**CRITICAL: Do NOT run `dotnet test` directly - it will cause Serilog and dependency errors!**

### Build First
Always build first using build.bat to ensure proper compilation:
```bash
cd "D:\S\C#\dwsim_interop_services\mcp_service\dwsim_worker" && ./build.bat 2>&1
```

The build.bat handles:
- Proper dependency resolution
- NuGet package restoration
- Compilation with correct references

**NOTE**: build.bat does NOT run tests automatically - you must run them separately using vstest.console.exe.

### Running All Tests
After building, run all tests using Visual Studio Test Console (from VS Professional 18):
```bash
"D:\Apps\Microsoft Visual Studio\18\Professional\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" "D:\S\C#\dwsim_interop_services\mcp_service\dwsim_worker\DwsimWorker.Tests\bin\Debug\DwsimWorker.Tests.dll" --logger:"console;verbosity=detailed"
```

### Running Specific Tests
To run a specific test by name:
```bash
"D:\Apps\Microsoft Visual Studio\18\Professional\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" "D:\S\C#\dwsim_interop_services\mcp_service\dwsim_worker\DwsimWorker.Tests\bin\Debug\DwsimWorker.Tests.dll" --Tests:GoldenTest_ThreePhaseSeparatorCalculation_Succeeds --logger:"console;verbosity=detailed"
```

### Test Output Tips
- Add `| tail -200` to see the last 200 lines of output
- Logs will show detailed Serilog output from the test execution
- Test results appear at the end with pass/fail status
- Use `2>&1` if you need to capture stderr as well


# Task Management

use the spec workflow mcp to update tasks in the tasks.md of the spec you are working on.

# Repo Management

commit changes at the end of each task you complete.

use conventional commit standard.