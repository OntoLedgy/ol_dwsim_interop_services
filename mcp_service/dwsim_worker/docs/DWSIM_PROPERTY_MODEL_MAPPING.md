# DWSIM Property Model Mapping

## Overview

This document maps our DwsimWorker physical property model to DWSIM's internal architecture. Understanding this mapping is critical for correctly interfacing with DWSIM's property system.

---

## Architecture Comparison

### Our Model (DwsimWorker)

```
PhysicalQuantities (abstract base class)
├── Temperature
├── Pressure
├── Distance
└── FlowRate (abstract)
    ├── MassFlowRate
    ├── MolarFlowRate
    └── VolumetricFlowRate

UnitsOfMeasure (class)
├── UnitName: string (e.g., "K", "Pa", "mol/s")
├── QuantityType: Type (references PhysicalQuantities subclass)
└── ValidRange: Ranges (min/max validation)

Measurements (class)
├── Quantity: PhysicalQuantities
├── Value: double
└── Unit: UnitsOfMeasure

PhysicalProperties (class)
├── Name: string (e.g., "Temperature", "InletPressure")
└── Measurement: Measurements

StreamProperties (class)
├── Temperature: PhysicalProperties
├── Pressure: PhysicalProperties
├── MolarFlow: PhysicalProperties
└── Composition: Composition
```

### DWSIM Model

```
MaterialStream
├── Phases: Dictionary<int, IPhase>
│   ├── [0] Mixture (Overall)
│   ├── [1] OverallLiquid
│   ├── [2] Vapor
│   ├── [3] Liquid1
│   ├── [4] Liquid2
│   └── [7] Solid
│
├── Methods:
│   ├── GetTemperature() -> double (in SI units)
│   ├── GetPressure() -> double (in SI units)
│   ├── GetMolarFlow() -> double (in SI units)
│   ├── GetMassFlow() -> double (in SI units)
│   └── GetVolumetricFlow() -> double (in SI units)
│
└── PropertyPackage: PropertyPackage

Phase
├── Properties: PhaseProperties
└── Compounds: Dictionary<string, Compound>

PhaseProperties (all Nullable<double>, in SI units)
├── temperature (K)
├── pressure (Pa)
├── molarflow (mol/s)
├── massflow (kg/s)
├── volumetric_flow (m3/s)
├── enthalpy (kJ/kg)
├── entropy (kJ/[kg.K])
├── density (kg/m3)
├── viscosity (Pa.s)
└── ... (60+ properties)

SystemsOfUnits.Units (implements IUnitsOfMeasure)
├── temperature: string (e.g., "K", "C", "F", "R")
├── pressure: string (e.g., "Pa", "bar", "psi", "atm")
├── molarflow: string (e.g., "mol/s", "kmol/h", "lbmol/h")
├── GetUnitSet(measureID) -> List<string> (available units)
├── GetUnitType(unitString) -> Enums.UnitOfMeasure
└── GetCurrentUnits(measureID) -> string (current unit)

SystemsOfUnits.Converter (static class)
├── ConvertToSI(unitString, value) -> double
└── ConvertFromSI(unitString, value) -> double
```

---

## Key Differences

| Aspect | Our Model | DWSIM Model |
|--------|-----------|-------------|
| **Storage** | Value + Unit (can be any unit) | Always SI units internally |
| **Unit Type** | UnitsOfMeasure class with type safety | String-based units |
| **Property Access** | Named properties with measurements | Direct nullable properties on PhaseProperties |
| **Validation** | Range validation on UnitsOfMeasure | Validation via IsValid property on nullable values |
| **Conversion** | Not yet implemented (delegated to DWSIM) | Converter.ConvertToSI / ConvertFromSI |
| **Type System** | Compile-time type checking | Runtime string matching |

---

## Detailed Mapping

### 1. Physical Quantities

**Our PhysicalQuantities → DWSIM Enums.UnitOfMeasure**

| Our Class | DWSIM Enum | CAPE-OPEN Property Name |
|-----------|------------|------------------------|
| `Temperature` | `Enums.UnitOfMeasure.temperature` | `"temperature"` |
| `Pressure` | `Enums.UnitOfMeasure.pressure` | `"pressure"` |
| `MolarFlowRate` | `Enums.UnitOfMeasure.molarflow` | `"totalFlow"` (molar basis) |
| `MassFlowRate` | `Enums.UnitOfMeasure.massflow` | `"totalFlow"` (mass basis) |
| `VolumetricFlowRate` | `Enums.UnitOfMeasure.volumetricFlow` | `"totalFlow"` (volume basis) |
| `Distance` | `Enums.UnitOfMeasure.distance` | N/A |

### 2. Units of Measure

**Our UnitsOfMeasure.UnitName → DWSIM Unit Strings**

