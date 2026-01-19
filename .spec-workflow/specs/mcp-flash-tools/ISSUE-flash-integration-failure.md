# Flash Integration Test Failure Investigation

## Status: IN PROGRESS - NEEDS CONTINUATION

## Problem Summary

The flash integration test `test_flash_tp_single_phase` fails with `converged=False` and `phase_count=0`. The C# ThermodynamicsAdapter is returning a failure result, but the exact failure point is not yet determined.

## Test Output

```
2026-01-19 22:56:08 [info] flash_tp_completed converged=False phase_count=0 session_id=21a17516-2582-469a-a38c-ec596dfd5ed8
assert result["converged"] is True
E   assert False is True
```

## Test Setup (Working)

The test fixture correctly:
1. Creates a session
2. Sets property package: `peng-robinson` (success)
3. Adds compound: `Methane` (success)

## Architecture Overview

The flash flow is:
1. Python `ThermodynamicsService.flash_tp()`
2. Creates `ThermodynamicsAdapter` via pythonnet
3. `ThermodynamicsAdapter.FlashTP()` calls `RunFlash()`
4. `RunFlash()` does:
   - Validates inputs
   - Creates `StreamProperties` with T, P, composition
   - Calls `StreamAdapter.CreateStream()` with `isSource=true`
   - Calls `StreamAdapter.FlashStream()`
   - Converts result to `FlashResultDto`

## Key Files

### C# Side
- `ThermodynamicsAdapter.cs` - Main adapter (line 80-163 is `RunFlash`)
- `StreamAdapter.cs` - Creates streams and flashes them
- `CapeOpenConverter.cs` - Converts DWSIM objects to DTOs
- `FlowsheetContext.cs` - Manages session state including compounds

### Python Side
- `thermodynamics_service.py` - Python service layer
- `test_flash_integration.py` - Integration tests

## Failure Points in RunFlash (ThermodynamicsAdapter.cs)

The code returns `CreateFailureResult()` (which sets `Converged=false, Phases=[]`) at these points:

1. **Line 91-93**: Input validation fails
2. **Line 114-119**: `StreamAdapter.CreateStream()` fails
3. **Line 125-131**: `StreamAdapter.FlashStream()` fails
4. **Line 136-142**: `_context.GetStream()` returns null

## Diagnostic Logging Added

Diagnostic logging was added to `ThermodynamicsAdapter.cs`:

```csharp
// At start of RunFlash:
_logger.Information("[ThermodynamicsAdapter] RunFlash starting: type={CalculationType}, compounds=[{Compounds}], composition=[{Composition}], T={Temperature}K, P={Pressure}Pa", ...);
_logger.Information("[ThermodynamicsAdapter] Context has {Count} compounds: [{Compounds}]", contextCompounds.Count, ...);
_logger.Information("[ThermodynamicsAdapter] Property package: {PropertyPackage}", pp != null ? pp.GetType().Name : "NULL");

// At each step:
_logger.Information("[ThermodynamicsAdapter] Creating stream with ID={StreamId}", tempStreamId);
_logger.Information("[ThermodynamicsAdapter] Flashing stream {StreamId}", actualStreamId);
// etc.
```

**Problem**: The Serilog logger doesn't have a console sink, so these logs don't appear. Attempted to add `.WriteTo.Console()` but got error: `'LoggerSinkConfiguration' object has no attribute 'Console'` - the Serilog.Sinks.Console package isn't loaded.

## Hypotheses to Test

### Hypothesis 1: Compound Names Mismatch
`CreateStreamProperties` receives `compounds[]` array but **doesn't use it**. Only `composition[]` is used to create the `Composition` model. Then `StreamAdapter.ApplyComposition` relies on `_context.GetCompounds()` to map fractions to compounds.

**To verify**: Check if `_context.GetCompounds()` returns `["Methane"]` when the adapter runs.

### Hypothesis 2: Context Not Shared
The test adds compound via `FlowsheetOperations.AddCompound` which uses a context retrieved from `SessionManager.GetSession(guid)`. The `ThermodynamicsAdapter` also retrieves context via the same path. They should be the same instance.

**To verify**: Add logging to confirm context identity.

### Hypothesis 3: Property Package Not Set on Stream
The stream might not have the property package assigned, causing flash to fail silently.

**To verify**: Check `TrySetStreamPropertyPackage` in StreamAdapter is succeeding.

### Hypothesis 4: Flash Succeeds But Phases Empty
The `CapeOpenConverter.BuildPhaseDtos()` (line 203-215) only creates a single "Overall" phase. But if `ToMaterialStreamDto` fails to read properties, it might return empty phases.

**To verify**: Add logging in converter.

## Comparison: Working Golden Test

The C# golden test `GoldenTest_ThreePhaseSeparatorCalculation_Succeeds` works correctly. Key differences:

1. Golden test loads a pre-built DWSIM file with all streams/units already configured
2. Golden test runs full flowsheet calculation, not standalone flash
3. Golden test uses `FlowsheetOperations.AddStream()` directly, not `ThermodynamicsAdapter`

## Next Steps for Continuation

1. **Add Python-side logging** (partially done in `thermodynamics_service.py`) to see what DTO comes back

2. **Test with debug script**: The file `mcp_service/server/debug_flash.py` was created but had import issues. Fix PYTHONPATH and run it to get detailed C# Serilog output.

3. **Check context state**: Before flash, verify:
   - `context.GetCompounds()` returns `["Methane"]`
   - `context.GetPropertyPackage()` is not null
   - The stream is created and findable

4. **Add try-catch with Python logging**: Wrap the adapter call in try-except to catch any .NET exceptions

5. **Run C# unit test**: The `ThermodynamicsAdapter` C# tests pass (validation logic), but they don't test actual DWSIM flash execution. Add a C# test that does a real flash.

## Files Modified (Uncommitted)

- `ThermodynamicsAdapter.cs` - Added diagnostic logging
- `thermodynamics_service.py` - Added Python-side logging (partial)

## Commands for Testing

```bash
# Build C#
cd "D:\S\C#\dwsim_interop_services\mcp_service\dwsim_worker" && ./build.bat 2>&1

# Run flash integration test
cd "D:\S\C#\dwsim_interop_services\mcp_service\server"
.\.venv\Scripts\python.exe -m pytest tests/integration/test_flash_integration.py::test_flash_tp_single_phase -v --no-cov 2>&1
```
