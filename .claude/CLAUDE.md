# Building

## Build Command

Always use this exact command to build:
```bash
cd "D:\S\C#\dwsim_interop_services\mcp_service\dwsim_worker" && ./build.bat 2>&1

Key points:
- Change to the mcp_service\dwsim_worker directory first
- Use ./build.bat (not cmd /c build.bat or just build.bat)
- Include 2>&1 to capture all output including errors


# Task Management

use the spec workflow mcp to update tasks in the tasks.md of the spec you are working on.

# Repo Management

commit changes at the end of each task you complete.

use conventional commit standard.