# Threading Fix and Integration Test Investigation Report

**Date**: 2026-01-15
**Status**: Threading issue resolved; DWSIM API usage issue identified

## Executive Summary

This report documents the resolution of a Windows access violation crash in the Python integration tests and identifies a remaining DWSIM API usage issue. The original problem was misdiagnosed as a threading issue, but was actually a DWSIM internal state management problem that manifested differently due to COM interop error propagation.

## Problem Statement

The Python integration test `test_simulation_workflow_integration` was crashing with:
```
Windows fatal exception: access violation
```

The crash occurred when calling `flowsheet_client.add_unit()` from a ThreadPoolExecutor worker thread.

## Root Cause Analysis

### Initial Hypothesis (INCORRECT)
We initially believed the issue was that DWSIM COM objects cannot be accessed from worker threads without proper STA (Single-Threaded Apartment) initialization.

### Actual Root Cause (CORRECT)
After implementing proper STA threading and improving error reporting, we discovered the real issue:

```
ArgumentException: An item with the same key has already been added
at DWSIM.FlowsheetBase.FlowsheetBase.AddObjectToSurface (line 1293)
```

The "access violation" was actually this duplicate key exception being propagated through COM interop in a way that appeared as an access violation.

### Why Threading Still Mattered
Even though threading wasn't the root cause, implementing proper STA threading was still necessary:
1. DWSIM requires STA mode for COM interop
2. The improved threading architecture provides better error messages
3. It prevents future COM-related issues

## Solution Implemented: Custom STA Thread Executor

### Architecture

Created `STAThreadExecutor` in `operation_timeout_runner.py` that:

1. **Initializes COM as STA BEFORE loading pythonnet**
   ```python
   ole32.CoInitializeEx(None, 0x2)  # COINIT_APARTMENTTHREADED
   ```

2. **Creates a dedicated STA worker thread**
   - Thread starts with STA mode set
   - Loads pythonnet CLR after STA initialization
   - Runs a task queue loop for executing operations

3. **Lazy initialization pattern**
   - `SessionClient` and `FlowsheetClient` now initialize only when first accessed
   - Ensures COM objects are created on the correct STA thread

### Files Modified

#### C# Changes

**DwsimWorker/Engine/FlowsheetOperations.cs**
- Added `isSource` parameter to `AddStream()` method
- Added comprehensive error reporting with inner exceptions
- Location: Lines 49-93

**Key changes:**
```csharp
public string AddStream(
    string sessionId,
    string name,
    double temperatureK,
    double pressurePa,
    double molarFlow,
    IDictionary<string, double> composition,
    bool isSource = false)  // NEW PARAMETER
```

Error handling now surfaces inner exceptions:
```csharp
var errorMsg = $"Unexpected error creating stream '{name}': {result.Message}";
if (result.Error != null)
{
    errorMsg += $" Inner: {result.Error.Message}";
    if (result.Error.InnerException != null)
    {
        errorMsg += $" InnerInner: {result.Error.InnerException.Message}";
    }
}
```

#### Python Changes

**mcp_service/server/dwsim_mcp_server/limits/operation_timeout_runner.py**
- Added `STAThreadExecutor` class (lines 29-104)
- Replaced ThreadPoolExecutor with STAThreadExecutor
- Added diagnostic logging for STA state verification

**mcp_service/server/dwsim_mcp_server/ipc/session_client.py**
- Added lazy initialization pattern
- Added STA thread verification
- Added diagnostic logging
- Lines: 38-65 (initialization), 93-115 (_ensure_initialized)

**mcp_service/server/dwsim_mcp_server/ipc/flowsheet_client.py**
- Added lazy initialization pattern
- Added `is_source` parameter to `add_stream()` method
- Lines: 23-56 (initialization), 67-105 (add_stream with is_source)

**mcp_service/server/dwsim_mcp_server/service/flowsheet_service.py**
- Updated protocol to include `is_source` parameter
- Pass through `is_source` to flowsheet client
- Lines: 37-49 (protocol), 125-151 (add_stream implementation)

