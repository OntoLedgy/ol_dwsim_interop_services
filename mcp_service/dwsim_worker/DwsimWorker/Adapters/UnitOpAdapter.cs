using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using DwsimWorker.Engine;
using DwsimWorker.Models;
using DwsimWorker.Exceptions;

namespace DwsimWorker.Adapters
{
    /// <summary>
    /// Adapter for adding unit operations to flowsheet and configuring their parameters.
    /// </summary>
    /// <remarks>
    /// This adapter wraps DWSIM's unit operation classes and provides a clean interface for
    /// unit operation management. Currently supports three-phase separators.
    ///
    /// Unit operations are the core processing elements in a flowsheet, representing
    /// physical equipment like separators, reactors, heat exchangers, etc.
    /// </remarks>
    public sealed class UnitOpAdapter
    {
        private readonly ILogger _logger;
        private readonly FlowsheetContext _context;
        private int _unitCounter = 0;

        // Internal registry for unit operation parameters (in-memory cache)
        private readonly Dictionary<string, Dictionary<string, object>> _unitParametersCache;

        // Port configuration for three-phase separator
        private static readonly Dictionary<string, string> ThreePhaseSeparatorPorts = new Dictionary<string, string>
        {
            { "Inlet", "inlet" },
            { "VaporOutlet", "vapor_outlet" },
            { "LiquidOutlet1", "liquid_outlet" },      // Light liquid
            { "LiquidOutlet2", "heavy_liquid_outlet" }  // Heavy liquid (water)
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitOpAdapter"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for unit operation logging.</param>
        /// <param name="context">The flowsheet context that manages unit operation state.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger or context is null.</exception>
        public UnitOpAdapter(ILogger logger, FlowsheetContext context)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _unitParametersCache = new Dictionary<string, Dictionary<string, object>>();
        }

