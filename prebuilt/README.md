<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

# DWSIM MCP Server - Pre-built Distribution

This folder contains pre-built DLLs for beta testers who don't want to build the C# interop layer.

## DWSIM Version Requirements

This MCP server requires **DWSIM v9.0.5 or later** which includes the headless `UpdateInterface` fix.

- **Pre-built binaries:** https://github.com/OntoLedgy/dwsim/releases/tag/v9.0.5-mcp
- **Upstream DWSIM:** https://github.com/DanWBR/dwsim (v9.0.5+)

## Quick Start for Beta Testers

### Option 1: Automatic Download (Recommended)

```powershell
# From repository root - downloads patched DWSIM binaries automatically
.\prebuilt\setup.ps1
```

This will:
1. Copy DwsimWorker DLLs to the correct location
2. Download patched DWSIM binaries (~280MB) from OntoLedgy/dwsim releases
3. Extract and configure everything
4. Install Python dependencies
5. Print your MCP configuration

### Option 2: Use Local Patched DWSIM Build

If you've built the patched DWSIM yourself:

```powershell
.\prebuilt\setup.ps1 -DwsimPath "C:\path\to\your\patched\dwsim\bin\x64\Debug"
```

### Option 3: Manual Setup

1. **Copy DwsimWorker DLLs:**
   ```powershell
   mkdir mcp_service\dwsim_worker\DwsimWorker\bin\Debug -Force
   Copy-Item prebuilt\DwsimWorker\* mcp_service\dwsim_worker\DwsimWorker\bin\Debug\ -Force
   ```

2. **Download DWSIM binaries:**
   - Download from: https://github.com/OntoLedgy/dwsim/releases/download/v9.0.5-mcp/dwsim_binaries.zip
   - Extract to: `mcp_service/dwsim_worker/dwsim_binaries/x64/Debug/`

3. **Install Python dependencies:**
   ```powershell
   cd mcp_service\server
   uv sync
   ```

## Folder Structure

```
prebuilt/
├── README.md           # This file
├── DwsimWorker/        # Pre-built C# interop DLLs
│   ├── DwsimWorker.dll
│   ├── Newtonsoft.Json.dll
│   ├── Serilog.dll
│   └── ...
└── setup.ps1           # Setup script (Windows)
```

## Manual Setup

If you prefer manual setup:

1. **Create target directories:**
   ```powershell
   mkdir mcp_service\dwsim_worker\DwsimWorker\bin\Debug -Force
   mkdir mcp_service\dwsim_worker\dwsim_binaries\x64\Debug -Force
   ```

2. **Copy DwsimWorker DLLs:**
   ```powershell
   Copy-Item prebuilt\DwsimWorker\* mcp_service\dwsim_worker\DwsimWorker\bin\Debug\ -Force
   ```

3. **Copy or link DWSIM binaries:**
   ```powershell
   # Option A: Copy from DWSIM installation
   $dwsimPath = "$env:LOCALAPPDATA\DWSIM"
   Copy-Item "$dwsimPath\*" mcp_service\dwsim_worker\dwsim_binaries\x64\Debug\ -Recurse -Force
   
   # Option B: Create symbolic link
   cmd /c mklink /D mcp_service\dwsim_worker\dwsim_binaries\x64\Debug "$dwsimPath"
   ```

## Building from Source

If you want to build the C# layer yourself:

```powershell
cd mcp_service\dwsim_worker
.\build.bat
```

Requirements:
- Visual Studio 2019+ or MSBuild
- .NET Framework 4.8 SDK

## Troubleshooting

### DLL not found errors

Ensure all DLLs are in the correct locations:
- DwsimWorker.dll → `mcp_service/dwsim_worker/DwsimWorker/bin/Debug/`
- DWSIM.*.dll → `mcp_service/dwsim_worker/dwsim_binaries/x64/Debug/`

### Python can't load .NET assembly

1. Check PYTHONPATH includes the repo root
2. Verify .NET Framework 4.8 is installed
3. Try running as Administrator

### Server starts but tools fail

Check the DWSIM binaries path in the Python configuration:
```python
# In mcp_service/dwsim_worker/clr_loader.py
DWSIM_PATH = "dwsim_binaries/x64/Debug"
```

## License

DwsimWorker is provided under the same license as this repository.
DWSIM binaries have their own license - see https://dwsim.org/
