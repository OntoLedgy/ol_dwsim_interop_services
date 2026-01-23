# DWSIM Interoperability Services

A Model Context Protocol (MCP) server that exposes DWSIM's chemical process simulation engine to Large Language Model (LLM) agents through safe, composable tools and resources.

## Overview

The DWSIM MCP Server bridges the gap between AI agents and professional chemical engineering simulation, enabling natural language-driven process design, analysis, and optimization. It provides a standardized MCP interface over DWSIM's .NET Framework engine with CAPE-OPEN interoperability.

## Architecture

**Polyglot Architecture:**
- **Python MCP Server** (`mcp_service/server/`): MCP façade using official MCP SDK
- **.NET Framework Worker** (`mcp_service/dwsim_worker/`): DWSIM engine hosting and execution
- **Shared Models** (`models/`): CAPE-OPEN and DWSIM domain models for interoperability
- **IPC Communication**: JSON-RPC 2.0 over Named Pipes or pythonnet for direct interop

## Project Structure

```
dwsim_interop_services/
├── models/                      # Shared object models (CAPE-OPEN, DWSIM, DTOs)
├── mcp_service/
│   ├── server/                  # Python MCP server
│   │   └── dwsim_mcp_server/    # Main Python package
│   └── dwsim_worker/            # .NET Framework engine worker
│       └── DwsimWorker/         # C# console application
├── integration-tests/           # End-to-end tests
├── docs/                        # Documentation
└── deployments/                 # Deployment configurations
```

## Key Features

- **MCP-Compliant Interface**: Full implementation of Model Context Protocol
- **CAPE-OPEN Integration**: Industry-standard interfaces for multi-simulator interoperability
- **Session-Based Architecture**: Multiple concurrent sessions with isolation
- **Safety & Resource Limits**: Sandboxed execution with timeouts and quotas
- **Comprehensive Tool Set**: Session management, flowsheet building, thermodynamics, simulation, analysis
- **Observability**: Structured logging, OpenTelemetry tracing, metrics

## Getting Started

### Prerequisites

- Python 3.10+
- uv (Python package manager)
- .NET Framework 4.8 Developer Pack/Targeting Pack (Windows) or .NET 6+ SDK (cross-platform)
- DWSIM assemblies (referenced from parent repository or installed separately)

### Installation

#### Install uv

**Windows (PowerShell)**

```powershell
irm https://astral.sh/uv/install.ps1 | iex
```

If this fails due to execution policy or PowerShell closing:

```powershell
# Allow local scripts for current user (permanent and safe)
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned

# Run the installer without piping to iex
$uv = "$env:TEMP\uv-install.ps1"
irm https://astral.sh/uv/install.ps1 -OutFile $uv
Unblock-File $uv
& $uv
```

If your profile is breaking the session, launch without it:

```powershell
powershell -NoProfile
```

**macOS / Linux (bash)**

```bash
curl -LsSf https://astral.sh/uv/install.sh | sh
```

Verify:

```bash
uv --version
```

#### Install dependencies and build (Windows / PowerShell)

```powershell
# Clone the repository
git clone https://github.com/OntoLedgy/dwsim_interop_services.git
cd dwsim_interop_services

# Install Python dependencies
cd mcp_service\server
uv sync
.\.venv\Scripts\Activate.ps1

# Build .NET worker
cd ..\dwsim_worker
build.bat
```

For a step-by-step guide (including **prebuilt binaries**), see `docs/resources/getting-started.md`.

**Notes on .NET (Windows)**
- This project targets **.NET Framework 4.8** for the DWSIM worker. Install the **.NET Framework 4.8 Developer Pack** (includes the Targeting Pack) and **Visual Studio Build Tools** with the ".NET desktop development" workload.
  - .NET Framework 4.8 download page (Developer Pack + Runtime): https://dotnet.microsoft.com/download/dotnet-framework/net48
  - .NET Framework installation guide (Microsoft Learn): https://learn.microsoft.com/dotnet/framework/install/guide-for-developers
  - Visual Studio Build Tools download: https://visualstudio.microsoft.com/downloads/ (scroll to "Build Tools for Visual Studio")
- The `build.bat` script sets the correct framework/tooling and builds `DwsimWorker.sln`. Use it instead of `dotnet build` on Windows to avoid framework/SDK mismatch.

**Notes on macOS/Linux**
- The .NET Framework worker is Windows-only. On macOS/Linux you can build/run the Python server, but the worker requires Windows with .NET Framework.

### DWSIM binaries setup

- Build DWSIM in the sibling `../dwsim` repository (x64 Debug/Release).
- Copy the built binaries into this repo (keeps tests and pythonnet loader self-contained):

```powershell
cd c:/Users/Mesbah.Khan/s/OntoLedgy/dwsim_interop_services
$src = "..\dwsim\DWSIM\bin\x64\Debug"
$dest = "mcp_service\dwsim_worker\dwsim_binaries\x64\Debug"
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item "$src\*.dll" $dest -Force
Copy-Item "$src\data" "$dest\data" -Recurse -Force
Copy-Item "$src\ThermoCS" "$dest\ThermoCS" -Recurse -Force
```

- Default paths in the worker/test configs now point to [mcp_service/dwsim_worker/dwsim_binaries/x64/Debug](mcp_service/dwsim_worker/dwsim_binaries/x64/Debug). For machine-specific installs, copy [mcp_service/dwsim_worker/dwsim.config.json.sample](mcp_service/dwsim_worker/dwsim.config.json.sample) to `mcp_service/dwsim_worker/dwsim.config.json` and set `dwsim_path` to your DWSIM build folder (gitignored).

### Quick Start

```bash
# Start the MCP server
cd mcp_service/server
python -m dwsim_mcp_server

# In another terminal, start the worker
cd mcp_service/dwsim_worker/DwsimWorker/bin/Debug
./DwsimWorker.exe
```

## Connecting to AI Assistants

The DWSIM MCP Server can be connected to multiple AI platforms:

| Platform | Configuration File | Guide |
|----------|-------------------|-------|
| **VS Code Copilot** | `settings.json` or `mcp.json` | [Getting Started](docs/resources/getting-started.md) |
| **Claude Desktop** | `claude_desktop_config.json` | [Getting Started](docs/resources/getting-started.md) |
| **OpenAI Codex** | `~/.codex/config.json` | [Getting Started](docs/resources/getting-started.md) |

### Beta Testers - Quick Setup

For beta testers who don't want to build the C# layer:

```powershell
# From repository root
.\prebuilt\setup.ps1
```

This will:
1. Set up the DwsimWorker DLLs
2. Link your DWSIM installation
3. Install Python dependencies
4. Print your MCP configuration

See [prebuilt/README.md](prebuilt/README.md) for details.

## Documentation

- [API Documentation](docs/api/) - MCP tool reference and API specifications
- [User Guides](docs/guides/) - Quickstart, configuration, troubleshooting
- [Architecture](docs/architecture/) - System design, security, observability

## Development

### Running Tests

```bash
# Python tests
cd mcp_service/server
pytest tests/

# Fuller run (includes smoke; requires DwsimWorker.dll built in Debug)
pytest tests/smoke tests/unit tests/integration

# C# tests
cd mcp_service/dwsim_worker
dotnet test
```

### Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines and code standards.

## License

This project is licensed under GPLv3 - see [LICENSE](LICENSE) file for details.

DWSIM is licensed under GPLv3. See the [DWSIM repository](https://github.com/DanWBR/dwsim) for more information.

## Acknowledgments

- DWSIM open-source simulation engine
- CAPE-OPEN standards organization
- Model Context Protocol by Anthropic