        /// <summary>
        /// Adds a three-phase separator to the flowsheet.
        /// </summary>
        /// <param name="name">The name of the separator (e.g., "SEP-101", "Three-Phase Separator 1").</param>
        /// <param name="config">The configuration parameters for the separator. Can be null for default configuration.</param>
        /// <returns>
        /// A PropertySetResult containing the generated unitId on success.
        /// Returns a failure result if unit creation fails.
        /// </returns>
        /// <remarks>
        /// A three-phase separator separates a mixed stream into three phases:
        /// - Vapor (lightest phase - gases)
        /// - Light liquid (intermediate phase - oil/hydrocarbons)
        /// - Heavy liquid (heaviest phase - water)
        ///
        /// The separator has 1 inlet port and 3 outlet ports. Use ConnectionAdapter to
        /// connect streams to these ports.
        ///
        /// Supported parameters:
        /// - PressureDrop: Pressure drop across separator in Pascal (default: 0)
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new UnitOpAdapter(logger, context);
        ///
        /// // Add separator with default configuration
        /// var result = adapter.AddThreePhaseSeparator("SEP-101", null);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Separator created with ID: {result.Data}");
        /// }
        ///
        /// // Add separator with pressure drop
        /// var config = new UnitOpConfig(new Dictionary<string, object>
        /// {
        ///     { "PressureDrop", 1000.0 }  // 1000 Pa pressure drop
        /// });
        /// var result2 = adapter.AddThreePhaseSeparator("SEP-102", config);
        /// </code>
        /// </example>
        public PropertySetResult AddThreePhaseSeparator(string name, UnitOpConfig config)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                var message = "Unit operation name cannot be null or empty";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(name)));
            }

            _logger.Debug("Attempting to add three-phase separator: {UnitName}", name);

            try
            {
                // Step 1: Generate unique unit ID
                _unitCounter++;
                var unitId = $"U{_unitCounter}";

                // Step 2: Create DWSIM unit operation object
                // TODO: In full implementation, create actual DWSIM.UnitOperations.Separators.Separator3Phase
                // For now, we add a placeholder to the context
                _context.AddUnit(null, unitId);  // null as placeholder for actual unit operation object

                // Step 3: Initialize parameters from config
                var parameters = new Dictionary<string, object>();
                if (config != null && config.Parameters.Count > 0)
                {
                    foreach (var param in config.Parameters)
                    {
                        parameters[param.Key] = param.Value;
                    }
                }

                // Set default parameters if not specified
                if (!parameters.ContainsKey("PressureDrop"))
                {
                    parameters["PressureDrop"] = 0.0;  // Default: no pressure drop
                }

                _unitParametersCache[unitId] = parameters;

                // Step 4: Log success with structured logging
                _logger.Information("Three-phase separator added successfully: {UnitId} (Name: {UnitName})",
                    unitId, name);
                _logger.Debug("Separator parameters: {Parameters}",
                    string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}")));

                return PropertySetResult.SuccessResult(unitId);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error adding three-phase separator '{name}': {ex.Message}";
                _logger.Error(ex, message);
                return PropertySetResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Sets a parameter value on a unit operation.
        /// </summary>
        /// <param name="unitId">The unique unit operation identifier.</param>
        /// <param name="parameterName">
        /// The parameter name. For three-phase separator, supported parameters: "PressureDrop" (Pa).
        /// </param>
        /// <param name="value">The parameter value.</param>
        /// <returns>A PropertySetResult indicating success or failure.</returns>
        /// <remarks>
        /// Parameter names are case-sensitive. Values are validated before being set.
        ///
        /// Supported parameters for three-phase separator:
        /// - PressureDrop: Pressure drop across separator (Pa), must be >= 0
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new UnitOpAdapter(logger, context);
        ///
        /// // Set pressure drop
        /// var result = adapter.SetParameter("U1", "PressureDrop", 5000.0);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine("Pressure drop set to 5000 Pa");
        /// }
        /// </code>
        /// </example>
        public PropertySetResult SetParameter(string unitId, string parameterName, object value)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                var message = "Unit ID cannot be null or empty";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(unitId)));
            }

            if (string.IsNullOrWhiteSpace(parameterName))
            {
                var message = "Parameter name cannot be null or empty";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(parameterName)));
            }

            _logger.Debug("Setting parameter {ParameterName}={Value} on unit {UnitId}",
                parameterName, value, unitId);

            try
            {
                // Step 1: Validate unit exists
                if (_context.GetUnit(unitId) == null)
                {
                    var message = $"Unit operation '{unitId}' not found";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new UnitNotFoundException(unitId));
                }

                // Step 2: Validate parameter value
                if (!ValidateParameterValue(parameterName, value, out string errorMessage))
                {
                    _logger.Warning("Parameter value validation failed: {ErrorMessage}", errorMessage);
                    return PropertySetResult.FailureResult(
                        $"Invalid parameter value for '{parameterName}': {errorMessage}",
                        new InvalidPropertyValueException(parameterName, value?.ToString(), errorMessage));
                }

                // Step 3: Set parameter value
                if (!_unitParametersCache.ContainsKey(unitId))
                {
                    _unitParametersCache[unitId] = new Dictionary<string, object>();
                }

                _unitParametersCache[unitId][parameterName] = value;

                // Step 4: Log success
                _logger.Information("Parameter set successfully: {UnitId}.{ParameterName}={Value}",
                    unitId, parameterName, value);

                return PropertySetResult.SuccessResult(parameterName);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error setting parameter '{parameterName}' on unit '{unitId}': {ex.Message}";
                _logger.Error(ex, message);
                return PropertySetResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Gets a parameter value from a unit operation.
        /// </summary>
        /// <param name="unitId">The unique unit operation identifier.</param>
        /// <param name="parameterName">The parameter name.</param>
        /// <returns>
        /// A PropertySetResult containing the parameter value on success.
        /// Returns a failure result if the unit or parameter is not found.
        /// </returns>
        /// <example>
        /// <code>
        /// var adapter = new UnitOpAdapter(logger, context);
        ///
        /// var result = adapter.GetParameter("U1", "PressureDrop");
        /// if (result.Success)
        /// {
        ///     double pressureDrop = (double)result.Data;
        ///     Console.WriteLine($"Pressure drop: {pressureDrop} Pa");
        /// }
        /// </code>
        /// </example>
        public PropertySetResult GetParameter(string unitId, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                var message = "Unit ID cannot be null or empty";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(unitId)));
            }

            if (string.IsNullOrWhiteSpace(parameterName))
            {
                var message = "Parameter name cannot be null or empty";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(parameterName)));
            }

            _logger.Debug("Getting parameter {ParameterName} from unit {UnitId}",
                parameterName, unitId);

            try
            {
                // Step 1: Validate unit exists
                if (_context.GetUnit(unitId) == null)
                {
                    var message = $"Unit operation '{unitId}' not found";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new UnitNotFoundException(unitId));
                }

                // Step 2: Get parameter value
                if (!_unitParametersCache.TryGetValue(unitId, out var parameters))
                {
                    var message = $"No parameters found for unit '{unitId}'";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new InvalidOperationException(message));
                }

                if (!parameters.TryGetValue(parameterName, out var value))
                {
                    var message = $"Parameter '{parameterName}' not found on unit '{unitId}'";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new KeyNotFoundException(message));
                }

                _logger.Debug("Parameter retrieved: {UnitId}.{ParameterName}={Value}",
                    unitId, parameterName, value);

                return PropertySetResult.SuccessResultWithData(value);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error getting parameter '{parameterName}' from unit '{unitId}': {ex.Message}";
                _logger.Error(ex, message);
                return PropertySetResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Gets the port names and types for a unit operation.
        /// </summary>
        /// <param name="unitId">The unique unit operation identifier.</param>
        /// <returns>
        /// A ValidationResult containing a dictionary of port names to port types on success.
        /// Port types are "inlet", "vapor_outlet", "liquid_outlet", "heavy_liquid_outlet".
        /// </returns>
        /// <remarks>
        /// For a three-phase separator, the ports are:
        /// - Inlet: inlet (feed stream)
        /// - VaporOutlet: vapor_outlet (gas phase)
        /// - LiquidOutlet1: liquid_outlet (light liquid phase - oil/hydrocarbons)
        /// - LiquidOutlet2: heavy_liquid_outlet (heavy liquid phase - water)
        ///
        /// Use these port names when connecting streams via ConnectionAdapter.
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new UnitOpAdapter(logger, context);
        ///
        /// var result = adapter.GetPorts("U1");
        /// if (result.Success)
        /// {
        ///     var ports = (Dictionary&lt;string, string&gt;)result.Data;
        ///     foreach (var port in ports)
        ///     {
        ///         Console.WriteLine($"Port: {port.Key}, Type: {port.Value}");
        ///     }
        /// }
        /// </code>
        /// </example>
        public ValidationResult GetPorts(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                var message = "Unit ID cannot be null or empty";
                _logger.Warning(message);
                return ValidationResult.FailureResult(message, new ArgumentException(message, nameof(unitId)));
            }

            _logger.Debug("Getting ports for unit {UnitId}", unitId);

            try
            {
                // Validate unit exists
                if (_context.GetUnit(unitId) == null)
                {
                    var message = $"Unit operation '{unitId}' not found";
                    _logger.Warning(message);
                    return ValidationResult.FailureResult(message, new UnitNotFoundException(unitId));
                }

                // Return three-phase separator ports
                // TODO: In full implementation, query actual DWSIM unit operation for ports
                var ports = new Dictionary<string, string>(ThreePhaseSeparatorPorts);

                _logger.Information("Retrieved {PortCount} ports for unit {UnitId}", ports.Count, unitId);

                // Return as a list of strings in format "PortName:PortType"
                var portStrings = ports.Select(p => $"{p.Key}:{p.Value}").ToList();
                return ValidationResult.SuccessResult(portStrings);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error getting ports for unit '{unitId}': {ex.Message}";
                _logger.Error(ex, message);
                return ValidationResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Validates a parameter value against constraints.
        /// </summary>
        /// <param name="parameterName">The parameter name.</param>
        /// <param name="value">The value to validate.</param>
        /// <param name="errorMessage">If validation fails, contains a description of the error.</param>
        /// <returns>True if the value is valid; otherwise, false.</returns>
        private bool ValidateParameterValue(string parameterName, object value, out string errorMessage)
        {
            if (value == null)
            {
                errorMessage = "Parameter value cannot be null";
                return false;
            }

            try
            {
                switch (parameterName)
                {
                    case "PressureDrop":
                        if (!(value is double) && !(value is int))
                        {
                            errorMessage = $"PressureDrop value must be numeric. Provided type: {value.GetType().Name}";
                            return false;
                        }
                        double pressureDrop = Convert.ToDouble(value);
                        if (pressureDrop < 0.0)
                        {
                            errorMessage = $"PressureDrop must be >= 0 Pa. Provided: {pressureDrop} Pa";
                            return false;
                        }
                        break;

                    default:
                        // For unknown parameters, accept any non-null value
                        _logger.Debug("Parameter '{ParameterName}' not in validation list, accepting value", parameterName);
                        break;
                }

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Validation error: {ex.Message}";
                return false;
            }
        }
    }
}
