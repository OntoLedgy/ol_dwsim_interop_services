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

# DWSIM Architecture Findings

This document describes key findings about DWSIM's internal architecture discovered during integration work.

## Flowsheet Class Structure

### The FOSSEEFlowsheet Discovery

During implementation of the FlowsheetContext component, we discovered that DWSIM's actual backend Flowsheet class is **not** named `DWSIM.SharedClasses.Flowsheet` as might be expected from documentation.

**The correct class is**: `DWSIM.SharedClasses.FOSSEEFlowsheet`

### Flowsheet-Related Types in DWSIM

DWSIM contains several types with "Flowsheet" in the name, each serving different purposes:

| Type Name | Namespace | Purpose | Usability |
|-----------|-----------|---------|-----------|
| `FOSSEEFlowsheet` | `DWSIM.SharedClasses` | **Backend model class** - Core flowsheet logic, simulation engine interface | ✅ **Use this** - Can be instantiated via `Activator.CreateInstance()` |
| `FormFlowsheet` | `DWSIM` | UI wrapper class - Windows Forms-based graphical interface | ❌ Requires graphics context, cannot be instantiated in console/service applications |
| `FlowsheetBag` | `DWSIM.SharedClasses` | Data transfer object - Serialization container | ℹ️ For persistence/serialization only |
| `FlowsheetResults` | Various | Results container - Calculation outputs | ℹ️ Result type, not for direct instantiation |

### Why FOSSEEFlowsheet?

The name "FOSSEE" refers to the **Free and Open Source Software for Education and Engineering** project, which contributed to DWSIM's development. The FOSSEEFlowsheet class represents the backend computational model that can be used without a UI context.

### Assembly Loading Requirements

To successfully work with DWSIM's Flowsheet classes, you must load the **DWSIM.exe** assembly in addition to the standard DLL assemblies:

```csharp
public static readonly string[] DefaultRequiredAssemblies = new[]
{
    "DWSIM.Interfaces",
    "DWSIM.Thermodynamics",
    "DWSIM.SharedClasses",
    "DWSIM",              // Main DWSIM.exe - CRITICAL!
    "CapeOpen"
};
```

**Key Points**:
- DWSIM.exe is the main executable but also functions as a .NET assembly
- The `FOSSEEFlowsheet` type is defined in `DWSIM.SharedClasses.dll` but depends on types from `DWSIM.exe`
- Use `Assembly.LoadFrom()` with both `.dll` and `.exe` extensions

### Type Discovery Pattern

When working with DWSIM, use a fallback pattern to handle potential naming variations across versions:

```csharp
private static readonly string[] PossibleFlowsheetTypeNames = new[]
{
    "DWSIM.SharedClasses.FOSSEEFlowsheet",  // Primary: backend model class
    "DWSIM.SharedClasses.Flowsheet",         // Legacy fallback
    "DWSIM.Flowsheet.Flowsheet",            // Alternative namespace
    "DWSIM.Simulator.Flowsheet"             // Another possibility
    // Note: DWSIM.FormFlowsheet excluded - it's UI-only
};
```

### Instantiation Example

```csharp
// Correct approach - instantiate FOSSEEFlowsheet
var flowsheetType = Type.GetType("DWSIM.SharedClasses.FOSSEEFlowsheet");
if (flowsheetType == null)
{
    // Search in loaded assemblies
    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
        flowsheetType = assembly.GetType("DWSIM.SharedClasses.FOSSEEFlowsheet");
        if (flowsheetType != null) break;
    }
}

var flowsheet = Activator.CreateInstance(flowsheetType);
```

## Validation Strategy

The `DwsimValidator` class validates that DWSIM assemblies are functional by attempting to instantiate:
1. `DWSIM.SharedClasses.FOSSEEFlowsheet` - Confirms flowsheet engine is available
2. `DWSIM.Thermodynamics.Streams.MaterialStream` - Confirms thermodynamics subsystem works

This ensures assemblies are not just loaded, but actually usable for simulation work.

## References

- DWSIM Project: https://dwsim.org
- FOSSEE Initiative: https://fossee.in/
- DWSIM Source: https://github.com/DanWBR/dwsim

## Version Information

These findings are based on:
- DWSIM Version: 9.0.5.0
- Assembly Set: DWSIM.Interfaces, DWSIM.Thermodynamics, DWSIM.SharedClasses, DWSIM.exe, CapeOpen
- Discovery Date: January 2026