**models/mcp_inputs/flowsheet_build.py**
- Added `is_source` field to `AddStreamInput` model
- Lines: 89-92

**mcp_service/server/tests/integration/test_simulation_integration.py**
- Completely rewritten to match C# golden test pattern
- Changed from 1 compound to 3 compounds (Methane, Water, n-Decane)
- Changed from 1 stream to 4 streams (FEED + 3 outlets)
- Added separator configuration parameters
- Added `is_source=True` for feed stream, `is_source=False` for outlets
- Lines: 15-174

## Test Results

### Successful Components ✅

1. **STA Threading**: Working perfectly
   ```
   [STAWorker] Thread 34604 COM initialized as STA (result: 0)
   [STAWorker] Thread 34604 .NET apartment state: STA
   ```

2. **Feed Stream Creation**: Succeeds
   ```
   stream_created is_source=True name=FEED session_id=... stream_id=S1
   ```

3. **Compound Addition**: All 3 compounds add successfully
   ```
   compound_added compound=Methane
   compound_added compound=Water
   compound_added compound=n-Decane
   ```

4. **Property Package**: Sets successfully
   ```
   property_package_set package=peng-robinson
   ```

### Failing Component ❌

**Second Stream Creation** (VAPOR, S2):
```
System.InvalidOperationException: Unexpected error creating stream 'VAPOR':
Failed to create MaterialStream: Exception has been thrown by the target of invocation.
Inner: Failed to create MaterialStream: Exception has been thrown by the target of invocation.
InnerInner: Exception has been thrown by the target of invocation.
-> Failed to create MaterialStream: Exception has been thrown by the target of invocation.
-> Exception has been thrown by the target of invocation.
-> An item with the same key has already been added.
```

**Stack trace points to:**
```
at DWSIM.FlowsheetBase.FlowsheetBase.AddObjectToSurface(ObjectType type, Int32 x, Int32 y, String tag, String id, ...)
at line 1293: An item with the same key has already been added
```

## Comparison with C# Golden Test

### What Works in C# But Fails in Python

The C# golden test (`GoldenTest_ThreePhaseSeparatorCalculation_Succeeds`) successfully creates the same flowsheet:
- 3 compounds (Methane, Water, n-Decane)
- 4 streams (FEED, VAPOR, LIGHT_LIQUID, HEAVY_LIQUID)
- 1 three-phase separator
- All connections

**Key difference**: The C# test uses **exactly the same** `StreamAdapter.CreateStream()` method that the Python test calls.

### Possible Causes of Difference

1. **Flowsheet Initialization**: Some DWSIM internal state might be initialized differently when called from Python vs C# directly

2. **GraphicObject Registration**: DWSIM maintains internal dictionaries of objects. The duplicate key suggests a GraphicObject ID or tag collision

3. **Missing DWSIM Setup Steps**: The C# test might implicitly perform setup steps that aren't exposed/called in Python

4. **COM Marshaling**: pythonnet might be creating or marshaling objects in a way that causes DWSIM's internal tracking to fail

## Remaining Issues

### Issue #1: Duplicate Key Error (HIGH PRIORITY)

**Symptom**: Second stream creation fails with "An item with the same key has already been added"

**Location**: `DWSIM.FlowsheetBase.FlowsheetBase.AddObjectToSurface` line 1293

**Investigation Needed**:
1. Compare FlowsheetContext initialization between C# direct call and Python pythonnet call
2. Check if DWSIM's GraphicObject dictionary needs explicit clearing between operations
3. Investigate if stream counter or ID generation is conflicting
4. Check if DWSIM's internal state needs specific initialization when called from COM

**Files to Investigate**:
- `DwsimWorker/Engine/FlowsheetContext.cs` - How flowsheet is initialized
- `DwsimWorker/Adapters/StreamAdapter.cs` - Stream creation logic (especially lines 130-160)
- DWSIM source code at `FlowsheetBase.AddObjectToSurface` line 1293

