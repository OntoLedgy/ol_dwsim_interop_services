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

Always build first using build.bat, then run tests via the build system:
```bash
cd "D:\S\C#\dwsim_interop_services\mcp_service\dwsim_worker" && ./build.bat 2>&1
```

The build.bat handles:
- Proper dependency resolution
- NuGet package restoration
- Compilation with correct references
- Test execution

If you need to run specific tests, still use build.bat first to ensure proper compilation.


# Task Management

use the spec workflow mcp to update tasks in the tasks.md of the spec you are working on.

# Repo Management

commit changes at the end of each task you complete.

use conventional commit standard.