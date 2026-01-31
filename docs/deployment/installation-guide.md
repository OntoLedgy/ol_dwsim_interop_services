# DWSIM MCP Server Installation Guide

This guide explains how to install the DWSIM MCP Server on Windows.

## Quick Start (Prerequisites Already Installed)

If you already have **Git**, **Python 3.12**, and **Visual Studio Build Tools** installed:

```powershell
# 1. Create install directory (if you can write to C:\)
mkdir C:\DwsimMcp

# 2. Download and run the setup script
cd C:\DwsimMcp
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/YOUR_ORG/dwsim_interop_services/develop/scripts/setup-user.ps1" -OutFile "setup-user.ps1"
.\setup-user.ps1 -RepoUrl "https://github.com/YOUR_ORG/dwsim_interop_services.git"

# 3. Start the server
.\start-http.bat
```

## Installation Options

### Option A: User-Only Installation (Recommended)

**Use this if prerequisites are already installed.**

No Administrator privileges required.

| Step | Command | Notes |
|------|---------|-------|
| 1 | `mkdir C:\DwsimMcp` | Or any writable location |
| 2 | `cd C:\DwsimMcp` | |
| 3 | `.\setup-user.ps1 -RepoUrl "..."` | Clones, builds, configures |
| 4 | `.\start-http.bat` | Starts HTTP server on port 8000 |

### Option B: Two-Phase Installation (New Machine)

**Use this for a fresh Windows machine with nothing installed.**

#### Phase 1: Install Prerequisites (Administrator)

Run **once** as Administrator:

```powershell
# Download the prerequisites script
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/YOUR_ORG/dwsim_interop_services/develop/scripts/setup-prerequisites.ps1" -OutFile "setup-prerequisites.ps1"

# Run as Administrator
.\setup-prerequisites.ps1 -InstallPath "C:\DwsimMcp" -Username "YourUsername"
```

This installs:
- Chocolatey (package manager)
- Git
- Python 3.12
- Visual Studio Build Tools 2022
- Creates `C:\DwsimMcp` with user write permissions

#### Phase 2: Setup Server (Normal User)

Run as your normal user account:

```powershell
cd C:\DwsimMcp
.\setup-user.ps1 -RepoUrl "https://github.com/YOUR_ORG/dwsim_interop_services.git"
```

### Option C: Full Automated Installation (Administrator)

**Use this if you want a single script that does everything.**

```powershell
# Run as Administrator
.\scripts\setup-windows-server.ps1 -RepoUrl "https://github.com/YOUR_ORG/dwsim_interop_services.git"
```

> **Warning**: This creates files owned by Administrator, which can cause permission issues during development/debugging.

## Prerequisites

The following must be installed before running `setup-user.ps1`:

| Component | Version | Check Command |
|-----------|---------|---------------|
| Git | Any | `git --version` |
| Python | 3.12+ | `python --version` |
| Visual Studio Build Tools | 2022 | `vswhere -latest` |
| .NET Framework | 4.8+ | Registry check |

### Manual Installation Links

If you prefer to install prerequisites manually:

- **Git**: https://git-scm.com/download/win
- **Python 3.12**: https://www.python.org/downloads/
- **VS Build Tools**: https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022
  - Select ".NET desktop development" workload
- **.NET Framework 4.8**: https://dotnet.microsoft.com/download/dotnet-framework/net48

## Directory Structure

After installation:

```
C:\DwsimMcp\
├── dwsim_interop_services\     # Git repository
│   ├── mcp_service\
│   │   ├── server\             # Python MCP server
│   │   │   ├── .venv\          # Python virtual environment
│   │   │   └── .env            # Environment configuration
│   │   └── dwsim_worker\       # C# DWSIM worker
│   │       ├── dwsim_binaries\ # DWSIM DLLs (downloaded)
│   │       └── DwsimWorker\    # Built worker DLL
│   └── scripts\                # Setup and utility scripts
├── simulations\                # Saved simulation cases
├── start-http.bat              # Start HTTP server
├── start-stdio.bat             # Start stdio server
├── diagnose.bat                # Run diagnostics
└── test-server.bat             # Test server connectivity
```

## Starting the Server

### HTTP Mode (for remote clients)

```powershell
C:\DwsimMcp\start-http.bat
```

Server runs at: `http://localhost:8000/mcp`

### Stdio Mode (for local IDE integration)

```powershell
C:\DwsimMcp\start-stdio.bat
```

## Troubleshooting

### "Access Denied" Errors

If you get permission errors:

1. **Check folder ownership**: Right-click `C:\DwsimMcp` → Properties → Security
2. **Fix permissions** (as Admin):
   ```powershell
   $acl = Get-Acl C:\DwsimMcp
   $rule = New-Object System.Security.AccessControl.FileSystemAccessRule("$env:USERNAME","FullControl","ContainerInherit,ObjectInherit","None","Allow")
   $acl.SetAccessRule($rule)
   Set-Acl C:\DwsimMcp $acl
   ```

### Build Fails

Run diagnostics:

```powershell
cd C:\DwsimMcp\dwsim_interop_services\mcp_service\server
.\.venv\Scripts\dwsim-mcp.exe doctor
```

### Server Won't Start

1. Check if port 8000 is in use: `netstat -an | findstr 8000`
2. Check logs in the terminal output
3. Verify DWSIM binaries exist: `dir C:\DwsimMcp\dwsim_interop_services\mcp_service\dwsim_worker\dwsim_binaries`

### Firewall Issues (Remote Access)

If accessing from another machine:

```powershell
# Run as Administrator
netsh advfirewall firewall add rule name="DWSIM MCP" dir=in action=allow protocol=tcp localport=8000
```

## Reinstalling / Clean Install

To completely reinstall:

```powershell
# 1. Stop any running server (Ctrl+C)

# 2. Delete the repository (keep install folder for permissions)
Remove-Item -Recurse -Force C:\DwsimMcp\dwsim_interop_services

# 3. Re-run setup
cd C:\DwsimMcp
.\setup-user.ps1 -RepoUrl "https://github.com/YOUR_ORG/dwsim_interop_services.git"
```

## Scripts Reference

| Script | Purpose | Admin Required |
|--------|---------|----------------|
| `setup-prerequisites.ps1` | Install Git, Python, VS Build Tools | Yes |
| `setup-user.ps1` | Clone repo, build, configure | No |
| `setup-windows-server.ps1` | Full automated setup | Yes |
| `start-http.bat` | Start HTTP server | No |
| `start-stdio.bat` | Start stdio server | No |
| `diagnose.bat` | Run diagnostics | No |
| `test-server.bat` | Test server connectivity | No |

## Environment Variables

The `.env` file contains:

```ini
DWSIM_WORKER_ASSEMBLY_PATH=C:\DwsimMcp\dwsim_interop_services\mcp_service\dwsim_worker\DwsimWorker\bin\Debug\DwsimWorker.dll
DWSIM_CASE_STORAGE_ROOTS=C:\DwsimMcp\simulations
PYTHONPATH=C:\DwsimMcp\dwsim_interop_services;C:\DwsimMcp\dwsim_interop_services\mcp_service\server
```

You can also set these as system environment variables if needed.
