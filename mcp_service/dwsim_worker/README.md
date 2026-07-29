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

# DWSIM Worker

.NET Framework console application that hosts the DWSIM simulation engine and communicates with the Python MCP server via JSON-RPC over Named Pipes.

## Architecture

The worker is responsible for:
- Hosting DWSIM simulation engine on STA threads
- Managing multiple concurrent simulation sessions with isolation
- Exposing DWSIM capabilities via JSON-RPC API
- Enforcing resource limits and timeouts
- Converting between JSON DTOs and DWSIM/CAPE-OPEN objects

## Assembly Loading Requirements

DwsimWorker automatically loads DWSIM assemblies at startup using a multi-strategy resolution approach:

### Required DWSIM Assemblies

The following assemblies must be available:
- `DWSIM.Interfaces.dll` - Core interfaces
- `DWSIM.Thermodynamics.dll` - Thermodynamic calculations
- `DWSIM.SharedClasses.dll` - Shared utilities
- `CapeOpen.dll` (optional) - CAPE-OPEN interface definitions

### Assembly Path Resolution

The worker resolves DWSIM assembly paths using this fallback order:

- **Local repo copy (default)**: `App.config` now points to [mcp_service/dwsim_worker/dwsim_binaries/x64/Debug](mcp_service/dwsim_worker/dwsim_binaries/x64/Debug), populated by copying from the sibling `../dwsim/DWSIM/bin/x64/Debug` build.
- **Local config override (gitignored)**: Create `mcp_service/dwsim_worker/dwsim.config.json` (use [dwsim.config.json.sample](dwsim.config.json.sample)) to point to a machine-specific DWSIM build without touching env vars.

1. **Environment Variable**: Set `DWSIM_PATH` to your DWSIM installation directory
   ```cmd
   set DWSIM_PATH=C:\Program Files\DWSIM
   ```

2. **App.config Setting**: Uncomment and set in `App.config`:
   ```xml
   <appSettings>
     <add key="DwsimPath" value="C:\Program Files\DWSIM" />
   </appSettings>
   ```

3. **Default Installation Paths**: Automatically checks common installation locations:
   - `C:\Program Files\DWSIM`
   - `C:\Program Files (x86)\DWSIM`

### Validation

After loading, the worker validates assemblies by instantiating core DWSIM types:
- `DWSIM.SharedClasses.Flowsheet`
- `DWSIM.Thermodynamics.Streams.MaterialStream`

This ensures assemblies are not just loaded but fully functional in a headless environment (no GUI).

### Exit Codes

The worker uses specific exit codes to indicate assembly loading status:

| Exit Code | Meaning | Description |
|-----------|---------|-------------|
| 0 | Success | All assemblies loaded and validated successfully |
| 1 | Load Failure | Assembly files not found or failed to load |
| 2 | Validation Failure | Assemblies loaded but validation failed |
| 3 | Configuration Error | Invalid configuration or timeout |

### Manual Testing

Use the included batch script to test assembly loading:

```cmd
test-assembly-loading.bat
```

Or test with custom path:
```cmd
set DWSIM_PATH=C:\Custom\Path\To\DWSIM
DwsimWorker.exe
echo Exit code: %ERRORLEVEL%
```

## Building

### Using Visual Studio

1. Open `DwsimWorker.sln` in Visual Studio 2019 or later
2. Restore NuGet packages
3. Update DWSIM assembly references in `DwsimWorker.csproj` to point to your DWSIM installation
4. Build the solution (F6)

### Using MSBuild

```cmd
msbuild DwsimWorker.sln /p:Configuration=Release
```

### Using .NET CLI

```bash
dotnet build DwsimWorker.sln --configuration Release
```

## Configuration

Edit `App.config` to configure:

```xml
<appSettings>
  <!-- Named Pipe name (must match Python server config) -->
  <add key="PipeName" value="dwsim_worker_pipe" />

  <!-- Maximum concurrent sessions -->
  <add key="MaxSessions" value="10" />

  <!-- Session timeout in minutes -->
  <add key="SessionTimeoutMinutes" value="60" />

  <!-- Resource limits -->
  <add key="MaxMemoryMB" value="4096" />
  <add key="OperationTimeoutSeconds" value="300" />
</appSettings>
```

## Running

### Standalone

```cmd
cd DwsimWorker\bin\Release
DwsimWorker.exe
```

### As a Windows Service

