<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

This file is part of the OntoLedgy Thermodynamics Architecture and is
dual-licensed:

  1. Open source under the GNU Affero General Public License v3.0 or
     later (AGPL-3.0-or-later). See the LICENSE file in the repository
     root for the full licence text and NOTICE for attribution.
  2. Commercial under a separate proprietary licence offered by
     OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

# Property Packages

Property packages (also called thermodynamic models or equations of state) define how DWSIM calculates physical and thermodynamic properties. Choosing the right property package is critical for accurate simulation results.

## Overview

A property package provides methods to calculate:
- Vapor-liquid equilibrium (VLE)
- Phase properties (density, enthalpy, entropy, etc.)
- Transport properties (viscosity, thermal conductivity)
- Fugacity coefficients and activity coefficients

## Equations of State

### Peng-Robinson (PR)

The most widely used cubic equation of state for hydrocarbon systems.

**Best for:**
- Natural gas processing
- Refinery operations
- Hydrocarbon mixtures
- High-pressure systems

**Limitations:**
- Less accurate for polar compounds
- Poor for aqueous systems
- Limited accuracy near critical point

**Usage:**
```python
set_property_package(session_id, "Peng-Robinson")
```

### Soave-Redlich-Kwong (SRK)

Alternative cubic EOS, similar to Peng-Robinson.

**Best for:**
- Gas processing
- Hydrocarbon systems
- Slightly better for light gases

**Limitations:**
- Similar to PR limitations
- Liquid density less accurate than PR

### Peng-Robinson with Volume Translation (PR-VT)

PR with improved liquid density predictions.

**Best for:**
- Applications requiring accurate liquid density
- Same systems as standard PR

### GERG-2008

High-accuracy multiparameter EOS for natural gas.

**Best for:**
- Pipeline-quality natural gas
- Custody transfer calculations
- When high accuracy is required

**Limitations:**
- Limited compound coverage (21 natural gas components)
- Computationally intensive

## Activity Coefficient Models

### NRTL (Non-Random Two-Liquid)

Activity coefficient model for liquid-liquid and vapor-liquid equilibrium.

**Best for:**
- Polar mixtures
- Aqueous-organic systems
- Azeotropic distillation
- Liquid-liquid extraction

**Parameters:**
- Binary interaction parameters (αij, τij)
- Available in DWSIM database for common pairs

**Usage:**
```python
set_property_package(session_id, "NRTL")
```

### UNIQUAC

Universal Quasi-Chemical activity coefficient model.

**Best for:**
- Similar to NRTL applications
- Better for size-asymmetric mixtures
- Polymer solutions

### Wilson

Simple activity coefficient model.

**Best for:**
- Miscible systems only
- Alcohol-water mixtures
- Simpler than NRTL/UNIQUAC

**Limitations:**
- Cannot model liquid-liquid equilibrium

## Combined Models

### Peng-Robinson + NRTL

Combines PR EOS for vapor phase with NRTL for liquid activity.

**Best for:**
- Mixed hydrocarbon/polar systems
- Natural gas with water
- Chemical processes with polar components

### SRK + NRTL

Alternative combining SRK with NRTL activity model.

## Specialized Models

### Steam Tables (IAPWS-IF97)

Industry-standard properties for pure water/steam.

**Best for:**
- Power plant steam cycles
- Pure water systems
- High-accuracy water properties

**Usage:**
```python
set_property_package(session_id, "Steam Tables (IAPWS-IF97)")
```

### UNIFAC

Predictive activity coefficient model using group contributions.

**Best for:**
- Systems lacking experimental data
- Screening calculations
- New compound combinations

**Limitations:**
- Less accurate than fitted models (NRTL, UNIQUAC)
- Limited temperature extrapolation

### CoolProp

High-accuracy properties for pure fluids and mixtures.

**Best for:**
- Refrigeration systems
- Pure fluid properties
- When REFPROP accuracy needed

## Selection Guide

| System Type | Recommended Package |
|-------------|---------------------|
| Light hydrocarbons (C1-C6) | Peng-Robinson |
| Natural gas | Peng-Robinson or GERG-2008 |
| Heavy hydrocarbons | Peng-Robinson |
| Polar + hydrocarbons | PR + NRTL |
| Aqueous systems | NRTL or UNIQUAC |
| Steam/water | Steam Tables |
| Alcohols + water | NRTL or Wilson |
| Refrigerants | CoolProp |
| Unknown/screening | UNIFAC |

## Tips for LLM Agents

1. **Default to Peng-Robinson** for hydrocarbon systems
2. **Use NRTL** when water or polar compounds are present
3. **Check compound coverage**: Some packages have limited databases
4. **Validate against data**: Compare results with known values when possible
5. **Consider pressure range**: Cubic EOS work best at moderate pressures
6. **Watch for phase behavior**: Ensure package can model all expected phases

## Binary Interaction Parameters

Many property packages require binary interaction parameters (kij, BIPs) for accurate VLE predictions. DWSIM includes a database of common parameters, but custom values can be specified for specific compound pairs.

```python
# Example: Setting custom BIP (if supported)
set_binary_interaction(session_id, "Methane", "Ethane", 0.003)
```
