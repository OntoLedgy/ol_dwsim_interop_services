# DWSIM MCP Server

Python-based Model Context Protocol (MCP) server that provides a clean, typed interface for LLM agents to interact with DWSIM chemical process simulation engine.

## Architecture

The MCP server acts as a façade layer that:
- Implements MCP protocol using the official Python SDK
- Exposes DWSIM capabilities as composable MCP tools
- Communicates with the .NET worker via JSON-RPC over Named Pipes
- Provides structured logging and observability
- Enforces safety and resource limits

## Installation

### Using uv (recommended)

```bash
cd /mcp_service/server
uv sync
source .venv/bin/activate
```

### DWSIM assemblies

- Ensure DWSIM binaries are available. The repo expects a local copy at [mcp_service/dwsim_worker/dwsim_binaries/x64/Debug](mcp_service/dwsim_worker/dwsim_binaries/x64/Debug) (sync from the sibling `../dwsim/DWSIM/bin/x64/Debug`).
- Override with `DWSIM_PATH` if you want to point to a different DWSIM installation; this is used by the worker assembly loader and pythonnet smoke tests.

## Configuration

Create a `.env` file or set environment variables:

```env
# Worker connection
DWSIM_WORKER_PIPE_NAME=dwsim_worker_pipe
DWSIM_WORKER_STARTUP_TIMEOUT=30

# Resource limits
DWSIM_MAX_SESSIONS=10
DWSIM_SESSION_TIMEOUT=3600
DWSIM_OPERATION_TIMEOUT=300
DWSIM_MEMORY_LIMIT_MB=2048
DWSIM_MEMORY_POLL_INTERVAL_SECONDS=2
DWSIM_MEMORY_RECOVERY_RATIO=0.9

# Logging
LOG_LEVEL=INFO
LOG_FORMAT=json
```

## Usage

### Starting the MCP Server

```bash
# Using Poetry
poetry run dwsim-mcp

# Using Python module
python -m dwsim_mcp_server

# With custom config
python -m dwsim_mcp_server --config config.yaml
```

### Using as a Library

```python
from dwsim_mcp_server import MCPServer

async def main():
    server = MCPServer()
    await server.start()

if __name__ == "__main__":
    import asyncio
    asyncio.run(main())
```

## Development

### Running Tests

```bash
# All tests
pytest

# With coverage
pytest --cov=dwsim_mcp_server --cov-report=html

# Specific test file
pytest tests/unit/test_session.py

# Fuller run (includes smoke; requires DwsimWorker.dll built in Debug)
pytest tests/smoke tests/unit tests/integration
```

### Code Quality

```bash
# Format code
black dwsim_mcp_server tests

# Lint
ruff check dwsim_mcp_server tests

# Type checking
mypy dwsim_mcp_server
```

## Project Structure

```
server/
├── dwsim_mcp_server/        # Main Python package
│   ├── server.py            # MCP server bootstrap
│   ├── tools/               # MCP tool implementations
│   ├── resources/           # MCP resource providers
│   ├── ipc/                 # IPC client for worker communication
│   ├── converters/          # Model converters
│   ├── config/              # Configuration management
│   └── observability/       # Logging, tracing, metrics
├── tests/                   # Test suite
├── pyproject.toml           # Poetry configuration
└── requirements.txt         # Pip dependencies
```

## Available MCP Tools

### Session Management
- `create_session` - Create a new DWSIM simulation session
- `close_session` - Close and cleanup a session
- `save_case` - Save session to DWSIM file
- `load_case` - Load existing DWSIM file

### Flowsheet Building
- `add_compound` - Add chemical compound to simulation
- `set_property_package` - Configure thermodynamic property package
- `add_stream` - Add material or energy stream
- `add_unit` - Add unit operation (mixer, separator, reactor, etc.)
- `connect` - Connect streams to unit operations

### Simulation
- `run` - Execute flowsheet simulation
- `get_status` - Check simulation status
- `get_results` - Retrieve simulation results

### Simulation Tools Details

#### run
Execute a simulation for the session and return convergence details and stream properties.

Input:
```json
{
  "session_id": "session-1234",
  "timeout_seconds": 120
}
```

