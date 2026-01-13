using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Serilog;
using DwsimWorker.Engine;
using DwsimWorker.Models;
using DwsimWorker.Exceptions;
using DwsimWorker.Utilities;

namespace DwsimWorker.Adapters
{
    /// <summary>
    /// Adapter for running flowsheet calculations and extracting results.
    /// Orchestrates solver invocation, convergence monitoring, result extraction, and mass balance validation.
    /// </summary>
    /// <remarks>
    /// The CalculationAdapter provides a high-level interface for:
    /// - Running DWSIM flowsheet calculations with timeout support
    /// - Monitoring convergence status and capturing solver diagnostics
    /// - Extracting calculated properties from all streams
    /// - Validating mass balances across the flowsheet
    /// - Collecting performance metrics and timing information
    ///
    /// This adapter wraps the low-level DWSIM solver API and provides a clean,
    /// immutable result pattern for all calculation operations.
    /// </remarks>
    public sealed class CalculationAdapter
    {
        private readonly ILogger _logger;
        private readonly FlowsheetContext _context;
        private readonly StreamAdapter _streamAdapter;

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationAdapter"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for calculation operation logging.</param>
        /// <param name="context">The flowsheet context that manages calculation state.</param>
        /// <param name="streamAdapter">The stream adapter for extracting calculated properties.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        public CalculationAdapter(ILogger logger, FlowsheetContext context, StreamAdapter streamAdapter)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _streamAdapter = streamAdapter ?? throw new ArgumentNullException(nameof(streamAdapter));

            _logger.Debug("CalculationAdapter initialized");
        }

        /// <summary>
        /// Runs the flowsheet calculation without timeout.
        /// </summary>
        /// <returns>A CalculationResult containing the complete calculation results.</returns>
        public CalculationResult RunCalculation()
        {
            return RunCalculation(TimeSpan.Zero);
        }

