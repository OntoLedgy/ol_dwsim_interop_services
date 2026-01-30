# Windows Deployment Guide

This guide covers deploying the DWSIM MCP Server on Windows systems.

## Platform Requirements

DWSIM requires Windows with Desktop Experience (full GUI) due to its dependency on Eto.Forms/WinForms. **Windows Server Core will NOT work.**

### Why Windows Desktop is Required

DWSIM uses **Eto.Forms** with **WinForms** as the platform backend:

```csharp
// From FlowsheetContext.cs
var platform = getPlatformMethod.Invoke(null, new object[] { "WinForms" });
```

**Windows Forms requires:**
- GDI+ (graphics subsystem)
- User32.dll (window management)
- Full Desktop Window Manager

**Windows Server Core lacks:**
- No GUI shell
- No Windows Forms runtime
- No GDI+ support for rendering

### Supported Windows Editions

| Edition | Works? | Notes |
|---------|--------|-------|
| Windows 11 Pro/Enterprise | Yes | Full desktop support |
| Windows 10 Pro/Enterprise | Yes | Full desktop support |
| Windows Server 2022 with Desktop Experience | Yes | Includes GUI components |
| Windows Server 2022 Core | **NO** | Missing GUI subsystem |
| Windows Server 2019 with Desktop Experience | Yes | Includes GUI components |
| Windows Server 2019 Core | **NO** | Missing GUI subsystem |

## Prerequisites

1. **Windows OS**: Windows 10/11 Pro or Windows Server with Desktop Experience
2. **.NET Framework 4.8**: Pre-installed on Windows 10 1903+ and Server 2022
3. **Python 3.11+**: Download from python.org or use winget
4. **DWSIM 9.x**: Download from dwsim.org or build from source

## Installation Steps

### Step 1: Install Python

```powershell
# Option A: Using winget (Windows 10/11)
winget install Python.Python.3.12

# Option B: Manual download
# Download from https://www.python.org/downloads/
# Enable "Add to PATH" during installation
```

### Step 2: Install DWSIM

```powershell
# Download DWSIM installer from https://dwsim.org/index.php/download/
# Run installer with default options
# Default path: C:\Users\<user>\AppData\Local\DWSIM9\
```

### Step 3: Clone Repository

```powershell
git clone <repository-url>
cd dwsim_interop_services
```

### Step 4: Build DwsimWorker

```powershell
cd mcp_service\dwsim_worker

# Create config file from sample
copy DwsimWorker\dwsim.config.sample.json DwsimWorker\dwsim.config.json

# Edit config to set DWSIM path (use your actual DWSIM installation)
# Example: "dwsimPath": "C:\\Users\\<user>\\AppData\\Local\\DWSIM9"

# Build
.\build.bat
```

### Step 5: Install Python Dependencies

```powershell
cd mcp_service\server
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -e .
```

### Step 6: Configure MCP Server

```powershell
# Create .env file from template
copy .env.example .env

# Edit .env with your paths:
# DWSIM_WORKER_ASSEMBLY_PATH=<path-to-DwsimWorker.dll>
# DWSIM_CASE_STORAGE_ROOTS=<path-to-simulation-cases>
```

### Step 7: Run the Server

```powershell
# Activate venv if not active
.\.venv\Scripts\Activate.ps1

# Run MCP server (stdio mode for Claude Desktop)
python -m dwsim_mcp_server

# Or run HTTP mode for network access
python -m dwsim_mcp_server --transport streamable-http --port 8000
```

## Claude Desktop Integration

Add to Claude Desktop config (`%APPDATA%\Claude\claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "dwsim": {
      "command": "python",
      "args": ["-m", "dwsim_mcp_server"],
      "cwd": "D:\\path\\to\\dwsim_interop_services\\mcp_service\\server",
      "env": {
        "DWSIM_WORKER_ASSEMBLY_PATH": "D:\\path\\to\\DwsimWorker.dll"
      }
    }
  }
}
```

## Windows Server Setup (with Desktop Experience)

### For Azure/Cloud VMs

When creating a Windows Server VM:

1. **Azure**: Select "Windows Server 2022 Datacenter: Azure Edition" (NOT Core)
2. **AWS**: Select Windows Server 2022 Base (Full version, not Core)
3. **GCP**: Select "Windows Server 2022 Datacenter"

### Post-VM Setup

```powershell
# 1. Verify .NET Framework 4.8 is installed
(Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full").Release
# Should be 528040 or higher for .NET 4.8

# 2. Install Python
Invoke-WebRequest -Uri "https://www.python.org/ftp/python/3.12.0/python-3.12.0-amd64.exe" -OutFile python-installer.exe
.\python-installer.exe /quiet InstallAllUsers=1 PrependPath=1

# 3. Install Git
winget install Git.Git

# 4. Install DWSIM (download manually or script)
# DWSIM installer available at https://dwsim.org
```

## Running as a Windows Service

For production, run the MCP server as a Windows Service using NSSM:

```powershell
# Install NSSM
winget install NSSM.NSSM

# Create service
nssm install DwsimMcpServer "C:\Python312\python.exe"
nssm set DwsimMcpServer AppParameters "-m dwsim_mcp_server --transport streamable-http --port 8000"
nssm set DwsimMcpServer AppDirectory "D:\path\to\mcp_service\server"
nssm set DwsimMcpServer AppEnvironmentExtra "DWSIM_WORKER_ASSEMBLY_PATH=D:\path\to\DwsimWorker.dll"

# Start service
nssm start DwsimMcpServer
```

Alternatively, use the built-in CLI service management:

```powershell
# Run as Administrator
dwsim-mcp service install
dwsim-mcp service start
dwsim-mcp service status
```

## Verification

After installation, verify everything is working:

```powershell
# Run diagnostics
dwsim-mcp doctor
```

Expected output:

```text
                               dwsim-mcp doctor
+-----------------------------------------------------------------------------+
| Status | Check              | Details                                       |
|--------+--------------------+-----------------------------------------------|
| PASS   | Python version     | Python 3.12.x detected.                       |
| PASS   | .NET Framework 4.8 | .NET Framework release 533509 detected.       |
| PASS   | pythonnet / clr    | pythonnet and clr are available.              |
| PASS   | DwsimWorker.dll    | Found DwsimWorker.dll at ...                  |
| PASS   | DWSIM config       | Config OK.                                    |
| PASS   | DWSIM assemblies   | DWSIM assemblies present.                     |
+-----------------------------------------------------------------------------+
6 passed, 0 warnings, 0 failed
```

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| `ModuleNotFoundError: No module named 'models'` | Set `PYTHONPATH` to the repo root |
| Missing DWSIM assemblies | Re-run `dwsim-mcp setup --download` or check config path |
| .NET Framework 4.8 not detected | Install .NET Framework 4.8 Developer Pack |
| Service commands fail | Run PowerShell as Administrator |
| pythonnet import errors | Ensure Python architecture matches (x64) |

### Windows Server Core Detection

If you accidentally deployed to Server Core, you'll see errors like:

```text
System.TypeInitializationException: The type initializer for 'System.Windows.Forms.XplatUI' threw an exception.
---> System.ArgumentNullException: Value cannot be null. (Parameter 'method')
```

The solution is to use Windows Server with Desktop Experience instead.

## Related Documentation

- [Installation](installation.md) - Quick start and development setup
- [Configuration Reference](configuration.md) - All environment variables and config files
- [Getting Started](../resources/getting-started.md) - First steps with the MCP server