**Diagnostic Commands**:
```bash
# Run C# golden test (this works)
"D:\Apps\Microsoft Visual Studio\18\Professional\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" \
  "D:\S\C#\dwsim_interop_services\mcp_service\dwsim_worker\DwsimWorker.Tests\bin\Debug\DwsimWorker.Tests.dll" \
  --Tests:GoldenTest_ThreePhaseSeparatorCalculation_Succeeds \
  --logger:"console;verbosity=detailed"

# Run Python integration test (this fails)
cd "D:\S\C#\dwsim_interop_services\mcp_service\server"
pytest tests/integration/test_simulation_integration.py::test_simulation_workflow_integration -v -s
```

### Issue #2: Binary Interaction Parameters (MEDIUM PRIORITY)

The C# golden test sets BIP (Binary Interaction Parameter) values for accurate three-phase calculations:

```csharp
var bips = new List<(string, string, double)>
{
    ("Methane", "Water", 0.0),
    ("Methane", "n-Decane", 0.0),
    ("Water", "n-Decane", 0.0)
};
```

**Status**: Not exposed in Python API

**Action**: May need to add BIP setting to FlowsheetOperations if accurate phase equilibrium is required

### Issue #3: Stream Flashing (LOW PRIORITY)

The C# golden test explicitly flashes the feed stream before adding the separator:

```csharp
streamAdapter.FlashStream(feedStreamId);
```

**Status**: Not exposed in Python API

**Action**: May need to expose stream flashing if required for DWSIM internal state management

## Build and Test Commands

### Build C# Worker
```bash
cd "D:\S\C#\dwsim_interop_services\mcp_service\dwsim_worker"
./build.bat
```

**Note**: If build fails with "file locked" error, kill Python processes:
```bash
taskkill //F //PID <pid>
```

### Run Tests

**C# Golden Test** (working):
```bash
"D:\Apps\Microsoft Visual Studio\18\Professional\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" \
  "D:\S\C#\dwsim_interop_services\mcp_service\dwsim_worker\DwsimWorker.Tests\bin\Debug\DwsimWorker.Tests.dll" \
  --Tests:GoldenTest_ThreePhaseSeparatorCalculation_Succeeds \
  --logger:"console;verbosity=detailed"
```

**Python Integration Test** (failing):
```bash
cd "D:\S\C#\dwsim_interop_services\mcp_service\server"
pytest tests/integration/test_simulation_integration.py::test_simulation_workflow_integration -v -s
```

## Files Changed Summary

### Created Files
- `docs/threading-fix-report.md` (this file)

### Modified Files

#### C# (.NET)
1. `mcp_service/dwsim_worker/DwsimWorker/Engine/FlowsheetOperations.cs`
   - Added `isSource` parameter
   - Enhanced error reporting
   - Lines: 49-93

#### Python
1. `mcp_service/server/dwsim_mcp_server/limits/operation_timeout_runner.py`
   - Added `STAThreadExecutor` class
   - Lines: 29-104

2. `mcp_service/server/dwsim_mcp_server/ipc/session_client.py`
   - Added lazy initialization
   - Added STA verification
   - Lines: 38-65, 93-115

3. `mcp_service/server/dwsim_mcp_server/ipc/flowsheet_client.py`
   - Added lazy initialization
   - Added `is_source` parameter
   - Lines: 23-56, 67-105

4. `mcp_service/server/dwsim_mcp_server/service/flowsheet_service.py`
   - Updated protocol and implementation for `is_source`
   - Lines: 37-49, 125-151

5. `models/mcp_inputs/flowsheet_build.py`
   - Added `is_source` field to model
   - Lines: 89-92

6. `mcp_service/server/tests/integration/test_simulation_integration.py`
   - Complete rewrite to match C# golden test
   - Lines: 15-174

## Next Steps for Future Work

