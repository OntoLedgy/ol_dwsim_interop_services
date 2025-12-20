# DWSIM Worker

.NET Framework console application that hosts the DWSIM simulation engine and communicates with the Python MCP server via JSON-RPC over Named Pipes.

## Architecture

The worker is responsible for:
- Hosting DWSIM simulation engine on STA threads
- Managing multiple concurrent simulation sessions with isolation
- Exposing DWSIM capabilities via JSON-RPC API
- Enforcing resource limits and timeouts
- Converting between JSON DTOs and DWSIM/CAPE-OPEN objects

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

### Assembly Load Errors

If you encounter "Could not load file or assembly 'DWSIM.XXX'" errors:

1. Verify all DWSIM DLLs are in the same directory as `DwsimWorker.exe`
2. Check binding redirects in `App.config`
3. Ensure DWSIM assemblies are built for the correct .NET Framework version

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

GPLv3 - See LICENSE file for details.

DWSIM is licensed under GPLv3. This worker is a derived work and must comply with DWSIM's license terms.
