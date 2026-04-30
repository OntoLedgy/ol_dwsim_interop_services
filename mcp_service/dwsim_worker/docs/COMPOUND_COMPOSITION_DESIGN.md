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

# Compound and Composition Architecture

## Overview

This document explains how compounds and stream compositions are managed in the DwsimWorker system. Understanding this architecture is critical for correctly creating and manipulating material streams.

---

## Key Concepts

### Separation of Concerns

The system separates two distinct responsibilities:

1. **Compound Management** (flowsheet-level): Which chemicals exist in the simulation
2. **Stream Composition** (stream-level): What percentage of each chemical is in a specific stream

This separation avoids redundancy and matches how DWSIM internally manages flowsheets.

---

## Architecture Details

### 1. Compounds (Flowsheet-Level)

**Location**: Managed by `CompoundAdapter`
**Scope**: Global to the entire flowsheet
**Purpose**: Define which chemicals are available for use in streams

```csharp
// Compounds are added to the flowsheet ONCE
var compoundAdapter = new CompoundAdapter(logger, context);

compoundAdapter.AddCompound("Water");      // Compound index 0
compoundAdapter.AddCompound("Ethanol");    // Compound index 1
compoundAdapter.AddCompound("Methanol");   // Compound index 2
```

**Important Properties:**
- Compounds are added in order and assigned implicit indices (0, 1, 2, ...)
- All streams in the flowsheet share the same compound list
- Compound order is fixed once added
- Compound names are case-insensitive ("Water" = "water" = "WATER")

### 2. Composition (Stream-Level)

**Location**: Part of `StreamProperties` model
**Scope**: Specific to each individual stream
**Purpose**: Define mole fraction of each compound in this particular stream

```csharp
// Composition stores only the mole fractions (positional array)
// Index 0 = fraction of first compound added to flowsheet
// Index 1 = fraction of second compound added to flowsheet
// etc.

var composition = new Composition(new[] { 0.5, 0.3, 0.2 });
//                                        ^    ^    ^
//                                        |    |    └─ 20% Methanol (index 2)
//                                        |    └────── 30% Ethanol (index 1)
//                                        └─────────── 50% Water (index 0)
```

**Important Properties:**
- Mole fractions are stored as a positional array
- Index corresponds to the order compounds were added to the flowsheet
- Mole fractions must sum to 1.0 ± 1e-6 (validated in constructor)
- Each mole fraction must be between 0.0 and 1.0
- The `Composition` class is immutable after creation

---

## Complete Workflow Example

### Example: Creating a Binary Distillation Flowsheet

```csharp
// ========================================
// STEP 1: Initialize Context
// ========================================
var context = new FlowsheetContext(logger, config);
context.Initialize();

// ========================================
// STEP 2: Add Compounds (Global)
// ========================================
var compoundAdapter = new CompoundAdapter(logger, context);

// Add compounds to flowsheet (order matters!)
compoundAdapter.AddCompound("Water");      // Index 0
compoundAdapter.AddCompound("Ethanol");    // Index 1

// Now ALL streams in this flowsheet will reference these compounds by index

// ========================================
// STEP 3: Set Property Package
// ========================================
var packageAdapter = new PropertyPackageAdapter(logger, context);
packageAdapter.SetPropertyPackage("NRTL"); // Good for alcohol-water systems

// ========================================
// STEP 4: Create Streams with Compositions
// ========================================
var streamAdapter = new StreamAdapter(logger, context);

// Create FEED stream (50% Water, 50% Ethanol)
var feedComposition = new Composition(new[] { 0.5, 0.5 });
var feedProps = CreateStreamProperties(
    temperature: 298.15,  // K
    pressure: 101325,     // Pa
    molarFlow: 100.0,     // mol/s
    composition: feedComposition
);
streamAdapter.CreateStream("FEED", feedProps);

// Create DISTILLATE stream (10% Water, 90% Ethanol - high purity ethanol)
var distillateComposition = new Composition(new[] { 0.1, 0.9 });
var distillateProps = CreateStreamProperties(
    temperature: 351.15,  // K
    pressure: 101325,     // Pa
    molarFlow: 45.0,      // mol/s
    composition: distillateComposition
);
streamAdapter.CreateStream("DISTILLATE", distillateProps);

// Create BOTTOMS stream (95% Water, 5% Ethanol - high purity water)
var bottomsComposition = new Composition(new[] { 0.95, 0.05 });
var bottomsProps = CreateStreamProperties(
    temperature: 373.15,  // K
    pressure: 101325,     // Pa
    molarFlow: 55.0,      // mol/s
    composition: bottomsComposition
);
streamAdapter.CreateStream("BOTTOMS", bottomsProps);
```

