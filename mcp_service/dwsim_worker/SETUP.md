<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

# DwsimWorker Setup Guide

This guide now has two separate paths:

- **Installing from a release**: use published binaries from a tagged GitHub Release.
- **Building from source**: use the repo-local contributor workflow and build the .NET worker yourself.

## Which do I want?

Choose **Installing from a release** if you want to run the published package and do not need to compile the worker locally.

Choose **Building from source** if you are editing the worker, debugging the .NET project, or contributing changes in this repository.

## Installing from a release

Tagged releases publish three assets:

1. A Python wheel for `ol-dwsim-mcp-server`
2. A source archive for the tag
3. `DwsimWorker-<version>.zip`, containing the filtered worker build output

Compatibility notes:

- The release workflow is built and smoke-checked against the OntoLedgy DWSIM build [`v9.0.5-mcp`](https://github.com/OntoLedgy/dwsim/releases/tag/v9.0.5-mcp).
- The standalone worker zip does **not** include DWSIM itself.
- The standalone worker zip does **not** include `dwsim.config.json`; you must create that locally.
- For the latest official DWSIM installers and platform notes, see the DWSIM download page: <https://dwsim.org/index.php/download/>

### Option 1: Install the Python wheel

This is the simplest path for most end users. The wheel already bundles the same filtered `DwsimWorker` payload that is used for the standalone worker zip.

#### Step 1: Install the wheel

Install from the downloaded release asset:

```cmd
python -m pip install <downloaded-wheel-file.whl>
```

Or install the tagged version from PyPI:

```cmd
python -m pip install ol-dwsim-mcp-server==<version>
```

#### Step 2: Point the package at DWSIM

If you want the CLI to download the compatible DWSIM binaries used by CI:

```cmd
dwsim-mcp setup --download
```

If you already have a compatible DWSIM build or installation, point the CLI at it:

```cmd
dwsim-mcp setup --dwsim-path "C:/path/to/DWSIM/bin"
```

#### Step 3: Verify and run

```cmd
dwsim-mcp doctor
dwsim-mcp run
```

You only need the standalone `DwsimWorker-<version>.zip` if you want to manage the worker binaries separately from the wheel.

### Option 2: Use the standalone DwsimWorker zip

Use this when you want the worker binaries as a separate download, for example when you are supplying the Python package independently and want to point it at an extracted worker build.

#### Step 1: Download the release assets you need

- Download `DwsimWorker-<version>.zip` from the GitHub Release.
- Download or install a compatible DWSIM build. The supported build used by CI is [`v9.0.5-mcp`](https://github.com/OntoLedgy/dwsim/releases/tag/v9.0.5-mcp).
- Install the Python package separately if you plan to run the MCP server (`dwsim-mcp`).

#### Step 2: Extract the flat zip into the expected worker layout

The release zip is intentionally flat: it contains the contents of `DwsimWorker/bin/Debug/`, not an enclosing directory.

Create a working directory that matches the worker's normal layout, then extract the zip into the `Debug` directory:

```text
C:\tools\dwsim_worker\
|-- dwsim.config.json
\-- DwsimWorker\
    \-- bin\
        \-- Debug\
            |-- DwsimWorker.dll
            |-- property_packages.toml
            |-- Tomlyn.dll
            |-- Newtonsoft.Json.dll
            \-- ...
```

#### Step 3: Create `dwsim.config.json`

Start from [`dwsim.config.json.sample`](dwsim.config.json.sample) in this repository and create `C:\tools\dwsim_worker\dwsim.config.json`.

If you have a repo checkout available, you can copy the sample and edit it:

```cmd
copy mcp_service\dwsim_worker\dwsim.config.json.sample C:\tools\dwsim_worker\dwsim.config.json
```

At minimum, set `dwsim_path` to your compatible DWSIM binaries directory:

```json
{
  "dwsim_path": "C:/path/to/DWSIM/bin",
  "msbuild_path": null
}
```

#### Step 4: Point the Python server at the extracted worker

Set `DWSIM_WORKER_DLL` to the extracted `DwsimWorker.dll`:

```cmd
set DWSIM_WORKER_DLL=C:\tools\dwsim_worker\DwsimWorker\bin\Debug\DwsimWorker.dll
```

In PowerShell, use:

```powershell
$env:DWSIM_WORKER_DLL = "C:\tools\dwsim_worker\DwsimWorker\bin\Debug\DwsimWorker.dll"
```

#### Step 5: Verify and run

```cmd
dwsim-mcp doctor
dwsim-mcp run
```

## Building from source

This section preserves the contributor/source-build workflow for this repository.

### Prerequisites

1. **Visual Studio 2019 or later** (or Visual Studio Build Tools)
   - With .NET desktop development workload
   - .NET Framework 4.8 SDK

2. **DWSIM Build Output**
   - You need access to a built copy of DWSIM (typically from the sibling `dwsim` repository)
   - DWSIM should be built in Debug configuration (x64)

### Quick Setup (New Machine)

#### Step 1: Configure DWSIM Path

Create `dwsim.config.json` from the sample template:

```cmd
cd mcp_service/dwsim_worker
copy dwsim.config.json.sample DwsimWorker\dwsim.config.json
```

Edit `DwsimWorker\dwsim.config.json` and set your DWSIM build path:

```json
{
  "dwsim_path": "d:/S/C#/dwsim/DWSIM/bin/x64/Debug",
  "msbuild_path": "D:/Apps/Microsoft Visual Studio/18/Professional/MSBuild/Current/Bin/MSBuild.exe"
}
```

**Configuration Options:**

- `dwsim_path` (required): Path to your DWSIM build output directory
- `msbuild_path` (optional): Path to `MSBuild.exe` if auto-detection doesn't find it

**Important:** Use forward slashes (`/`) in paths, even on Windows.

**MSBuild Auto-Detection:**

The build script automatically detects MSBuild using multiple strategies:

1. Checks `msbuild_path` in config (if specified)
2. Uses `vswhere.exe` for VS 2017+ installations
3. Checks common installation paths (Program Files)
4. Scans Windows Registry for VS installations
5. Searches `C:` and `D:` drives for Visual Studio folders

If your Visual Studio is in a non-standard location (for example `D:\Apps`), the auto-detection should still find it via registry or filesystem search. However, you can explicitly set `msbuild_path` in the config for faster builds.

#### Step 2: Run Setup Script

The setup script will copy DWSIM binaries to the local `dwsim_binaries` folder:

```cmd
cd mcp_service/dwsim_worker
setup-dwsim-binaries.bat
```

This creates `dwsim_binaries/x64/Debug/` and copies all necessary DWSIM assemblies.

#### Step 3: Build the Project

Now build using the standard build script (which automatically runs setup):

```cmd
cd "D:\S\C#\dwsim_interop_services\mcp_service\dwsim_worker"
build.bat
```

The build script now:

1. Automatically runs `setup-dwsim-binaries.bat` first
2. Then builds the solution with MSBuild

#### Step 4: Run Tests

After building, run tests using Visual Studio Test Console:

```cmd
"D:\Apps\Microsoft Visual Studio\18\Professional\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" "D:\S\C#\dwsim_interop_services\mcp_service\dwsim_worker\DwsimWorker.Tests\bin\Debug\DwsimWorker.Tests.dll" --logger:"console;verbosity=detailed"
```

### What Gets Copied

The setup script copies the entire DWSIM build output, including:

#### Core DWSIM Assemblies

- `DWSIM.Interfaces.dll` - Core interfaces
- `DWSIM.Thermodynamics.dll` - Thermodynamic calculations
- `DWSIM.SharedClasses.dll` - Shared utilities
- `DWSIM.UnitOperations.dll` - Unit operation implementations
- `CapeOpen.dll` - CAPE-OPEN interface definitions

#### Additional Dependencies

- All DWSIM property packages (GERG2008, PCSAFT, etc.)
- All DWSIM math libraries
- All third-party dependencies (CoolProp, etc.)
- Database link libraries
- Configuration files

The script uses `robocopy` for efficient incremental copying. Only changed files are copied on subsequent runs.

### Directory Structure

After setup, your directory structure should look like:

```text
mcp_service/dwsim_worker/
|-- build.bat
|-- setup-dwsim-binaries.bat
|-- DwsimWorker/
|   |-- dwsim.config.json
|   \-- ...
|-- dwsim_binaries/
|   \-- x64/
|       \-- Debug/
|           |-- DWSIM.Interfaces.dll
|           |-- DWSIM.Thermodynamics.dll
|           \-- ... (all DWSIM files)
\-- ...
```

### Troubleshooting

#### Error: "Could not find MSBuild.exe"

The build script couldn't locate MSBuild automatically.

**Solutions:**

1. Verify Visual Studio is installed with .NET desktop development workload
2. Add `msbuild_path` to `DwsimWorker\dwsim.config.json`:

   ```json
   {
     "dwsim_path": "...",
     "msbuild_path": "D:/Apps/Microsoft Visual Studio/18/Professional/MSBuild/Current/Bin/MSBuild.exe"
   }
   ```

3. Run the detection script manually to see what it finds:

   ```cmd
   powershell -ExecutionPolicy Bypass -File find-msbuild.ps1
   ```

#### Error: "Configuration file not found"

You need to create `dwsim.config.json`:

```cmd
copy dwsim.config.json.sample DwsimWorker\dwsim.config.json
```

Then edit it with your DWSIM path.

#### Error: "Source DWSIM directory does not exist"

The path in `dwsim.config.json` is incorrect or DWSIM hasn't been built yet:

1. Verify the path points to your DWSIM build output
2. Build DWSIM first if you haven't already
3. Use forward slashes in the path, for example `d:/S/C#/dwsim/DWSIM/bin/x64/Debug`

#### Error: "Some critical DWSIM assemblies are missing"

Your DWSIM build may be incomplete:

1. Rebuild DWSIM in Debug configuration (x64)
2. Verify these files exist in your DWSIM build output:
   - `DWSIM.Interfaces.dll`
   - `DWSIM.Thermodynamics.dll`
   - `DWSIM.SharedClasses.dll`
   - `DWSIM.UnitOperations.dll`

#### Tests Fail with "DWSIM assemblies not found"

The `dwsim_binaries` folder wasn't created properly:

1. Run `setup-dwsim-binaries.bat` manually
2. Verify `dwsim_binaries/x64/Debug/` was created and contains DLLs
3. Check the console output for errors

#### Robocopy Exit Codes

The setup script uses `robocopy`, which has unusual exit codes:

- `0-7`: Success (various combinations of copied/extra/mismatched files)
- `8+`: Errors

Don't be alarmed by exit codes `1-7`; these indicate successful copying.

### Why This Approach?

#### Benefits

1. **Machine Independence**: Each developer configures their own DWSIM path via `dwsim.config.json` (gitignored)
2. **No Environment Variables**: No need to set `DWSIM_PATH` system-wide
3. **Consistent Tests**: Tests use the local `dwsim_binaries` copy, not external paths
4. **Fast Incremental Updates**: `robocopy` only copies changed files
5. **CI/CD Ready**: Can be adapted for build servers

#### Files Gitignored

The following are in `.gitignore`:

- `dwsim.config.json` - Machine-specific DWSIM path configuration
- `dwsim_binaries/` - Local copy of DWSIM assemblies (large binary files)

### Advanced Configuration

#### Using a Different DWSIM Build

If you want to test with a different DWSIM version:

1. Update `dwsim.config.json` with the new path
2. Run `setup-dwsim-binaries.bat` to copy new binaries
3. Rebuild and test

#### Manual Copy (Without Build)

If you just want to update DWSIM binaries without building:

```cmd
setup-dwsim-binaries.bat
```

#### Clean Setup

To completely reset:

```cmd
rem Delete local binaries
rmdir /s /q dwsim_binaries

rem Re-run setup
setup-dwsim-binaries.bat

rem Rebuild
build.bat
```

### Integration with Build Script

The `build.bat` script now automatically runs `setup-dwsim-binaries.bat` as **Step 1** before building. This means:

- First-time setup is automatic
- DWSIM binaries are kept in sync with your DWSIM build
- You can always just run `build.bat` and everything works

To skip the setup step (if you know binaries are current), you can still build manually:

```cmd
msbuild DwsimWorker.sln /p:Configuration=Debug /t:Restore,Build
```

But for most cases, just use `build.bat`. It's smart enough to only copy changed files.