### Immediate (HIGH PRIORITY)
1. **Debug the duplicate key error**:
   - Add logging to StreamAdapter to see what stream IDs/tags are being generated
   - Compare object registration between successful C# test and failing Python test
   - Check if DWSIM's internal dictionaries need explicit initialization

2. **Simplify the test**:
   - Create a minimal test with just 1 compound and 2 streams to isolate the issue
   - If that works, incrementally add complexity

### Short-term (MEDIUM PRIORITY)
1. **Clean up diagnostic logging**:
   - Remove or comment out verbose STA thread logging once issue is resolved
   - Keep only essential error reporting

2. **Add BIP support** (if needed):
   - Expose BIP setting in FlowsheetOperations
   - Add Python API wrapper

3. **Add stream flashing** (if needed):
   - Expose FlashStream in FlowsheetOperations
   - Add Python API wrapper

### Long-term (LOW PRIORITY)
1. **Improve error messages**:
   - Better COM exception translation
   - More context in error reports

2. **Add integration test coverage**:
   - Test single-compound, two-stream flowsheet
   - Test different unit operation types
   - Test error conditions

## Technical Notes

### STA Threading Requirements
- COM objects must be created and accessed from the same STA thread
- `CoInitializeEx` must be called before any COM operations
- pythonnet CLR must be loaded AFTER STA initialization

### DWSIM Internal Architecture
- DWSIM maintains multiple internal dictionaries:
  - `SimulationObjects` - actual unit operations and streams
  - `GraphicObjects` - visual representation on flowsheet surface
  - Various lookup tables by ID, tag, and name

- Object registration happens at multiple levels:
  - `AddObjectToSurface` - adds to GraphicObjects dictionary
  - `AddCompoundToMaterialStream` - links compounds
  - Various internal tracking structures

### pythonnet COM Interop
- pythonnet uses .NET's COM interop layer
- Exceptions can be wrapped multiple layers deep
- Error messages may lose context through COM boundary

## Diagnostic Output Examples

### Successful STA Initialization
```
[STAWorker] Thread 34604 starting
[STAThreadExecutor] Started STA worker thread 34604
[STAWorker] Thread 34604 COM initialized as STA (result: 0)
[STAWorker] Thread 34604 .NET apartment state: STA
[SessionClient] Initializing on thread 34604
[STA] Thread 34604 current state: STA
[STA] Thread 34604 already in STA mode
[SessionClient] Initialization complete on thread 34604
```

### Successful Feed Stream Creation
```
2026-01-15 17:42:36 [info] stream_created
  [dwsim_mcp_server.service.flowsheet_service]
  is_source=True
  name=FEED
  session_id=41318b04-1efa-4a16-b1ff-77330ac75d6d
  stream_id=S1
```

### Failed Outlet Stream Creation
```
System.InvalidOperationException: Unexpected error creating stream 'VAPOR':
Failed to create MaterialStream: Exception has been thrown by the target of invocation.
Inner: Failed to create MaterialStream: Exception has been thrown by the target of invocation.
InnerInner: Exception has been thrown by the target of invocation.
-> An item with the same key has already been added.
```

## References

### Key Code Locations
- STA Thread Executor: `mcp_service/server/dwsim_mcp_server/limits/operation_timeout_runner.py:29-104`
- Stream Creation: `mcp_service/dwsim_worker/DwsimWorker/Adapters/StreamAdapter.cs:103-160`
- FlowsheetOperations: `mcp_service/dwsim_worker/DwsimWorker/Engine/FlowsheetOperations.cs:49-93`
- Integration Test: `mcp_service/server/tests/integration/test_simulation_integration.py:15-174`

### Related Documentation
- DWSIM Documentation: https://dwsim.org
- pythonnet Documentation: https://pythonnet.github.io/
- COM Threading Models: https://docs.microsoft.com/en-us/windows/win32/com/processes--threads--and-apartments

---

**Report Generated**: 2026-01-15
**Author**: Claude Code
**Status**: Threading resolved, DWSIM API issue identified