---

## Design Rationale

### Why Separate Compounds from Composition?

**1. Avoid Redundancy**
```csharp
// BAD: Storing compound names in every stream (redundant)
class Composition {
    Dictionary<string, double> moleFractions; // "Water" -> 0.5, "Ethanol" -> 0.5
}

// GOOD: Store once at flowsheet level, reference by index
// Flowsheet: ["Water", "Ethanol"]
class Composition {
    double[] moleFractions; // [0.5, 0.5]
}
```

**2. Match DWSIM's Internal Model**

DWSIM internally manages compounds at the flowsheet level, not the stream level. Our architecture mirrors this for seamless integration.

**3. Performance**

Arrays are faster and more memory-efficient than dictionaries when you have:
- Small number of compounds (typically 2-20)
- Frequent composition lookups
- Need for ordered iteration

**4. Type Safety**

Positional arrays prevent typos and name mismatches:
```csharp
// BAD: Easy to make typos
composition["Watr"] = 0.5;  // Oops, typo!

// GOOD: Compile-time index checking
composition[0] = 0.5;  // Clear, no ambiguity
```

---

## Common Patterns

### Pattern 1: Pure Component Stream

```csharp
// Flowsheet has: ["Water", "Ethanol", "Methanol"]

// Create pure water stream (100% water)
var pureWater = new Composition(new[] { 1.0, 0.0, 0.0 });

// Create pure ethanol stream (100% ethanol)
var pureEthanol = new Composition(new[] { 0.0, 1.0, 0.0 });
```

### Pattern 2: Trace Components

```csharp
// Main component with trace impurities
var composition = new Composition(new[] {
    0.999,  // 99.9% Water
    0.0005, // 0.05% Ethanol
    0.0005  // 0.05% Methanol
});
```

### Pattern 3: Equal Mixture

```csharp
// Three compounds: ["A", "B", "C"]
var n = 3;
var equalFractions = Enumerable.Repeat(1.0 / n, n).ToArray();
var composition = new Composition(equalFractions); // [0.333..., 0.333..., 0.333...]
```

---

## Validation Rules

### Composition Validation

The `Composition` class enforces strict validation in its constructor:

```csharp
public Composition(IReadOnlyList<double> moleFractions)
{
    // Rule 1: Cannot be null or empty
    if (moleFractions == null || moleFractions.Count == 0)
        throw new ArgumentNullException();

    // Rule 2: Each fraction must be between 0 and 1
    foreach (var fraction in moleFractions)
    {
        if (fraction < 0.0 || fraction > 1.0)
            throw new ArgumentException("Mole fraction out of range [0, 1]");
    }

    // Rule 3: Must sum to 1.0 ± 1e-6
    double sum = moleFractions.Sum();
    if (Math.Abs(sum - 1.0) > 1e-6)
        throw new ArgumentException("Mole fractions must sum to 1.0");
}
```

### Common Validation Errors

**Error 1: Fractions Don't Sum to 1.0**
```csharp
// WRONG: Sum = 0.8 (not 1.0)
var bad = new Composition(new[] { 0.5, 0.3 }); // Throws!

// CORRECT: Sum = 1.0
var good = new Composition(new[] { 0.5, 0.5 });
```

