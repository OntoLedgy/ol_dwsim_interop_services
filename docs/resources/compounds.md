# Compounds Database

DWSIM includes an extensive database of chemical compounds with physical and thermodynamic properties. This guide explains how to work with compounds in simulations.

## Adding Compounds

Before creating streams or running calculations, you must add the required compounds to your session.

**Using add_compound tool:**
```python
add_compound(session_id, "Methane")
add_compound(session_id, "Ethane")
add_compound(session_id, "Water")
```

## Compound Naming

DWSIM uses standardized names. Common variations are supported:

| Standard Name | Also Accepts |
|--------------|--------------|
| Methane | CH4, methane |
| Ethane | C2H6, ethane |
| Propane | C3H8, propane |
| n-Butane | Butane, nButane, n-C4 |
| i-Butane | Isobutane, iC4 |
| Water | H2O, water |
| Nitrogen | N2, nitrogen |
| Carbon dioxide | CO2, CarbonDioxide |
| Hydrogen sulfide | H2S, HydrogenSulfide |

## Common Compound Categories

### Light Hydrocarbons (Natural Gas)

- Methane (CH4)
- Ethane (C2H6)
- Propane (C3H8)
- n-Butane (C4H10)
- i-Butane (C4H10)
- n-Pentane (C5H12)
- i-Pentane (C5H12)
- n-Hexane (C6H14)

### Permanent Gases

- Nitrogen (N2)
- Oxygen (O2)
- Carbon dioxide (CO2)
- Carbon monoxide (CO)
- Hydrogen (H2)
- Hydrogen sulfide (H2S)
- Argon (Ar)
- Helium (He)

### Aromatics

- Benzene (C6H6)
- Toluene (C7H8)
- Ethylbenzene
- o-Xylene, m-Xylene, p-Xylene

### Alcohols

- Methanol (CH3OH)
- Ethanol (C2H5OH)
- 1-Propanol
- 2-Propanol (Isopropanol)
- 1-Butanol

### Common Solvents

- Acetone
- Dichloromethane
- Chloroform
- Ethyl acetate
- Dimethyl sulfoxide (DMSO)

### Refrigerants

- R-134a (1,1,1,2-Tetrafluoroethane)
- R-32 (Difluoromethane)
- R-410A (mixture)
- Ammonia (R-717)

## Compound Properties

Each compound in the database includes:

### Critical Properties
- Critical temperature (Tc)
- Critical pressure (Pc)
- Critical volume (Vc)
- Acentric factor (ω)

### Physical Properties
- Molecular weight (MW)
- Normal boiling point (Tb)
- Normal melting point (Tm)
- Triple point temperature and pressure

### Temperature-Dependent Properties
- Vapor pressure (Antoine equation)
- Heat capacity (polynomial correlation)
- Viscosity
- Thermal conductivity
- Surface tension

## Querying Available Compounds

Use the databank query tool to search for compounds:

```python
# Search by name
query_databank(session_id, name="methanol")

# Search by CAS number
query_databank(session_id, cas="67-56-1")

# Search by formula
query_databank(session_id, formula="C2H6O")
```

## Adding Custom Compounds

For compounds not in the database, you can define custom compounds with:

1. **Joback Method**: Estimate properties from molecular groups
2. **UNIFAC Groups**: Define for activity coefficient estimation
3. **Manual Entry**: Provide all critical properties

```python
add_custom_compound(session_id, {
    "name": "MyCompound",
    "molecular_weight": 100.0,
    "critical_temperature": 500.0,  # K
    "critical_pressure": 4000000.0,  # Pa
    "acentric_factor": 0.25,
    "normal_boiling_point": 350.0  # K
})
```

## Pseudo-Components

For petroleum fractions, use pseudo-components:

```python
add_pseudo_component(session_id, {
    "name": "Light Naphtha",
    "average_boiling_point": 373.0,  # K
    "specific_gravity": 0.7,
    "molecular_weight": 100.0
})
```

## Tips for LLM Agents

1. **Check spelling**: Compound names must match database entries
2. **Use standard names**: "n-Butane" not "normal butane"
3. **Add all compounds first**: Before creating streams
4. **Consider interactions**: Some compound pairs require BIPs
5. **Water is special**: Use appropriate property package for aqueous systems
6. **Check property package compatibility**: Not all packages support all compounds

## Common Issues

### Compound Not Found

If add_compound fails:
1. Check spelling and capitalization
2. Try common synonyms
3. Use query_databank to search
4. Consider adding as custom compound

### Missing Properties

Some compounds may lack certain properties:
- Transport properties (viscosity, thermal conductivity)
- Temperature correlations outside valid range
- Activity coefficient parameters

The simulation will warn if properties are extrapolated or estimated.
