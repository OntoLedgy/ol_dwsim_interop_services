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

### Using Poetry (recommended)

```bash
poetry install
```

### Using pip

```bash
pip install -r requirements.txt
```

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

## License

GPLv3 - See LICENSE file for details.