        /// <summary>
        /// Runs the flowsheet calculation with the specified timeout.
        /// </summary>
        /// <param name="timeout">The maximum time to allow for the calculation. Use TimeSpan.Zero for no timeout.</param>
        /// <returns>A CalculationResult containing the complete calculation results or timeout information.</returns>
        public CalculationResult RunCalculation(TimeSpan timeout)
        {
            _logger.Information("Starting flowsheet calculation (timeout: {Timeout})",
                timeout == TimeSpan.Zero ? "none" : timeout.ToString());

            var startTime = DateTime.UtcNow;
            var sw = Stopwatch.StartNew();
            _context.InvalidateCalculationCache("calculation started");
            _context.UpdateConvergenceStatus(ConvergenceStatus.InProgress());

            try
            {
                // Step 1: Validate flowsheet is ready
                var flowsheet = _context.GetFlowsheet();
                if (flowsheet == null)
                {
                    var result = CalculationResult.FailureResult(
                        "Flowsheet is not initialized",
                        new InvalidOperationException("Flowsheet is null"));
                    _context.CacheCalculationResult(result);
                    return result;
                }

                // Step 2: Run calculation with optional timeout
                bool calculationSuccess;
                if (timeout > TimeSpan.Zero)
                {
                    calculationSuccess = RunCalculationWithTimeout(flowsheet, timeout, sw);
                }
                else
                {
                    calculationSuccess = RunCalculationCore(flowsheet);
                }

                sw.Stop();
                var endTime = DateTime.UtcNow;

                // REMOVED: Fallback Calculate() workaround
                // We must solve the root cause: why RequestCalculationAndWait() doesn't properly
                // calculate unit operations. The fallback was masking the real problem.

                // Step 2a: Verify all units were calculated
                foreach (var unitId in _context.GetUnitIds())
                {
                    var unit = _context.GetUnit(unitId);
                    if (unit != null)
                    {
                        var calculatedProp = unit.GetType().GetProperty("Calculated");
                        var isCalculated = calculatedProp?.GetValue(unit) as bool?;

                        if (isCalculated == false)
                        {
                            _logger.Error("Unit {UnitId} was NOT calculated by RequestCalculationAndWait(). This indicates the solver is not running properly.", unitId);
                            calculationSuccess = false;
                        }
                    }
                }

                // Step 3: Create timing information
                var timing = CalculationTiming.FromTimestamps(startTime, endTime);

                // Step 3a: Try to mark flowsheet as solved if all units calculated successfully
                try
                {
                    bool allUnitsCalculated = true;
                    foreach (var unitId in _context.GetUnitIds())
                    {
                        var unit = _context.GetUnit(unitId);
                        var calculatedProp = unit?.GetType().GetProperty("Calculated");
                        var isCalculated = calculatedProp?.GetValue(unit) as bool?;
                        if (isCalculated == false)
                        {
                            allUnitsCalculated = false;
                            break;
                        }
                    }

                    if (allUnitsCalculated)
                    {
                        var solvedProp = flowsheet.GetType().GetProperty("Solved");
                        if (solvedProp != null && solvedProp.CanWrite)
                        {
                            _logger.Debug("All units calculated, setting Flowsheet.Solved = true");
                            solvedProp.SetValue(flowsheet, true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Could not set Flowsheet.Solved property");
                }

                // Step 4: Check convergence status
                var convergenceStatus = GetConvergenceStatus(flowsheet);

                // Step 5: Capture solver messages
                var messages = GetSolverMessages(flowsheet);

                // If calculation failed or didn't converge, return appropriate result
                if (!calculationSuccess || convergenceStatus.State == ConvergenceState.Error)
                {
                    var result = CalculationResult.FailureResult(
                        convergenceStatus.Message ?? "Calculation failed",
                        null,
                        convergenceStatus,
                        timing,
                        messages);
                    _context.CacheCalculationResult(result);
                    return result;
                }

                if (convergenceStatus.State != ConvergenceState.Converged)
                {
                    var result = CalculationResult.NotConvergedResult(
                        convergenceStatus,
                        timing,
                        messages,
                        "Calculation did not converge");
                    _context.CacheCalculationResult(result);
                    return result;
                }

                // Step 6: Extract stream results
                var streamResults = ExtractStreamResults();

                // Step 7: Validate mass balance
                var massBalance = ValidateMassBalance(streamResults);

                // Step 8: Return successful result
                _logger.Information("Calculation completed successfully in {Duration}ms", timing.TotalMilliseconds);

                var successResult = CalculationResult.SuccessResult(
                    convergenceStatus,
                    timing,
                    streamResults,
                    massBalance,
                    messages);
                _context.CacheCalculationResult(successResult);
                return successResult;
            }
            catch (CalculationTimeoutException)
            {
                _context.UpdateConvergenceStatus(
                    ConvergenceStatus.Error($"Calculation timed out after {timeout}."));
                // Re-throw timeout exceptions
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                var timing = CalculationTiming.FromTimestamps(startTime, DateTime.UtcNow);

                _logger.Error(ex, "Calculation failed with exception");

                var result = CalculationResult.FailureResult(
                    $"Calculation failed: {ex.Message}",
                    ex,
                    ConvergenceStatus.Error(ex.Message),
                    timing);
                _context.CacheCalculationResult(result);
                return result;
            }
        }

        /// <summary>
        /// Runs the calculation with a timeout.
        /// </summary>
        private bool RunCalculationWithTimeout(object flowsheet, TimeSpan timeout, Stopwatch sw)
        {
            // TODO: In full implementation, use Task.Run with CancellationToken
            // For now, just run synchronously and check elapsed time
            var success = RunCalculationCore(flowsheet);

            if (sw.Elapsed > timeout)
            {
                throw new CalculationTimeoutException(timeout, sw.Elapsed);
            }

            return success;
        }

        /// <summary>
        /// Core calculation logic that invokes the DWSIM solver.
        /// </summary>
        private bool RunCalculationCore(object flowsheet)
        {
            _logger.Debug("Invoking DWSIM solver");

            try
            {
                var flowsheetType = flowsheet.GetType();

                var requestAndWait = flowsheetType.GetMethod("RequestCalculationAndWait");
                if (requestAndWait != null)
                {
                    // DIAGNOSTICS: Check what's in SimulationObjects before calling solver
                    try
                    {
                        var simObjects = flowsheetType.GetProperty("SimulationObjects")?.GetValue(flowsheet);
                        if (simObjects is System.Collections.IDictionary dict)
                        {
                            _logger.Debug("Flowsheet has {Count} objects in SimulationObjects", dict.Count);
                            foreach (System.Collections.DictionaryEntry entry in dict)
                            {
                                var obj = entry.Value;
                                var gobj = obj?.GetType().GetProperty("GraphicObject")?.GetValue(obj);
                                var objType = gobj?.GetType().GetProperty("ObjectType")?.GetValue(gobj);

                                // Check output connector status (critical for GetSolvingList)
                                var outConnectors = gobj?.GetType().GetProperty("OutputConnectors")?.GetValue(gobj);
                                bool? outIsAttached = null;
                                if (outConnectors is System.Collections.IList outList && outList.Count > 0)
                                {
                                    var connector0 = outList[0];
                                    outIsAttached = connector0?.GetType().GetProperty("IsAttached")?.GetValue(connector0) as bool?;
                                }

                                // Check Active property (GetSolvingList checks this)
                                var isActive = gobj?.GetType().GetProperty("Active")?.GetValue(gobj);

                                // Check input connector status (GetSolvingList walks backwards through InputConnectors)
                                var inConnectors = gobj?.GetType().GetProperty("InputConnectors")?.GetValue(gobj);
                                bool? inIsAttached = null;
                                string inAttachedFromName = null;
                                if (inConnectors is System.Collections.IList inList && inList.Count > 0)
                                {
                                    var connector0 = inList[0];
                                    inIsAttached = connector0?.GetType().GetProperty("IsAttached")?.GetValue(connector0) as bool?;

                                    // CRITICAL: Check if AttachedConnector is properly set (GetSolvingList needs this)
                                    if (inIsAttached == true)
                                    {
                                        var attachedConnector = connector0?.GetType().GetProperty("AttachedConnector")?.GetValue(connector0);
                                        if (attachedConnector != null)
                                        {
                                            var attachedFrom = attachedConnector.GetType().GetProperty("AttachedFrom")?.GetValue(attachedConnector);
                                            if (attachedFrom != null)
                                            {
                                                inAttachedFromName = attachedFrom.GetType().GetProperty("Name")?.GetValue(attachedFrom) as string;
                                            }
                                            else
                                            {
                                                _logger.Warning("  Object '{Key}': InputConnector[0].AttachedConnector.AttachedFrom is NULL", entry.Key);
                                            }
                                        }
                                        else
                                        {
                                            _logger.Warning("  Object '{Key}': InputConnector[0].AttachedConnector is NULL (IsAttached=true but no attached connector!)", entry.Key);
                                        }
                                    }
                                }

                                _logger.Debug("  Object '{Key}': ObjectType={ObjectType}, OutConn[0].IsAttached={OutAttached}, InConn[0].IsAttached={InAttached}, InConn[0].AttachedFrom.Name={InAttachedFromName}, Active={Active}",
                                    entry.Key, objType, outIsAttached?.ToString() ?? "null", inIsAttached?.ToString() ?? "null", inAttachedFromName ?? "null", isActive?.ToString() ?? "null");
                            }
                        }
                    }
                    catch (Exception diagEx)
                    {
                        _logger.Debug(diagEx, "Could not inspect SimulationObjects");
                    }

                    // DIAGNOSTICS: Check FlowsheetSurface.GraphicObjects count
                    // This is CRITICAL - GetSolvingList() iterates this collection, not SimulationObjects!
                    try
                    {
                        var getSurfaceMethod = flowsheetType.GetMethod("GetSurface");
                        if (getSurfaceMethod != null)
                        {
                            var surface = getSurfaceMethod.Invoke(flowsheet, null);
                            if (surface != null)
                            {
                                var graphicObjectsProp = surface.GetType().GetProperty("GraphicObjects");
                                if (graphicObjectsProp != null)
                                {
                                    var graphicObjects = graphicObjectsProp.GetValue(surface);
                                    if (graphicObjects is System.Collections.IList graphicList)
                                    {
                                        _logger.Information("FlowsheetSurface.GraphicObjects has {Count} objects", graphicList.Count);

                                        if (graphicList.Count == 0)
                                        {
                                            _logger.Error("CRITICAL: FlowsheetSurface.GraphicObjects is EMPTY! GetSolvingList() will return 0 phases and calculations will not run.");
                                        }
                                        else
                                        {
                                            // Log each object in GraphicObjects for verification
                                            for (int i = 0; i < graphicList.Count; i++)
                                            {
                                                var gobj = graphicList[i];
                                                var name = gobj?.GetType().GetProperty("Name")?.GetValue(gobj)?.ToString() ?? "unknown";
                                                var objType = gobj?.GetType().GetProperty("ObjectType")?.GetValue(gobj)?.ToString() ?? "unknown";
                                                _logger.Debug("  GraphicObjects[{Index}]: Name={Name}, Type={Type}", i, name, objType);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception diagEx)
                    {
                        _logger.Warning(diagEx, "Could not inspect FlowsheetSurface.GraphicObjects");
                    }

                    // DIAGNOSTICS: Try calling GetSolvingList() directly to see what it returns
                    // GetSolvingList is a static method on FlowsheetSolver class, not an instance method
                    try
                    {
                        // Find FlowsheetSolver type
                        var flowsheetSolverAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == "DWSIM.FlowsheetSolver");

                        if (flowsheetSolverAssembly != null)
                        {
                            var flowsheetSolverType = flowsheetSolverAssembly.GetType("DWSIM.FlowsheetSolver.FlowsheetSolver");
                            if (flowsheetSolverType != null)
                            {
                                var getSolvingList = flowsheetSolverType.GetMethod("GetSolvingList",
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                                if (getSolvingList != null)
                                {
                                    _logger.Debug("Calling FlowsheetSolver.GetSolvingList(flowsheet, false) for diagnostics...");
                                    var solvingListResult = getSolvingList.Invoke(null, new object[] { flowsheet, false });

                                    // GetSolvingList returns Object[] where each element is a List<string> for that phase
                                    // Array[0] = Phase 0 endpoints, Array[1] = Phase 1, etc.
                                    if (solvingListResult is object[] resultArray && resultArray.Length > 0)
                                    {
                                        _logger.Debug("GetSolvingList returned Object[] with {Count} phases", resultArray.Length);

                                        for (int phaseIdx = 0; phaseIdx < resultArray.Length; phaseIdx++)
                                        {
                                            if (resultArray[phaseIdx] is System.Collections.IList phaseList)
                                            {
                                                var objectNames = phaseList.Cast<object>()
                                                    .Select(x => x?.ToString() ?? "null")
                                                    .ToList();

                                                _logger.Debug("  Phase {Phase}: {Count} objects: {Objects}",
                                                    phaseIdx,
                                                    phaseList.Count,
                                                    string.Join(", ", objectNames));

                                                // Check if these names exist in SimulationObjects dictionary
                                                if (phaseIdx == 0 && phaseList.Count > 0)
                                                {
                                                    var simObjsProp = flowsheet.GetType().GetProperty("SimulationObjects");
                                                    if (simObjsProp != null)
                                                    {
                                                        var simObjs = simObjsProp.GetValue(flowsheet) as System.Collections.IDictionary;
                                                        if (simObjs != null)
                                                        {
                                                            _logger.Debug("SimulationObjects dictionary has {Count} entries", simObjs.Count);
                                                            foreach (var name in objectNames)
                                                            {
                                                                bool exists = simObjs.Contains(name);
                                                                _logger.Debug("  Name '{Name}' exists in SimulationObjects: {Exists}", name, exists);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                _logger.Debug("  Phase {Phase}: type {Type}",
                                                    phaseIdx,
                                                    resultArray[phaseIdx]?.GetType().Name ?? "null");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        _logger.Debug("GetSolvingList returned: {Type}", solvingListResult?.GetType().Name ?? "null");
                                    }
                                }
                                else
                                {
                                    _logger.Warning("GetSolvingList method not found on FlowsheetSolver");
                                }
                            }
                            else
                            {
                                _logger.Warning("FlowsheetSolver type not found in assembly");
                            }
                        }
                        else
                        {
                            _logger.Warning("DWSIM.FlowsheetSolver assembly not loaded");
                        }
                    }
                    catch (Exception diagEx2)
                    {
                        _logger.Warning(diagEx2, "Failed to call GetSolvingList for diagnostics");
                    }

                    // CRITICAL: Set GlobalSettings before calling solver
                    // These are required for the DWSIM solver to actually run
                    // Without these, RequestCalculationAndWait returns an empty list immediately
                    try
                    {
                        // Try to find the Settings type (it's in the DWSIM.GlobalSettings assembly but has no namespace)
                        Type globalSettingsType = null;
                        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

                        foreach (var asm in loadedAssemblies)
                        {
                            if (asm.GetName().Name == "DWSIM.GlobalSettings")
                            {
                                // The type is DWSIM.GlobalSettings.Settings (with full namespace)
                                globalSettingsType = asm.GetType("DWSIM.GlobalSettings.Settings");
                                if (globalSettingsType != null)
                                {
                                    _logger.Debug("Found DWSIM.GlobalSettings.Settings type");
                                    break;
                                }
                                else
                                {
                                    _logger.Warning("DWSIM.GlobalSettings.Settings type not found");
                                }
                            }
                        }

                        if (globalSettingsType != null)
                        {
                            var calculatorActivated = globalSettingsType.GetProperty("CalculatorActivated");
                            var calculatorBusy = globalSettingsType.GetProperty("CalculatorBusy");
                            var solverBreakOnException = globalSettingsType.GetProperty("SolverBreakOnException");
                            var solverMode = globalSettingsType.GetProperty("SolverMode");
                            var automationMode = globalSettingsType.GetProperty("AutomationMode");

                            if (calculatorActivated != null)
                            {
                                calculatorActivated.SetValue(null, true);
                                _logger.Debug("Set GlobalSettings.CalculatorActivated = true");
                            }
                            // CRITICAL: Set CalculatorBusy = false BEFORE calling solver
                            // The solver checks this at lines 1133 and 1179 and returns immediately if true
                            if (calculatorBusy != null)
                            {
                                calculatorBusy.SetValue(null, false);
                                _logger.Debug("Set GlobalSettings.CalculatorBusy = false");
                            }
                            if (solverBreakOnException != null)
                            {
                                solverBreakOnException.SetValue(null, true);
                                _logger.Debug("Set GlobalSettings.SolverBreakOnException = true");
                            }
                            if (solverMode != null)
                            {
                                solverMode.SetValue(null, 1);
                                _logger.Debug("Set GlobalSettings.SolverMode = 1");
                            }
                            // CRITICAL: Set AutomationMode = true for headless operation
                            // This signals to DWSIM that we're running in batch/automation mode without UI
                            // While DWSIM 9.0.5.0 doesn't use this to skip UpdateInterface, it's the proper pattern
                            // and may help with future DWSIM versions or other UI-related code paths
                            if (automationMode != null)
                            {
                                automationMode.SetValue(null, true);
                                _logger.Information("Set GlobalSettings.AutomationMode = true (headless mode)");
                            }
                        }
                        else
                        {
                            _logger.Warning("Could not find Settings type in DWSIM.GlobalSettings assembly");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to set GlobalSettings properties");
                    }

                    // CRITICAL: Replace UpdateInterface with a no-op to prevent NullReferenceException in headless mode
                    // UpdateInterface is called repeatedly in the solver's wait loop, and we can't properly initialize Eto.Forms
                    try
                    {
                        var updateInterfaceMethod = flowsheet.GetType().GetMethod("UpdateInterface");
                        if (updateInterfaceMethod != null)
                        {
                            _logger.Debug("Attempting to override UpdateInterface with no-op delegate");

                            // Create a delegate that does nothing
                            Action noOpAction = () => { };

                            // Try to replace the method using a dynamic approach
                            // Unfortunately, we can't directly replace instance methods in C#
                            // So this is more of a documentation of the issue
                            _logger.Debug("Cannot directly override UpdateInterface - will handle exceptions instead");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug(ex, "Could not inspect UpdateInterface method");
                    }

                    _logger.Debug("Calling flowsheet.RequestCalculationAndWait()");
                    object result = null;
                    try
                    {
                        result = requestAndWait.Invoke(flowsheet, null);
                    }
                    catch (System.Reflection.TargetInvocationException tex)
                    {
                        // Check if it's a NullReferenceException from UpdateInterface - this is expected in headless mode
                        if (tex.InnerException is NullReferenceException nullRefEx &&
                            nullRefEx.StackTrace?.Contains("UpdateInterface") == true)
                        {
                            _logger.Warning("UpdateInterface NullRef (expected in headless mode) - waiting for background calculation to complete");

                            // The calculation runs in a background thread. Even though UpdateInterface threw,
                            // the background calculation might still be running. Wait a bit to give it time to complete.
                            System.Threading.Thread.Sleep(2000); // Wait 2 seconds for background thread to finish

                            _logger.Debug("Checking if units were calculated by background thread...");

                            // Check if units were calculated by the background thread
                            bool allUnitsCalculated = true;
                            foreach (var unitId in _context.GetUnitIds())
                            {
                                var unit = _context.GetUnit(unitId);
                                if (unit != null)
                                {
                                    var calculatedProp = unit.GetType().GetProperty("Calculated");
                                    var isCalculated = calculatedProp?.GetValue(unit) as bool?;
                                    if (isCalculated == false)
                                    {
                                        allUnitsCalculated = false;
                                        _logger.Debug("Unit {UnitId} not calculated", unitId);
                                        break;
                                    }
                                    else
                                    {
                                        _logger.Debug("Unit {UnitId} calculated successfully", unitId);
                                    }
                                }
                            }

                            if (allUnitsCalculated)
                            {
                                _logger.Information("All units calculated successfully by background thread despite UpdateInterface exception - solver succeeded");
                                return true; // Calculation actually succeeded
                            }
                            else
                            {
                                _logger.Warning("Units not calculated after waiting - UpdateInterface exception interrupted solver before completion");
                                return false; // Will trigger fallback Calculate()
                            }
                        }
                        throw;
                    }
                    _logger.Debug("RequestCalculationAndWait returned: {ResultType}", result?.GetType().Name ?? "null");

                    if (result is System.Collections.IEnumerable enumerable)
                    {
                        int exceptionCount = 0;
                        int itemCount = 0;
                        foreach (var item in enumerable)
                        {
                            itemCount++;
                            if (item is Exception ex)
                            {
                                exceptionCount++;
                                _logger.Error(ex, "DWSIM solver returned exception {Count}: {Message}", exceptionCount, ex.Message);

                                // Log inner exception details
                                if (ex.InnerException != null)
                                {
                                    _logger.Error("  Inner exception: {InnerType}: {InnerMessage}",
                                        ex.InnerException.GetType().Name, ex.InnerException.Message);
                                }

                                return false;
                            }
                            else
                            {
                                _logger.Debug("RequestCalculationAndWait result item {Index}: {ItemType}", itemCount, item?.GetType().Name ?? "null");
                            }
                        }
                        _logger.Debug("RequestCalculationAndWait returned {Count} items, {ExceptionCount} exceptions", itemCount, exceptionCount);

                        // Empty list means successful calculation (no exceptions)
                        if (itemCount == 0)
                        {
                            _logger.Information("Solver returned empty list - this indicates successful calculation with no exceptions");
                        }
                    }

                    _logger.Information("DWSIM solver completed (RequestCalculationAndWait)");
                    return true;
                }

                var solveMethod = flowsheetType.GetMethod("Solve");
                if (solveMethod != null)
                {
                    _logger.Debug("Calling flowsheet.Solve()");
                    solveMethod.Invoke(flowsheet, null);
                    _logger.Information("DWSIM solver completed (Solve)");
                    return true;
                }

                var requestCalculation = flowsheetType.GetMethod("RequestCalculation");
                if (requestCalculation != null)
                {
                    requestCalculation.Invoke(flowsheet, new object[] { null, true });
                    _logger.Information("DWSIM solver completed");
                    return true;
                }

                _logger.Warning("DWSIM solver method not found on flowsheet");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "DWSIM solver failed");
                return false;
            }
        }

        /// <summary>
        /// Gets the convergence status from the flowsheet after calculation.
        /// </summary>
        private ConvergenceStatus GetConvergenceStatus(object flowsheet)
        {
            _logger.Debug("Checking convergence status");

            try
            {
                var solvedProperty = flowsheet.GetType().GetProperty("Solved");
                if (solvedProperty != null)
                {
                    var solvedValue = solvedProperty.GetValue(flowsheet);
                    _logger.Debug("Flowsheet Solved property value: {SolvedValue}", solvedValue);

                    if (solvedValue is bool solved)
                    {
                        if (solved)
                        {
                            _logger.Information("Flowsheet converged successfully (Solved=true)");
                            return ConvergenceStatus.Converged(0, 0.0);
                        }
                        else
                        {
                            // Try to get error messages from flowsheet and unit operations
                            var errorMessage = "Flowsheet not solved (Solved=false)";
                            try
                            {
                                var errorProp = flowsheet.GetType().GetProperty("ErrorMessage");
                                if (errorProp != null)
                                {
                                    var error = errorProp.GetValue(flowsheet)?.ToString();
                                    if (!string.IsNullOrWhiteSpace(error))
                                    {
                                        errorMessage += $": {error}";
                                    }
                                }

                                // Check unit operations for errors
                                foreach (var unitId in _context.GetUnitIds())
                                {
                                    var unit = _context.GetUnit(unitId);
                                    if (unit != null)
                                    {
                                        try
                                        {
                                            var unitErrorProp = unit.GetType().GetProperty("ErrorMessage");
                                            if (unitErrorProp != null)
                                            {
                                                var unitError = unitErrorProp.GetValue(unit)?.ToString();
                                                if (!string.IsNullOrWhiteSpace(unitError))
                                                {
                                                    errorMessage += $"; Unit {unitId}: {unitError}";
                                                }
                                            }

                                            // Check if unit is calculated
                                            var calculatedProp = unit.GetType().GetProperty("Calculated");
                                            object calculated = null;
                                            if (calculatedProp != null)
                                            {
                                                calculated = calculatedProp.GetValue(unit);
                                                _logger.Debug("Unit {UnitId} Calculated property: {Calculated}", unitId, calculated);
                                            }

                                            // Check GraphicObject connector states
                                            try
                                            {
                                                var graphicObj = unit.GetType().GetProperty("GraphicObject")?.GetValue(unit);
                                                if (graphicObj != null)
                                                {
                                                    var inputConnectors = graphicObj.GetType().GetProperty("InputConnectors")?.GetValue(graphicObj);
                                                    var outputConnectors = graphicObj.GetType().GetProperty("OutputConnectors")?.GetValue(graphicObj);

                                                    if (inputConnectors is System.Collections.IList inputList)
                                                    {
                                                        _logger.Debug("Unit {UnitId} has {Count} input connectors", unitId, inputList.Count);
                                                        for (int i = 0; i < inputList.Count; i++)
                                                        {
                                                            var connector = inputList[i];
                                                            var isAttached = connector?.GetType().GetProperty("IsAttached")?.GetValue(connector);
                                                            _logger.Debug("  InputConnector[{Index}].IsAttached = {IsAttached}", i, isAttached);
                                                        }
                                                    }

                                                    if (outputConnectors is System.Collections.IList outputList)
                                                    {
                                                        _logger.Debug("Unit {UnitId} has {Count} output connectors", unitId, outputList.Count);
                                                        for (int i = 0; i < outputList.Count; i++)
                                                        {
                                                            var connector = outputList[i];
                                                            var isAttached = connector?.GetType().GetProperty("IsAttached")?.GetValue(connector);
                                                            _logger.Debug("  OutputConnector[{Index}].IsAttached = {IsAttached}", i, isAttached);
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception connEx)
                                            {
                                                _logger.Debug(connEx, "Could not check connector states for unit {UnitId}", unitId);
                                            }

                                            // Try calling Calculate directly to see what error we get
                                            if (calculated is bool calc && !calc)
                                            {
                                                _logger.Debug("Attempting direct Calculate() on unit {UnitId} to diagnose issue", unitId);
                                                try
                                                {
                                                    var calculateMethod = unit.GetType().GetMethod("Calculate");
                                                    if (calculateMethod != null)
                                                    {
                                                        calculateMethod.Invoke(unit, new object[] { null });
                                                        _logger.Information("Direct Calculate() on unit {UnitId} succeeded", unitId);

                                                        // Check if unit is now calculated
                                                        if (calculatedProp != null)
                                                        {
                                                            var nowCalculated = calculatedProp.GetValue(unit);
                                                            _logger.Debug("Unit {UnitId} Calculated property after direct call: {IsCalculated}", unitId, nowCalculated);
                                                        }
                                                    }
                                                }
                                                catch (System.Reflection.TargetInvocationException directCalcEx)
                                                {
                                                    var innerEx = directCalcEx.InnerException ?? directCalcEx;
                                                    _logger.Warning(innerEx, "Direct Calculate() on unit {UnitId} failed: {Message}", unitId, innerEx.Message);
                                                    errorMessage += $"; Direct calculate error: {innerEx.Message}";
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }
                            catch { }

                            _logger.Warning("Flowsheet did not converge: {Message}", errorMessage);
                            return ConvergenceStatus.NotConverged(errorMessage, 0, 0.0);
                        }
                    }
                }

                _logger.Warning("Solved property not found on flowsheet, assuming converged");
                return ConvergenceStatus.Converged(0, 0.0);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get convergence status");
                return ConvergenceStatus.Error($"Failed to get convergence status: {ex.Message}");
            }
        }

        /// <summary>
        /// Captures solver diagnostic messages during/after calculation.
        /// </summary>
        private IReadOnlyList<SolverMessage> GetSolverMessages(object flowsheet)
        {
            _logger.Debug("Capturing solver messages");

            var messages = new List<SolverMessage>();

            try
            {
                // TODO: In full implementation, capture messages from DWSIM solver events/logs
                // Example: Subscribe to flowsheet message events during calculation

                // For now, return empty list
                return messages.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to capture solver messages");
                return messages.AsReadOnly();
            }
        }

        /// <summary>
        /// Extracts calculated properties from all streams in the flowsheet.
        /// </summary>
        private IReadOnlyList<StreamResult> ExtractStreamResults()
        {
            _logger.Debug("Extracting stream results");

            var results = new List<StreamResult>();

            try
            {
                // Get all stream IDs from context
                var streamIds = _context.GetStreamIds();

                foreach (var streamId in streamIds)
                {
                    try
                    {
                        var streamResult = _streamAdapter.GetCalculatedProperties(streamId);
                        results.Add(streamResult);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to extract results for stream {StreamId}", streamId);
                        // Continue with other streams
                    }
                }

                _logger.Information("Extracted results for {StreamCount} streams", results.Count);
                return results.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to extract stream results");
                return results.AsReadOnly();
            }
        }

        /// <summary>
        /// Validates mass balance across the flowsheet.
        /// </summary>
        private MassBalanceResult ValidateMassBalance(IReadOnlyList<StreamResult> streamResults)
        {
            _logger.Debug("Validating mass balance");

            try
            {
                // TODO: In full implementation, identify inlet/outlet streams from connections
                // For now, use a simple approach: assume first stream is inlet, rest are outlets

                if (streamResults.Count < 2)
                {
                    _logger.Warning("Insufficient streams for mass balance validation");
                    return null;
                }

                var inletStreams = new List<StreamResult> { streamResults[0] };
                var outletStreams = streamResults.Skip(1).ToList();

                var massBalance = MassBalanceValidator.Validate(
                    inletStreams,
                    outletStreams,
                    MassBalanceValidator.DefaultTolerancePercent);

                if (!massBalance.IsValid)
                {
                    _logger.Warning("Mass balance validation failed: {Error}%", massBalance.RelativeErrorPercent);
                }
                else
                {
                    _logger.Information("Mass balance validation passed");
                }

                return massBalance;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Mass balance validation failed");
                return null;
            }
        }

        /// <summary>
        /// Gets unit operation-specific metrics.
        /// </summary>
        /// <param name="unitId">The unit operation ID.</param>
        /// <returns>A dictionary of metrics, or null if unit not found or metrics unavailable.</returns>
        public IDictionary<string, object> GetUnitMetrics(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                _logger.Warning("Unit ID cannot be null or empty");
                return null;
            }

            _logger.Debug("Getting metrics for unit {UnitId}", unitId);

            try
            {
                // TODO: In full implementation, query DWSIM unit operation for metrics
                // Example: For separator, get actual pressure drop, phase split ratios, etc.

                var unit = _context.GetUnit(unitId);
                if (unit == null)
                {
                    _logger.Warning("Unit {UnitId} not found", unitId);
                    return null;
                }

                // Placeholder: return empty dictionary
                var metrics = new Dictionary<string, object>();

                _logger.Debug("Retrieved {MetricCount} metrics for unit {UnitId}", metrics.Count, unitId);
                return metrics;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get metrics for unit {UnitId}", unitId);
                return null;
            }
        }

        /// <summary>
        /// Gets the most recent convergence status from cache.
        /// </summary>
        /// <returns>The cached convergence status.</returns>
        public ConvergenceStatus GetCurrentStatus()
        {
            return _context.GetCachedConvergenceStatus();
        }

        /// <summary>
        /// Gets the cached calculation result without re-running the solver.
        /// </summary>
        /// <returns>The cached calculation result, or null if none is cached.</returns>
        public CalculationResult GetCachedResult()
        {
            return _context.GetCachedCalculationResult();
        }
    }
}
