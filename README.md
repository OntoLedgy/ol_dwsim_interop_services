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
- .NET Framework 4.8 (Windows) or .NET Core 6+ (cross-platform)
- DWSIM assemblies (referenced from parent repository or installed separately)

### Installation

```bash
# Clone the repository
git clone <repository-url>
cd dwsim_interop_services

# Install Python dependencies
cd mcp_service/server
pip install -r requirements.txt

# Build .NET worker
cd ../dwsim_worker
dotnet build DwsimWorker.sln
```

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
