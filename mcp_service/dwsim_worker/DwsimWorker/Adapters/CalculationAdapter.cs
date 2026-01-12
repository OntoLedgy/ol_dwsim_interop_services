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

                // Step 3: Create timing information
                var timing = CalculationTiming.FromTimestamps(startTime, endTime);

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
                    var result = requestAndWait.Invoke(flowsheet, null);
                    if (result is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            if (item is Exception ex)
                            {
                                _logger.Error(ex, "DWSIM solver returned exception");
                                return false;
                            }
                        }
                    }

                    _logger.Information("DWSIM solver completed");
                    return true;
                }

                var solveMethod = flowsheetType.GetMethod("Solve");
                if (solveMethod != null)
                {
                    solveMethod.Invoke(flowsheet, null);
                    _logger.Information("DWSIM solver completed");
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
