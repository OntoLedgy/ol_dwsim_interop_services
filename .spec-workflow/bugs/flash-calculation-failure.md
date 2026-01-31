# Bug Report: DWSIM MCP Flash Calculation Failure

**Status:** ✅ Fixed
**Priority:** Critical
**Reported:** 2026-01-31
**Fixed:** 2026-01-31
**Server:** 192.168.0.30:8000

## Resolution

**Root Cause:** The DWSIM Inspector component was throwing `IndexOutOfRangeException` when calling `DWSIM.Inspector.Host.CheckAndAdd` during headless flash calculations. The Inspector was not disabled in automation mode.

**Fix Applied:** Added `ConfigureGlobalSettingsForHeadlessMode()` helper in `StreamAdapter.cs` that configures DWSIM's `GlobalSettings` before stream calculations:
- `AutomationMode = true`
- `InspectorEnabled = false` (prevents the Inspector array index error)
- `CalculatorActivated = true`
- `CalculatorBusy = false`

**Commits:**
- `ca4e3e5` - fix(flash): configure GlobalSettings for headless mode before stream Calculate
- `fd312e6` - fix(thermo): validate compound names and fix stream composition in flash tests

**Verification:** Golden test passed via MCP on 2026-01-31:
- Three-phase separator simulation converged in 391ms
- Mass balance error: 0%
- All stream results computed correctly

---

## Original Summary

The DWSIM MCP server's `flash_stream` tool fails with `IndexOutOfRangeException` for all flash calculations. This prevents any simulation from running successfully.

## Symptoms

### Error Message
```
Failed to flash stream 'S1': Flash calculation failed for stream 'S1': Index was outside the bounds of the array.
Inner: Index was outside the bounds of the array. -> Index was outside the bounds of the array.
```

### Stack Trace Location
```
at DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.NestedLoops.Flash_PT(Double[] Vz, Double P, Double T, PropertyPackage PP, Boolean ReuseKI, Double[] PrevKi)
in D:\S\C#\dwsim\DWSIM.Thermodynamics\FlashAlgorithms\NestedLoops.vb:line 181
```

### Affected Tools
- `flash_stream` - Always fails with IndexOutOfRangeException
- `flash_tp` - Returns `converged: false` with empty phases
- `run` - Returns "No streams available" (because flash fails first)

### Working Tools
- `create_session` ✅
- `add_compound` ✅
- `set_property_package` ✅
- `set_binary_interaction_parameter` ✅
- `add_stream` ✅
- `add_unit` ✅
- `connect` ✅
- `list_objects` ✅

## Root Cause Analysis

### Primary Issue: Missing DWSIM Patch

The DWSIM binaries on the server are missing a critical patch for headless operation. The `DWSIM.UI.Desktop.Shared.dll` does not contain the `UpdateInterface()` fix.

### Why This Causes the Error

1. When `flash_stream` is called, DWSIM's solver runs calculations
2. During calculation, DWSIM calls `UpdateInterface()` to update UI progress
3. In headless mode (no GUI), `Application.Instance` is null
4. This throws `NullReferenceException` which interrupts the calculation
5. The interrupted calculation leaves arrays uninitialized
6. Subsequent array access throws `IndexOutOfRangeException`

### Required Patch

The fix is documented in `.claude/bugs/calculation-errors/analysis.md` (lines 664-679).

Location: `DWSIM.UI.Desktop.Shared/Flowsheet/Flowsheet.cs`

```csharp
public override void UpdateInterface()
{
    // Skip UI updates in automation/headless mode
    if (GlobalSettings.Settings.AutomationMode)
        return;

    // Skip UI updates if Eto.Forms Application not initialized
    if (Application.Instance == null)
        return;

    Application.Instance.Invoke(() =>
    {
        if (FlowsheetForm != null) FlowsheetForm.Invalidate();
    });
}
```

This patch exists on the `fix/headless-updateinterface` branch of `https://github.com/OntoLedgy/dwsim`.

## Debugging Steps

### Step 1: Verify the DLL is Missing the Fix

Run this PowerShell command on the server:

```powershell
$dll = "C:\DwsimMcp\dwsim_interop_services\mcp_service\dwsim_worker\dwsim_binaries\x64\Debug\DWSIM.UI.Desktop.Shared.dll"
$content = [System.IO.File]::ReadAllBytes($dll)
$text = [System.Text.Encoding]::ASCII.GetString($content)
if ($text -match "AutomationMode") { "DLL HAS fix" } else { "DLL MISSING fix" }
```

**Expected result if broken:** `DLL MISSING fix`

### Step 2: Check DLL Timestamp

```powershell
Get-Item "C:\DwsimMcp\dwsim_interop_services\mcp_service\dwsim_worker\dwsim_binaries\x64\Debug\DWSIM.UI.Desktop.Shared.dll" | Select-Object Name, LastWriteTime, Length
```

