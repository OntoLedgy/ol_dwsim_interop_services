<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

# Installation

This guide covers Windows and development setup for ol-dwsim-mcp-server.

> **Note:** DWSIM requires Windows with Desktop Experience (full GUI). See [Windows Deployment Guide](windows-deployment.md) for detailed platform requirements and server setup.

## Prerequisites

- Python 3.11+ (Python 3.11 or 3.12 recommended)
- .NET Framework 4.8 Developer Pack/Targeting Pack (Windows)
- Windows 10/11 or Windows Server with Desktop Experience
- DWSIM binaries available locally (downloaded by `dwsim-mcp setup --download` or from an existing install)
- NSSM (optional, for running as a Windows service)

## Quick Start

> Note: If you are running from a source checkout, set `PYTHONPATH` to the repo root so the shared `models` package is importable.

CLI commands available: `run`, `version`, `setup`, `doctor`, `init`, `service`.

### 1) Install the package

```powershell
python -m pip install ol-dwsim-mcp-server
```

Expected output (excerpt):

```text
Requirement already satisfied: ol-dwsim-mcp-server in ... (0.1.0)
```

### 2) Download DWSIM binaries and create config

```powershell
$env:PYTHONPATH = "C:\path\to\dwsim_interop_services"
dwsim-mcp setup --download
```

Expected output:

```text
Downloading DWSIM binaries ------------------- 297.5/297.5 MB 14.2 MB/s 0:00:00
DWSIM binaries downloaded.
DWSIM configuration created.
Config:
C:\path\to\dwsim_interop_services\mcp_service\dwsim_worker\dwsim.config.json
```

### 3) Run diagnostics

```powershell
$env:PYTHONPATH = "C:\path\to\dwsim_interop_services"
dwsim-mcp doctor
```

Expected output (excerpt):

```text
                               dwsim-mcp doctor
+-----------------------------------------------------------------------------+
| Status | Check              | Details                                       |
|--------+--------------------+-----------------------------------------------|
| PASS   | Python version     | Python 3.12.9 detected.                       |
| PASS   | .NET Framework 4.8 | .NET Framework release 533509 detected.       |
| PASS   | DWSIM assemblies   | DWSIM assemblies present at ...               |
+-----------------------------------------------------------------------------+
6 passed, 0 warnings, 0 failed
```

## Manual Setup (existing DWSIM installation)

If you already have a DWSIM install/build, point the configuration at the existing binaries.

```powershell
$env:PYTHONPATH = "C:\path\to\dwsim_interop_services"
dwsim-mcp setup --dwsim-path "C:\Program Files\DWSIM\bin"
```

Expected output:

```text
DWSIM configuration created.
Config:
C:\path\to\dwsim_interop_services\mcp_service\dwsim_worker\dwsim.config.json
```

### Windows service (NSSM)

The CLI can manage a Windows service using NSSM. Service actions require an elevated terminal.

```powershell
dwsim-mcp service status
```

Expected output (when not elevated):

```text
Administrative privileges are required for service actions.
Re-run this command in an elevated terminal (Run as Administrator).
```

## Development Setup

```powershell
git clone https://github.com/OntoLedgy/dwsim_interop_services.git
```

Expected output:

```text
Cloning into 'dwsim_interop_services'...
```

```powershell
cd dwsim_interop_services\mcp_service\server
poetry install
```

Expected output (excerpt):

```text
Updating dependencies
Resolving dependencies...
Writing lock file
Installing the current project: ol-dwsim-mcp-server (0.1.0)
```

```powershell
cd ..\dwsim_worker
.\build.bat
```

Expected output (excerpt):

```text
Step 2: Building solution...
MSBuild version 18.0.5+e22287bf1 for .NET Framework
Build succeeded.
BUILD SUCCEEDED
```

## Verification

Run the diagnostics tool after setup to confirm your environment:

```powershell
$env:PYTHONPATH = "C:\path\to\dwsim_interop_services"
dwsim-mcp doctor
```

Expected output (full example):

```text
                               dwsim-mcp doctor
+-----------------------------------------------------------------------------+
| Status | Check              | Details                                       |
|--------+--------------------+-----------------------------------------------|
| PASS   | Python version     | Python 3.12.9 detected.                       |
| PASS   | .NET Framework 4.8 | .NET Framework release 533509 detected.       |
| PASS   | pythonnet / clr    | pythonnet unknown and clr are available.      |
| PASS   | DwsimWorker.dll    | Found DwsimWorker.dll at                      |
|        |                    | C:\path\to\dwsim_interop_services\mcp_service\dw |
|        |                    | sim_worker\DwsimWorker\bin\Debug\DwsimWorker. |
|        |                    | dll.                                          |
| PASS   | DWSIM config       | Config OK.                                    |
|        |                    | dwsim_path=C:\path\to\dwsim_interop_services\mcp |
|        |                    | _service\dwsim_worker\dwsim_binaries\x64\Debu |
|        |                    | g                                             |
| PASS   | DWSIM assemblies   | DWSIM assemblies present at                   |
|        |                    | C:\path\to\dwsim_interop_services\mcp_service\dw |
|        |                    | sim_worker\dwsim_binaries\x64\Debug.          |
+-----------------------------------------------------------------------------+
6 passed, 0 warnings, 0 failed
```

## Troubleshooting

- `ModuleNotFoundError: No module named 'models'`: set `PYTHONPATH` to the repo root before running `dwsim-mcp`.
- `dwsim-mcp doctor` reports missing DWSIM assemblies: re-run `dwsim-mcp setup --download` or `dwsim-mcp setup --dwsim-path ...`.
- Service commands fail with admin error: open PowerShell as Administrator before running `dwsim-mcp service ...`.
- .NET Framework 4.8 missing: install the .NET Framework 4.8 Developer Pack and re-run `dwsim-mcp doctor`.

## Related documentation

- [Getting Started](../resources/getting-started.md)
- [Prebuilt setup](../../prebuilt/README.md)
- [API docs](../api/)
- [Observability](../observability.md)