```cmd
sc create DwsimWorker binPath= "C:\path\to\DwsimWorker.exe"
sc start DwsimWorker
```

## DWSIM Assembly References

The worker requires the following DWSIM assemblies:

- `DWSIM.Interfaces.dll` - Core interfaces
- `DWSIM.Thermodynamics.dll` - Thermodynamic calculations
- `DWSIM.SharedClasses.dll` - Shared utilities
- `CapeOpen.dll` - CAPE-OPEN interface definitions
- `DWSIM.UnitOperations.dll` - Unit operation implementations
- `DWSIM.Thermodynamics.PropertyPackages.dll` - Property packages

**Important:** Update the `<Reference>` elements in `DwsimWorker.csproj` to point to your DWSIM installation directory. By default, it looks for DWSIM assemblies in the parent `dwsim` repository.

## Project Structure

```
dwsim_worker/
├── DwsimWorker/                  # Main console application
│   ├── Program.cs                # Entry point
│   ├── App.config                # Configuration
│   ├── IPC/                      # Named Pipe server and JSON-RPC dispatcher
│   ├── Engine/                   # DWSIM engine hosting and session management
│   ├── Adapters/                 # DWSIM API wrappers
│   ├── Converters/               # JSON ↔ CAPE-OPEN ↔ DWSIM converters
│   ├── Limits/                   # Resource enforcement
│   └── Utilities/                # Helper utilities
├── DwsimWorker.Tests/            # xUnit tests
└── DwsimWorker.sln               # Visual Studio solution
```

## Specification 1.2: Three-Phase Separator Properties (Adapters Layer)

Specification 1.2 adds a comprehensive adapters layer for flowsheet configuration and property manipulation. This layer provides clean, type-safe interfaces for working with DWSIM flowsheets.

### New Components

#### Adapters
High-level API wrappers that simplify DWSIM interactions:

- **CompoundAdapter** - Add compounds from DWSIM database to flowsheet
- **PropertyPackageAdapter** - Configure thermodynamic property packages (Peng-Robinson, NRTL, etc.)
- **StreamAdapter** - Create material streams and set/get thermodynamic properties
- **UnitOpAdapter** - Add unit operations (three-phase separators) and configure parameters
- **ConnectionAdapter** - Connect streams to unit operation ports and validate flowsheet topology

#### Models
Type-safe domain models for physical properties:

- **PhysicalQuantities** - Base types for Temperature, Pressure, MolarFlowRate
- **UnitsOfMeasure** - Unit definitions with valid ranges
- **Measurements** - Value + unit combinations
- **PhysicalProperties** - Named property with measurement
- **StreamProperties** - Complete stream state (T, P, flow, composition)
- **Composition** - Mole fraction array with validation
- **UnitOpConfig** - Unit operation configuration parameters
- **ConnectionInfo** - Stream-to-unit connection details

#### Converters
- **CapeOpenPropertyConverter** - Maps property names to CAPE-OPEN identifiers

#### Exceptions
Custom exception types for domain-specific errors:
- **DwsimException** (base)
- **PropertySetException**
- **CompoundNotFoundException**
- **InvalidPropertyValueException**
- **StreamNotFoundException**
- **UnitNotFoundException**

### Usage Examples

#### Configure a Three-Phase Separator Flowsheet