Output:
```json
{
  "status": "converged",
  "convergence_state": "Converged",
  "elapsed_ms": 1250.0,
  "stream_results": [
    {
      "id": "stream-001",
      "name": "Vapor Outlet",
      "temperature_k": 350.0,
      "pressure_pa": 101325.0,
      "total_molar_flow_mol_per_s": 10.5,
      "phases": []
    }
  ],
  "messages": ["Calculation converged successfully."],
  "mass_balance_valid": true,
  "mass_balance_error_percent": 0.4
}
```

#### get_status
Retrieve the latest simulation status for a session.

Input:
```json
{
  "session_id": "session-1234"
}
```

Output:
```json
{
  "status": "running",
  "is_running": true,
  "last_run_timestamp": "2024-12-19T20:30:00Z",
  "elapsed_ms": 450.0
}
```

#### get_results
Retrieve cached results from the most recent run. Use `object_id` to request a single stream.

Input:
```json
{
  "session_id": "session-1234",
  "object_id": "stream-001"
}
```

Output:
```json
{
  "status": "converged",
  "convergence_state": "Converged",
  "elapsed_ms": 1250.0,
  "stream_results": [
    {
      "id": "stream-001",
      "name": "Vapor Outlet",
      "temperature_k": 350.0,
      "pressure_pa": 101325.0,
      "total_molar_flow_mol_per_s": 10.5,
      "phases": []
    }
  ],
  "messages": [],
  "mass_balance_valid": true,
  "mass_balance_error_percent": 0.4
}
```

### Simulation Workflow Example

```json
{"tool": "create_session", "arguments": {"name": "Separator Run"}}
{"tool": "add_compound", "arguments": {"session_id": "session-1234", "compound_name": "Water"}}
{"tool": "set_property_package", "arguments": {"session_id": "session-1234", "package_name": "peng-robinson", "options": {}}}
{"tool": "add_stream", "arguments": {"session_id": "session-1234", "name": "feed", "temperature": 298.15, "pressure": 101325.0, "molar_flow": 10.0, "composition": {"Water": 1.0}}}
{"tool": "add_unit", "arguments": {"session_id": "session-1234", "unit_type": "separator", "name": "sep-01", "parameters": {}}}
{"tool": "connect", "arguments": {"session_id": "session-1234", "source_id": "stream-001", "target_id": "unit-001", "port_name": "Inlet"}}
{"tool": "run", "arguments": {"session_id": "session-1234", "timeout_seconds": 120}}
{"tool": "get_status", "arguments": {"session_id": "session-1234"}}
{"tool": "get_results", "arguments": {"session_id": "session-1234"}}
```

### Simulation Error Codes

Simulation tools return structured error payloads when operations fail:
- `CONVERGENCE_FAILURE` - Solver did not converge
- `SIMULATION_TIMEOUT` - Calculation exceeded timeout
- `INVALID_FLOWSHEET_STATE` - Missing or invalid flowsheet topology
- `NO_RESULTS_AVAILABLE` - Results requested before a successful run
- `RESOURCE_LIMIT_EXCEEDED` - Memory or resource cap exceeded
- `SESSION_EXPIRED` - Session lifetime exceeded

### Thermodynamics
- `flash_tp` - Flash calculation at temperature and pressure
- `flash_ph` - Flash at pressure and enthalpy
- `flash_ps` - Flash at pressure and entropy

### Analysis
- `sensitivity_analysis` - Perform sensitivity study
- `optimization` - Optimize process variables

## Troubleshooting

### Worker Connection Issues

If the server cannot connect to the .NET worker:

1. Ensure the worker is running: `DwsimWorker.exe`
2. Check the pipe name matches in both server and worker configs
3. Verify Named Pipes are available (Windows) or use TCP fallback

### Import Errors

```bash
# Ensure all dependencies are installed
pip install -r requirements.txt

# Verify PYTHONPATH includes project root
export PYTHONPATH="${PYTHONPATH}:$(pwd)"
```

### Performance Issues

- Adjust `DWSIM_OPERATION_TIMEOUT` for complex simulations
- Increase `DWSIM_MAX_SESSIONS` if running concurrent simulations
- Enable worker process pooling in config

### Simulation Troubleshooting

- If `run` returns `INVALID_FLOWSHEET_STATE`, ensure at least one unit operation exists and streams are connected.
- If `get_results` returns `NO_RESULTS_AVAILABLE`, run a simulation first and verify it converged.
- If `SIMULATION_TIMEOUT` occurs, increase `timeout_seconds` for the `run` tool or adjust `DWSIM_OPERATION_TIMEOUT`.

## License

GPLv3 - See LICENSE file for details.