#### Temperature
| Our Unit | DWSIM String | Conversion to SI (K) |
|----------|--------------|---------------------|
| "K" | "K" | value (base unit) |
| "C" | "C" | value + 273.15 |
| "F" | "F" | (value - 32) × 5/9 + 273.15 |
| "R" | "R" | value / 1.8 |

#### Pressure
| Our Unit | DWSIM String | Conversion to SI (Pa) |
|----------|--------------|---------------------|
| "Pa" | "Pa" | value (base unit) |
| "bar" | "bar" | value × 100000 |
| "psi" | "psi", "psia" | value / 0.000145038 |
| "atm" | "atm" | value / 1.01325e-5 |
| "kPa" | "kPa" | value / 0.001 |

#### Molar Flow Rate
| Our Unit | DWSIM String | Conversion to SI (mol/s) |
|----------|--------------|------------------------|
| "mol/s" | "mol/s" | value (base unit) |
| "kmol/s" | "kmol/s" | value × 1000 |
| "kmol/h" | "kmol/h" | value / 3.6 |
| "lbmol/h" | "lbmol/h" | value / 7.93664 |

#### Mass Flow Rate
| Our Unit | DWSIM String | Conversion to SI (kg/s) |
|----------|--------------|----------------------|
| "kg/s" | "kg/s" | value (base unit) |
| "kg/h" | "kg/h" | value / 3600 |
| "lb/h", "lbm/h" | "lbm/h" | value / 7936.64 |
| "t/h" | "t/h" | value / 3.6 |

#### Distance
| Our Unit | DWSIM String | Conversion to SI (m) |
|----------|--------------|-------------------|
| "m" | "m" | value (base unit) |
| "ft" | "ft" | value / 3.28084 |
| "cm" | "cm" | value / 100 |
| "mm" | "mm" | value / 1000 |

### 3. Property Access Patterns

#### Reading Stream Properties

**DWSIM Way:**
```vb
' Get temperature in SI units (K)
Dim tempK As Double = materialStream.Phases(0).Properties.temperature.GetValueOrDefault()

' Get pressure in SI units (Pa)
Dim pressPa As Double = materialStream.Phases(0).Properties.pressure.GetValueOrDefault()

' Get molar flow in SI units (mol/s)
Dim molarFlow As Double = materialStream.Phases(0).Properties.molarflow.GetValueOrDefault()

' Or use convenience methods
Dim tempK2 As Double = materialStream.GetTemperature()
Dim pressPa2 As Double = materialStream.GetPressure()
```

**Our Way (via StreamAdapter):**
```csharp
// Our StreamProperties stores value + unit
var streamProps = new StreamProperties(
    temperature: new PhysicalProperties("Temperature",
        new Measurements(new Temperature(), 298.15, kelvinUnit)),
    pressure: new PhysicalProperties("Pressure",
        new Measurements(new Pressure(), 101325, pascalUnit)),
    molarFlow: new PhysicalProperties("MolarFlow",
        new Measurements(new MolarFlowRate(), 1.0, molPerSecUnit)),
    composition: composition
);

// StreamAdapter converts to/from DWSIM's SI units
var result = streamAdapter.SetProperties("FEED", streamProps);
```

#### Unit Conversion Strategy

**DWSIM:**
- Stores all values internally in SI base units
- Uses `Converter.ConvertToSI(unitString, value)` when setting properties
- Uses `Converter.ConvertFromSI(unitString, value)` when displaying in UI

**Our Approach:**
- Store value + unit as user provided
- When interfacing with DWSIM:
  1. Convert from our unit to SI using DWSIM's Converter
  2. Set the SI value on DWSIM's PhaseProperties
  3. When reading from DWSIM, get SI value and optionally convert to user's preferred unit

---

## Integration Points

### StreamAdapter Integration

The `StreamAdapter` class bridges between our model and DWSIM's model:

```csharp
// File: DwsimWorker/Adapters/StreamAdapter.cs

// Setting properties (our model → DWSIM)
public PropertySetResult SetProperties(string streamId, StreamProperties properties)
{
    var stream = GetStream(streamId); // Returns DWSIM MaterialStream

    // Convert our Temperature to DWSIM's SI units
    double tempInKelvin = properties.Temperature.Value;
    string tempUnit = properties.Temperature.Unit.UnitName;

    // Use DWSIM's converter to get SI value
    double tempSI = SystemsOfUnits.Converter.ConvertToSI(tempUnit, tempInKelvin);

    // Set on DWSIM's PhaseProperties (in SI units)
    stream.Phases[0].Properties.temperature = tempSI;

    // Similar for pressure, flow, etc.
}

// Getting properties (DWSIM → our model)
public PropertySetResult GetProperties(string streamId)
{
    var stream = GetStream(streamId);

    // Get from DWSIM (always in SI)
    double tempSI = stream.Phases[0].Properties.temperature.GetValueOrDefault();

    // Create our model (we can keep in SI or convert to user's preferred unit)
    var tempUnit = new UnitsOfMeasure("K", typeof(Temperature),
        new Ranges(0, double.PositiveInfinity));
    var tempMeasurement = new Measurements(new Temperature(), tempSI, tempUnit);
    var tempProperty = new PhysicalProperties("Temperature", tempMeasurement);

    return PropertySetResult.SuccessResultWithData(
        new StreamProperties(tempProperty, pressProperty, flowProperty, composition));
}
```