**Error 2: Negative Fractions**
```csharp
// WRONG: Negative fraction
var bad = new Composition(new[] { 1.2, -0.2 }); // Throws!

// CORRECT: All positive
var good = new Composition(new[] { 0.6, 0.4 });
```

**Error 3: Fractions > 1.0**
```csharp
// WRONG: Fraction exceeds 1.0
var bad = new Composition(new[] { 1.5, -0.5 }); // Throws!

// CORRECT: All ≤ 1.0
var good = new Composition(new[] { 0.7, 0.3 });
```

---

## Querying Compound Information

### Getting Compound List

```csharp
var compoundAdapter = new CompoundAdapter(logger, context);

// Get all compounds added to flowsheet
var result = compoundAdapter.GetCompounds();
if (result.Success)
{
    var compounds = (List<string>)result.Data;
    // compounds = ["Water", "Ethanol", "Methanol"]

    // Now you know:
    // Index 0 = Water
    // Index 1 = Ethanol
    // Index 2 = Methanol
}
```

### Validating Compound Names

```csharp
// Check if a compound name is valid before adding
var result = compoundAdapter.ValidateCompoundName("Water");
if (result.Success)
{
    compoundAdapter.AddCompound("Water");
}
```

### Getting Available Compounds

```csharp
// Get list of all compounds in DWSIM database
var result = compoundAdapter.GetAvailableCompounds();
// Returns: ValidationResult with message containing compound names
```

---

## Integration with DWSIM

### DWSIM's Internal Model

DWSIM stores compounds and compositions using a similar architecture:

```vb
' DWSIM MaterialStream (simplified)
Public Class MaterialStream
    ' Phases contain compounds
    Public Property Phases As Dictionary(Of Integer, Phase)
End Class

Public Class Phase
    ' Each phase has compounds with mole fractions
    Public Property Compounds As Dictionary(Of String, Compound)
End Class

Public Class Compound
    Public Property Name As String
    Public Property MoleFraction As Double
    Public Property MassFlow As Double
    Public Property MolarFlow As Double
End Class
```

### Our Adapter's Mapping

**Our Model** → **DWSIM Model**

```
CompoundAdapter.AddCompound("Water")
    ↓
FlowsheetContext adds to Flowsheet.Compounds
    ↓
DWSIM: Flowsheet.AvailableCompounds.Add("Water")

StreamAdapter.CreateStream("FEED", properties)
    ↓
Creates MaterialStream with Composition
    ↓
DWSIM: stream.Phases[0].Compounds["Water"].MoleFraction = 0.5
```

---

## Testing Considerations

### Unit Tests

When writing tests, you need to:

1. **Add compounds before creating streams**
   ```csharp
   [Fact]
   public void CreateStream_WithValidComposition_Succeeds()
   {
       // Arrange - Add compounds FIRST
       compoundAdapter.AddCompound("Water");
       compoundAdapter.AddCompound("Ethanol");

       // Now create composition
       var composition = new Composition(new[] { 0.5, 0.5 });
       var properties = CreateStreamProperties(composition);

       // Act
       var result = streamAdapter.CreateStream("FEED", properties);

       // Assert
       Assert.True(result.Success);
   }
   ```

2. **Know the compound count**
   ```csharp
   // If flowsheet has 2 compounds, composition MUST have 2 fractions
   var composition = new Composition(new[] { 0.6, 0.4 }); // Correct

   // WRONG: Mismatch in count
   var bad = new Composition(new[] { 0.5, 0.3, 0.2 }); // 3 fractions for 2 compounds
   ```

### Integration Tests

Integration tests should verify the compound-composition relationship:

