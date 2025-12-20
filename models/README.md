# Shared Models

This directory contains all shared object models that facilitate interoperability between the Python MCP server, C# engine worker, LLM agents, and external systems.

## Organization

### CAPE-OPEN Models (`cape_open/`)

Standard CAPE-OPEN interface implementations for multi-simulator interoperability:

- `material_stream.py` - ICapeThermoMaterialObject
- `thermo_property_package.py` - ICapeThermoPropertyPackage
- `unit_operation.py` - ICapeUnit
- `compound.py` - ICapeThermoCompounds
- `phase.py` - ICapeThermoPhases
- `reaction.py` - ICapeReaction

### DWSIM Application Models (`dwsim/`)

DWSIM-specific models not covered by CAPE-OPEN:

- `flowsheet.py` - Complete flowsheet representation
- `session.py` - Simulation session state
- `simulation_result.py` - Simulation output data
- `property_package_config.py` - Property package configuration
- `solver_config.py` - Solver settings

### Request DTOs (`requests/`)

Pydantic models for MCP tool inputs:

- `create_session_request.py`
- `add_stream_request.py`
- `add_unit_request.py`
- `run_simulation_request.py`
- `flash_request.py`

### Response DTOs (`responses/`)

Pydantic models for MCP tool outputs:

- `create_session_response.py`
- `simulation_result_response.py`
- `stream_properties_response.py`
- `flash_result_response.py`

### Error Models (`errors/`)

Structured error types:

- `session_error.py` - Session management errors
- `simulation_error.py` - Simulation execution errors
- `validation_error.py` - Input validation errors

## Design Principles

### One File Per Class

Each model class is in its own dedicated file for:
- **Improved readability**: Easy to locate and understand individual models
- **Better git history**: Changes to one model don't affect others
- **Enhanced discoverability**: LLMs can explore models independently
- **Simplified testing**: One model = one test file

### Pydantic-Based

All models use Pydantic for:
- **Type safety**: Automatic validation of field types
- **JSON serialization**: Easy conversion to/from JSON for IPC
- **Schema generation**: JSON Schema for MCP tool definitions
- **Documentation**: Field descriptions embedded in models

### CAPE-OPEN Alignment

Models in `cape_open/` follow CAPE-OPEN specifications:
- Property names match CAPE-OPEN conventions
- Units follow CAPE-OPEN SI defaults
- Interfaces map directly to CAPE-OPEN COM interfaces

## Usage Examples

### Python (MCP Server)

```python
from models.cape_open.material_stream import MaterialStream
from models.requests.create_session_request import CreateSessionRequest

# Create a stream
stream = MaterialStream(
    name="Feed",
    temperature=298.15,
    pressure=101325.0,
    composition={"water": 0.5, "ethanol": 0.5}
)

# Create a session request
request = CreateSessionRequest(
    name="My Simulation",
    timeout=3600
)

# Serialize to JSON for IPC
json_data = stream.model_dump_json()
```

### C# (Engine Worker)

```csharp
using Newtonsoft.Json;
using Models.CapeOpen;

// Deserialize from JSON
var stream = JsonConvert.DeserializeObject<MaterialStream>(jsonData);

// Convert to DWSIM objects
var dwsimStream = ConvertToDwsimStream(stream);
```

## Versioning

Models are versioned alongside the package:
- Breaking changes: Increment major version
- New models: Increment minor version
- Bug fixes: Increment patch version

See [CHANGELOG.md](../CHANGELOG.md) for version history.

## Testing

Model tests are located in `../tests/models/`:
- Validation tests
- Serialization roundtrip tests
- CAPE-OPEN compliance tests
- Example data fixtures

Run tests:
```bash
pytest tests/models/
```