### Step 3: Check if Patched DWSIM Source is Available

```powershell
# Check if OntoLedgy/dwsim repo exists locally
Get-ChildItem "D:\S\C#\dwsim" -ErrorAction SilentlyContinue

# If exists, check the branch
cd "D:\S\C#\dwsim"
git branch --show-current
git log --oneline -5
```

## Fix Options

### Option A: Rebuild from Patched DWSIM Source (Recommended)

If the patched DWSIM source exists locally:

```powershell
# 1. Navigate to DWSIM source
cd "D:\S\C#\dwsim"

# 2. Ensure on correct branch
git checkout fix/headless-updateinterface
git pull

# 3. Verify patch is present
Select-String -Path "DWSIM.UI.Desktop.Shared\Flowsheet\Flowsheet.cs" -Pattern "AutomationMode"

# 4. Find MSBuild
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1

# 5. Rebuild DWSIM (x64 Debug)
& $msbuild DWSIM.sln /p:Configuration=Debug /p:Platform=x64 /t:Rebuild

# 6. Copy patched DLL to binaries folder
$source = "bin\x64\Debug\DWSIM.UI.Desktop.Shared.dll"
$dest = "C:\DwsimMcp\dwsim_interop_services\mcp_service\dwsim_worker\dwsim_binaries\x64\Debug\"
Copy-Item $source $dest -Force

# 7. Verify fix is now present
$dll = "$dest\DWSIM.UI.Desktop.Shared.dll"
$content = [System.IO.File]::ReadAllBytes($dll)
$text = [System.Text.Encoding]::ASCII.GetString($content)
if ($text -match "AutomationMode") { "SUCCESS: DLL now has fix" } else { "FAILED: Still missing fix" }

# 8. Restart MCP server
Restart-Service DwsimMcpServer -ErrorAction SilentlyContinue
# Or if running manually, restart the process
```

### Option B: Re-download from Updated GitHub Release

If the GitHub release has been updated with patched binaries:

```powershell
# 1. Stop service
Stop-Service DwsimMcpServer -ErrorAction SilentlyContinue

# 2. Clear old binaries
Remove-Item -Recurse -Force "C:\DwsimMcp\dwsim_interop_services\mcp_service\dwsim_worker\dwsim_binaries" -ErrorAction SilentlyContinue

# 3. Re-download
cd "C:\DwsimMcp\dwsim_interop_services\mcp_service\server"
.\.venv\Scripts\dwsim-mcp.exe setup --download

# 4. Verify
$dll = "..\dwsim_worker\dwsim_binaries\x64\Debug\DWSIM.UI.Desktop.Shared.dll"
$content = [System.IO.File]::ReadAllBytes($dll)
$text = [System.Text.Encoding]::ASCII.GetString($content)
if ($text -match "AutomationMode") { "SUCCESS" } else { "FAILED - Release not updated" }

# 5. Restart
Start-Service DwsimMcpServer
```

### Option C: Manual Patch (If No Source Available)

If you cannot rebuild DWSIM, you may need to:
1. Get the patched DLL from a working machine
2. Transfer it to the server
3. Replace the existing DLL

## Verification

After applying the fix, test the MCP tools:

```python
# Test sequence
1. create_session
2. add_compound (Methane, Ethane, Propane)
3. set_property_package (Peng-Robinson)
4. add_stream (Feed, is_source=true, T=300K, P=500000Pa, flow=100mol/s)
5. flash_stream (Feed)  # THIS SHOULD NOW WORK
6. add_unit (separator)
7. connect streams
8. run simulation
```

Or use the MCP test endpoint if available.

## Related Documentation

- `.claude/bugs/calculation-errors/analysis.md` - Full root cause analysis
- `docs/threading-fix-report.md` - Threading and DLL mismatch issues
- `mcp_service/dwsim_worker/SETUP.md` - Build setup instructions

## File Locations

| File | Path |
|------|------|
| DWSIM binaries | `C:\DwsimMcp\dwsim_interop_services\mcp_service\dwsim_worker\dwsim_binaries\x64\Debug\` |
| DwsimWorker DLL | `C:\DwsimMcp\dwsim_interop_services\mcp_service\dwsim_worker\DwsimWorker\bin\Debug\` |
| Python server | `C:\DwsimMcp\dwsim_interop_services\mcp_service\server\` |
| DWSIM source (if exists) | `D:\S\C#\dwsim\` |

## Expected Outcome

After fix:
- `flash_stream` returns `{"flashed": true, ...}`
- `flash_tp` returns `{"converged": true, "phases": [...]}`
- `run` simulation completes with stream results
- Mass balance validates correctly