```csharp
[Fact]
public void CompoundOrder_PreservedInComposition()
{
    // Arrange
    compoundAdapter.AddCompound("A");
    compoundAdapter.AddCompound("B");
    compoundAdapter.AddCompound("C");

    var composition = new Composition(new[] { 0.5, 0.3, 0.2 });
    var properties = CreateStreamProperties(composition);
    streamAdapter.CreateStream("FEED", properties);

    // Act - Get properties back
    var result = streamAdapter.GetProperties("FEED");
    var retrievedComp = ((StreamProperties)result.Data).Composition;

    // Assert - Order is preserved
    Assert.Equal(0.5, retrievedComp.MoleFractions[0]); // A
    Assert.Equal(0.3, retrievedComp.MoleFractions[1]); // B
    Assert.Equal(0.2, retrievedComp.MoleFractions[2]); // C
}
```

---

## Best Practices

1. **Add All Compounds First**
   ```csharp
   // GOOD: Add all compounds before creating streams
   compoundAdapter.AddCompound("Water");
   compoundAdapter.AddCompound("Ethanol");
   streamAdapter.CreateStream("FEED", ...);

   // BAD: Don't interleave compound addition and stream creation
   compoundAdapter.AddCompound("Water");
   streamAdapter.CreateStream("STREAM1", ...);
   compoundAdapter.AddCompound("Ethanol"); // Order becomes confusing
   ```

2. **Document Compound Order**
   ```csharp
   // Document the compound order in comments for clarity
   // Compounds: [0: Water, 1: Ethanol, 2: Methanol]
   var feedComp = new Composition(new[] { 0.5, 0.3, 0.2 });
   ```

3. **Use Helper Methods**
   ```csharp
   // Create helper to make composition creation clearer
   public static Composition CreateBinaryComposition(
       double fraction1, double fraction2)
   {
       if (Math.Abs(fraction1 + fraction2 - 1.0) > 1e-6)
           throw new ArgumentException("Fractions must sum to 1.0");

       return new Composition(new[] { fraction1, fraction2 });
   }

   // Usage is clearer
   var comp = CreateBinaryComposition(waterFraction: 0.7, ethanolFraction: 0.3);
   ```

4. **Validate Early**
   ```csharp
   // Validate composition will be created successfully
   var fractions = new[] { 0.5, 0.3 };
   if (!Composition.SumsToOne(fractions))
   {
       // Fix before creating Composition object
       fractions = NormalizeFractions(fractions);
   }
   var composition = new Composition(fractions);
   ```

---

## Future Enhancements

### Potential Improvements

1. **Named Composition Builder**
   ```csharp
   // Future enhancement: Builder with named compounds
   var comp = new CompositionBuilder(context)
       .WithCompound("Water", 0.5)
       .WithCompound("Ethanol", 0.5)
       .Build(); // Automatically maps to indices
   ```

2. **Composition Utilities**
   ```csharp
   public static class CompositionUtilities
   {
       public static Composition Normalize(double[] fractions)
       {
           double sum = fractions.Sum();
           var normalized = fractions.Select(f => f / sum).ToArray();
           return new Composition(normalized);
       }

       public static Composition FromMassFractions(double[] massFractions,
                                                    double[] molecularWeights)
       {
           // Convert mass fractions to mole fractions
           // ...
       }
   }
   ```

3. **Compound Registry**
   ```csharp
   public class CompoundRegistry
   {
       private readonly List<string> _compounds = new List<string>();

       public int GetIndex(string compoundName)
       {
           return _compounds.IndexOf(compoundName);
       }

       public string GetName(int index)
       {
           return _compounds[index];
       }
   }
   ```

---

## Summary

- **Compounds** are managed at the flowsheet level (globally)
- **Compositions** are positional arrays of mole fractions (per-stream)
- **Index position** in composition array corresponds to order compounds were added
- **Validation** ensures mole fractions are valid (0-1, sum to 1.0)
- **Architecture** matches DWSIM's internal model for seamless integration

This design provides:
- ✅ Clear separation of concerns
- ✅ Memory efficiency
- ✅ Type safety
- ✅ Performance
- ✅ Seamless DWSIM integration
