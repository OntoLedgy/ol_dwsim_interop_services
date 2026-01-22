# DWSIM MCP Server Documentation

This documentation provides reference materials for using the DWSIM MCP Server to build and run chemical process simulations through LLM agents.

## Available Topics

| Topic | Description |
|-------|-------------|
| [unit-operations](resource://docs/unit-operations) | Guide to DWSIM unit operations and their parameters |
| [property-packages](resource://docs/property-packages) | Thermodynamic property packages and their applicability |
| [compounds](resource://docs/compounds) | Compound database and adding chemicals to simulations |

## Quick Start

1. **Create a session**: Use `create_session` tool to start a new simulation workspace
2. **Add compounds**: Use `add_compound` to add chemicals (e.g., "Methane", "Water")
3. **Set property package**: Use `set_property_package` (e.g., "Peng-Robinson")
4. **Build flowsheet**: Add streams and unit operations, connect them
5. **Run simulation**: Use `run` tool to execute calculations
6. **Get results**: Use `get_results` or access result resources

## Common Workflows

### Three-Phase Separator

```
1. add_compound: Methane, Ethane, Propane, Water
2. set_property_package: Peng-Robinson
3. add_stream: Feed (T=300K, P=5bar, composition)
4. add_unit: ThreePhaseSeparator
5. connect: Feed -> Separator
6. run
7. get_results
```

### Flash Calculation

```
1. flash_tp: Temperature-Pressure flash for phase equilibrium
   - Specify compounds, composition, T, P
   - Returns vapor/liquid split and phase properties
```

## Units

All properties use SI units:
- Temperature: K (Kelvin)
- Pressure: Pa (Pascal)
- Flow: mol/s (molar) or kg/s (mass)
- Composition: mole fraction (0-1)

## Resources

Access detailed results and documentation via MCP resources:
- `resource://docs/{topic}` - Documentation
- `resource://cases/{name}` - Sample cases
- `resource://session/{id}/results` - Simulation results