### Unit Conversion Helper (Future Implementation)

We should create a helper class that wraps DWSIM's Converter:

```csharp
// Future: DwsimWorker/Utilities/UnitConverter.cs

public static class UnitConverter
{
    public static double ToSI(UnitsOfMeasure unit, double value)
    {
        return SystemsOfUnits.Converter.ConvertToSI(unit.UnitName, value);
    }

    public static double FromSI(UnitsOfMeasure unit, double value)
    {
        return SystemsOfUnits.Converter.ConvertFromSI(unit.UnitName, value);
    }

    public static double Convert(double value, UnitsOfMeasure fromUnit, UnitsOfMeasure toUnit)
    {
        // Convert to SI first, then to target unit
        double siValue = ToSI(fromUnit, value);
        return FromSI(toUnit, siValue);
    }
}
```

---

## CAPE-OPEN Property Names

DWSIM implements CAPE-OPEN interfaces for interoperability. Key property names:

| CAPE-OPEN Name | DWSIM Property | Our Model | Units (SI) |
|----------------|----------------|-----------|-----------|
| `"temperature"` | `Phases[0].Properties.temperature` | `StreamProperties.Temperature` | K |
| `"pressure"` | `Phases[0].Properties.pressure` | `StreamProperties.Pressure` | Pa |
| `"totalFlow"` (molar) | `Phases[0].Properties.molarflow` | `StreamProperties.MolarFlow` | mol/s |
| `"flow"` | Per-compound flows | `Composition.MoleFractions` | mol/s |
| `"fraction"` | Per-compound fractions | `Composition.MoleFractions` | dimensionless |
| `"enthalpy"` | `Phases[0].Properties.enthalpy` | Future | kJ/kg |
| `"entropy"` | `Phases[0].Properties.entropy` | Future | kJ/(kg·K) |

---

## Best Practices for Integration

### 1. Always Use SI Units When Interfacing with DWSIM

```csharp
// GOOD: Convert to SI before setting
double valueSI = Converter.ConvertToSI(unit.UnitName, value);
stream.Phases[0].Properties.temperature = valueSI;

// BAD: Setting non-SI value directly
stream.Phases[0].Properties.temperature = value; // Wrong if value is not in K!
```

### 2. Handle Nullable Properties

```csharp
// DWSIM properties are Nullable<double>
var temp = stream.Phases[0].Properties.temperature;
if (temp.HasValue)
{
    double tempValue = temp.Value;
    // or use GetValueOrDefault()
    double tempValue2 = temp.GetValueOrDefault(); // Returns 0 if null
}
```

### 3. Validate Before Setting

```csharp
// Use our validation
if (!properties.IsValid(out string errorMsg))
{
    return PropertySetResult.FailureResult(errorMsg);
}

// DWSIM also validates
try
{
    stream.Phases[0].Properties.temperature = tempSI;
    stream.Validate(); // Throws if invalid
}
catch (ArgumentException ex)
{
    return PropertySetResult.FailureResult(ex.Message);
}
```

### 4. Use Phases[0] for Overall Stream Properties

```csharp
// CORRECT: Overall stream properties
var overallPhase = stream.Phases[0]; // Mixture phase
double temp = overallPhase.Properties.temperature.GetValueOrDefault();

// INCORRECT: Don't use vapor/liquid phases for overall properties
var vaporPhase = stream.Phases[2]; // This is just the vapor phase!
```

---

## Unit Testing Considerations

When writing tests for adapters, remember:

1. **Mock DWSIM objects must use SI units internally**
   ```csharp
   mockStream.Phases[0].Properties.temperature = 298.15; // K, not C!
   mockStream.Phases[0].Properties.pressure = 101325;    // Pa, not bar!
   ```

2. **Test unit conversions explicitly**
   ```csharp
   [Fact]
   public void SetProperties_ConvertsUnitsToSI()
   {
       // User provides temperature in Celsius
       var tempC = 25.0;
       var properties = CreateStreamProperties(tempC, "C");

       adapter.SetProperties("FEED", properties);

       // Verify DWSIM received it in Kelvin
       Assert.Equal(298.15, mockStream.Phases[0].Properties.temperature, 0.01);
   }
   ```