```csharp
using DwsimWorker.Engine;
using DwsimWorker.Adapters;
using DwsimWorker.Models;

// Initialize flowsheet context
var config = new FlowsheetContextConfigBuilder()
    .WithAssemblyPath("C:\\Path\\To\\DWSIM")
    .WithFlowsheetName("MySeparator")
    .Build();

var context = new FlowsheetContext(logger, config);
context.Initialize();

// Initialize adapters
var compoundAdapter = new CompoundAdapter(logger, context);
var propertyPackageAdapter = new PropertyPackageAdapter(logger, context);
var streamAdapter = new StreamAdapter(logger, context);
var unitOpAdapter = new UnitOpAdapter(logger, context);
var connectionAdapter = new ConnectionAdapter(logger, context);

// Add compounds
compoundAdapter.AddCompound("Methane");
compoundAdapter.AddCompound("Ethane");
compoundAdapter.AddCompound("Propane");
compoundAdapter.AddCompound("Water");

// Set property package
propertyPackageAdapter.SetPropertyPackage("Peng-Robinson");

// Create inlet stream
var inletProps = new StreamProperties(
    new PhysicalProperties("Temperature", new Measurements(new Temperature(), 298.15, kelvinUnit)),
    new PhysicalProperties("Pressure", new Measurements(new Pressure(), 500000, pascalUnit)),
    new PhysicalProperties("MolarFlow", new Measurements(new MolarFlowRate(), 100.0, molPerSecUnit)),
    new Composition(new[] { 0.4, 0.3, 0.2, 0.1 })
);
var inletResult = streamAdapter.CreateStream("INLET", inletProps);

// Create three-phase separator
var separatorConfig = new UnitOpConfig(new Dictionary<string, object>
{
    { "PressureDrop", 5000.0 }  // 0.05 bar pressure drop
});
var separatorResult = unitOpAdapter.AddThreePhaseSeparator("SEP-101", separatorConfig);

// Create outlet streams
streamAdapter.CreateStream("VAPOR_OUT", vaporProps);
streamAdapter.CreateStream("LIQUID1_OUT", liquid1Props);
streamAdapter.CreateStream("LIQUID2_OUT", liquid2Props);

// Connect streams
connectionAdapter.ConnectStream(inletResult.Message, separatorResult.Message, "Inlet");
connectionAdapter.ConnectStream("VAPOR_OUT", separatorResult.Message, "VaporOutlet");
connectionAdapter.ConnectStream("LIQUID1_OUT", separatorResult.Message, "LiquidOutlet1");
connectionAdapter.ConnectStream("LIQUID2_OUT", separatorResult.Message, "LiquidOutlet2");

// Validate topology
var validation = connectionAdapter.ValidateTopology();
if (validation.Success)
{
    Console.WriteLine("Flowsheet configured successfully!");
}
```

#### Property Operations

```csharp
// Set stream property
streamAdapter.SetProperty("INLET", "Temperature", 310.15);  // Change to 310.15 K

// Get stream property
var result = streamAdapter.GetProperty("INLET", "Temperature");
if (result.Success)
{
    var temperature = (double)result.Data;
    Console.WriteLine($"Inlet temperature: {temperature} K");
}

// Get all stream properties
var propsResult = streamAdapter.GetProperties("INLET");
if (propsResult.Success)
{
    var props = (StreamProperties)propsResult.Data;
    Console.WriteLine($"T={props.Temperature.Value.Value} K, P={props.Pressure.Value.Value} Pa");
}
```

### Testing

Spec 1.2 includes comprehensive tests:

#### Unit Tests
- **CompoundAdapterTests** - 17 tests for compound operations
- **PropertyPackageAdapterTests** - 22 tests for property package configuration
- **StreamAdapterTests** - 21 tests for stream creation and property round-trip
- **UnitOpAdapterTests** - 20 tests for unit operation management
- **ConnectionAdapterTests** - 22 tests for stream connections and topology validation

#### Integration Tests
- **ThreePhaseSeparatorIntegrationTests** - Golden test validating complete workflow
  - End-to-end flowsheet configuration
  - Error scenario testing (invalid connections, duplicates)

#### Performance Tests
- **PropertySetPerformanceTests** - NFR performance validation
  - Individual operation latencies (AddCompound < 100ms, CreateStream < 200ms, etc.)
  - Full workflow performance (4 compounds + 4 streams + 1 separator < 2 seconds)
  - Memory footprint validation (< 50MB)
  - Performance baseline metrics for regression detection

Run tests:
```bash
# All tests
dotnet test DwsimWorker.Tests

# Unit tests only
dotnet test DwsimWorker.Tests --filter Category!=Integration&Category!=Performance

# Integration tests
dotnet test DwsimWorker.Tests --filter Category=Integration

# Performance tests
dotnet test DwsimWorker.Tests --filter Category=Performance
```

### Requirements Satisfied

Spec 1.2 implements:
- **Req 1**: Compound management via CompoundAdapter
- **Req 2**: Property package configuration via PropertyPackageAdapter
- **Req 3**: Material stream properties with round-trip validation via StreamAdapter
- **Req 4**: Three-phase separator support via UnitOpAdapter
- **Req 5**: Stream connections via ConnectionAdapter
- **Req 6**: Flowsheet consistency validation

### Design Principles