3. **Test with various units**
   ```csharp
   [Theory]
   [InlineData("K", 298.15, 298.15)]
   [InlineData("C", 25.0, 298.15)]
   [InlineData("F", 77.0, 298.15)]
   public void SetTemperature_HandlesAllUnits(string unit, double value, double expectedK)
   {
       // Test that all temperature units convert correctly
   }
   ```

---

## Future Enhancements

### 1. Add Unit Conversion to Our Model

```csharp
// Measurements.cs - Add conversion method
public Measurements ConvertTo(UnitsOfMeasure targetUnit)
{
    if (targetUnit.QuantityType != Unit.QuantityType)
        throw new ArgumentException("Cannot convert between different quantity types");

    double siValue = UnitConverter.ToSI(Unit, Value);
    double targetValue = UnitConverter.FromSI(targetUnit, siValue);

    return new Measurements(Quantity, targetValue, targetUnit);
}
```

### 2. Support More Physical Quantities

Add classes for:
- `Enthalpy` (energy per mass)
- `Entropy` (energy per mass per temperature)
- `Density` (mass per volume)
- `Viscosity` (dynamic viscosity)
- `ThermalConductivity`
- etc.

### 3. Create Unit Registry

```csharp
public static class StandardUnits
{
    // Temperature
    public static readonly UnitsOfMeasure Kelvin =
        new UnitsOfMeasure("K", typeof(Temperature), new Ranges(0, double.PositiveInfinity));
    public static readonly UnitsOfMeasure Celsius =
        new UnitsOfMeasure("C", typeof(Temperature), new Ranges(-273.15, double.PositiveInfinity));

    // Pressure
    public static readonly UnitsOfMeasure Pascal =
        new UnitsOfMeasure("Pa", typeof(Pressure), new Ranges(0, double.PositiveInfinity));
    public static readonly UnitsOfMeasure Bar =
        new UnitsOfMeasure("bar", typeof(Pressure), new Ranges(0, double.PositiveInfinity));

    // ... etc.
}
```

---

## References

### DWSIM Source Files

- **SystemsOfUnits.vb** (`/mnt/d/S/C#/dwsim/DWSIM.SharedClasses/UnitsOfMeasure/SystemsOfUnits.vb`)
  - Lines 1-500: Units class definition and IUnitsOfMeasure implementation
  - Lines 150-290: GetUnitSet() method - available units per quantity
  - Lines 295-400: GetUnitType() method - string to enum mapping
  - Lines 1315-2600: Converter class with ConvertToSI/ConvertFromSI

- **MaterialStream.vb** (`/mnt/d/S/C#/dwsim/DWSIM.Thermodynamics/MaterialStream/MaterialStream.vb`)
  - Lines 33: Import of Converter: `Imports cv = DWSIM.SharedClasses.SystemsOfUnits.Converter`
  - Lines 98-133: Phase shortcuts (Mixture, Vapor, Liquid1, Liquid2, Solid)
  - Lines 224-231: Property access examples (GetTemperature, GetPressure, etc.)
  - Lines 4904+: CAPE-OPEN interface implementations

- **ThermodynamicsBase.vb** (`/mnt/d/S/C#/dwsim/DWSIM.Thermodynamics/BaseClasses/ThermodynamicsBase.vb`)
  - Lines 150-250: Phase class definition
  - Lines 156: `Public Property Properties As New PhaseProperties`
  - Lines 1258-1400: PhaseProperties class with all property definitions

### Our Implementation Files

- **Ranges.cs** (`DwsimWorker/Models/Ranges.cs`) - Min/max validation
- **PhysicalQuantities.cs** (`DwsimWorker/Models/PhysicalQuantities.cs`) - Quantity type hierarchy
- **UnitsOfMeasure.cs** (`DwsimWorker/Models/UnitsOfMeasure.cs`) - Unit definitions with ranges
- **Measurements.cs** (`DwsimWorker/Models/Measurements.cs`) - Value + quantity + unit
- **PhysicalProperties.cs** (`DwsimWorker/Models/PhysicalProperties.cs`) - Named properties
- **StreamProperties.cs** (`DwsimWorker/Models/StreamProperties.cs`) - Stream property container
- **StreamAdapter.cs** (`DwsimWorker/Adapters/StreamAdapter.cs`) - DWSIM integration layer

---

## Summary

Our model provides type-safe, validated physical properties with explicit units, while DWSIM uses a pragmatic approach with SI units internally and string-based unit identifiers for conversion. The StreamAdapter bridges these approaches by:

1. Accepting user input in any supported unit
2. Converting to SI using DWSIM's Converter before setting properties
3. Reading SI values from DWSIM and presenting them with appropriate units
4. Leveraging DWSIM's proven unit conversion system rather than duplicating it

This architecture allows us to provide a clean API while delegating the complex unit conversion logic to DWSIM's mature implementation.