- **Result Pattern**: All operations return Result types (LoadResult, PropertySetResult, ValidationResult, ConnectionResult) instead of throwing exceptions for expected failures
- **Immutability**: Models are immutable value types
- **Type Safety**: Strong typing for physical quantities with units
- **Validation**: Input validation at adapter boundaries
- **Logging**: Comprehensive structured logging using Serilog
- **Testability**: All adapters accept logger and context via constructor injection

## API Reference

The worker exposes the following JSON-RPC methods to the MCP server:

### Session Management
- `CreateSession(name)` → sessionId
- `CloseSession(sessionId)`
- `SaveCase(sessionId, filePath)`
- `LoadCase(filePath)` → sessionId

### Flowsheet Operations
- `AddCompound(sessionId, compoundId)`
- `SetPropertyPackage(sessionId, packageType)`
- `AddStream(sessionId, streamType, name)`
- `AddUnit(sessionId, unitType, name)`
- `ConnectStreams(sessionId, fromPort, toPort)`

### Simulation
- `RunSimulation(sessionId)` → status
- `GetSimulationStatus(sessionId)` → status
- `GetResults(sessionId, objectId)` → results

### Thermodynamics
- `FlashTP(sessionId, streamId, temperature, pressure)` → phases
- `FlashPH(sessionId, streamId, pressure, enthalpy)` → phases
- `FlashPS(sessionId, streamId, pressure, entropy)` → phases

## Testing

### Unit Tests

```bash
# Using Visual Studio Test Explorer
# Or using command line:
dotnet test DwsimWorker.Tests

# With coverage
dotnet test DwsimWorker.Tests /p:CollectCoverage=true
```

### Integration Tests

Integration tests are located in the repository root `integration-tests/` directory and test the full Python ↔ C# communication stack.

## Troubleshooting

### Assembly Loading Issues

#### Exit Code 1: Assembly Load Failure

**Symptoms**: Worker exits with code 1, log shows "Assembly file not found" or "DWSIM assembly path does not exist"

**Solutions**:
1. **Set DWSIM_PATH environment variable**:
   ```cmd
   set DWSIM_PATH=C:\Program Files\DWSIM
   ```

2. **Configure App.config**: Uncomment the DwsimPath setting in `App.config`:
   ```xml
   <add key="DwsimPath" value="C:\Your\DWSIM\Path" />
   ```

3. **Install DWSIM**: Download from https://dwsim.org and install to default location

4. **Verify assembly files exist**: Check that these files are in the DWSIM directory:
   - DWSIM.Interfaces.dll
   - DWSIM.Thermodynamics.dll
   - DWSIM.SharedClasses.dll

#### Exit Code 2: Validation Failure

**Symptoms**: Worker exits with code 2, log shows "Assembly validation failed"

**Causes**:
- DWSIM assemblies are corrupted
- Incompatible DWSIM version
- Missing dependencies (e.g., CapeOpen.dll)

**Solutions**:
1. Reinstall DWSIM
2. Check logs for specific validation error messages
3. Verify .NET Framework 4.8 is installed
4. Check binding redirects in App.config

#### Version Conflicts

**Symptoms**: "Could not load file or assembly" with version mismatch message

**Solutions**:
1. Check `App.config` binding redirects for Newtonsoft.Json:
   ```xml
   <dependentAssembly>
     <assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" />
     <bindingRedirect oldVersion="0.0.0.0-13.0.0.0" newVersion="13.0.3.0" />
   </dependentAssembly>
   ```

2. Update to match installed DWSIM version requirements

#### Platform/Bitness Issues

**Symptoms**: BadImageFormatException or "is not a valid Win32 application"

**Solutions**:
1. Ensure DwsimWorker is compiled for x64 (DWSIM is 64-bit only)
2. Set Platform target to x64 in project properties
3. Verify you're not mixing x86/x64 assemblies

### Named Pipe Connection Issues

If the Python server cannot connect:

1. Verify the pipe name matches in both `App.config` and Python server config
2. Check Windows Event Viewer for access denied errors
3. Run worker as Administrator if needed
4. Verify Named Pipes are enabled on your system

### Memory Issues

For large simulations:

1. Increase `MaxMemoryMB` in `App.config`
2. Reduce `MaxSessions` to limit concurrent load
3. Enable 64-bit compilation (change Platform target from AnyCPU to x64)

## License

AGPL-3.0-or-later - See LICENSE file for details.

DWSIM is licensed under GPLv3. This worker is a derived work and must comply with DWSIM's license terms.
