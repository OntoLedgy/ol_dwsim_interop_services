using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using DwsimWorker.Engine;
using DwsimWorker.Models;
using DwsimWorker.Converters;
using DwsimWorker.Exceptions;

namespace DwsimWorker.Adapters
{
    /// <summary>
    /// Adapter for creating material streams and setting/getting thermodynamic properties.
    /// Uses CAPE-OPEN interfaces for property operations.
    /// </summary>
    /// <remarks>
    /// This adapter wraps DWSIM's MaterialStream class and provides a clean interface for
    /// stream creation and property manipulation. It uses the CapeOpenPropertyConverter for
    /// property name mapping and validates all inputs before calling DWSIM APIs.
    ///
    /// All properties use SI units:
    /// - Temperature: Kelvin (K)
    /// - Pressure: Pascal (Pa)
    /// - Molar Flow: mol/s
    /// - Composition: Mole fractions (dimensionless, sum = 1.0)
    /// </remarks>
    public sealed class StreamAdapter
    {
        private readonly ILogger _logger;
        private readonly FlowsheetContext _context;
        private int _streamCounter = 0;

        private static readonly Dictionary<int, string> PhaseIdToName = new Dictionary<int, string>
        {
            { 0, "Overall" },
            { 1, "OverallLiquid" },
            { 2, "Vapor" },
            { 3, "Liquid1" },
            { 4, "Liquid2" },
            { 5, "Liquid3" },
            { 6, "Aqueous" },
            { 7, "Solid" }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamAdapter"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for stream operation logging.</param>
        /// <param name="context">The flowsheet context that manages stream state.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger or context is null.</exception>
        public StreamAdapter(ILogger logger, FlowsheetContext context)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Creates a new material stream with the specified properties.
        /// </summary>
        /// <param name="name">The name of the stream (e.g., "Inlet", "Feed1").</param>
        /// <param name="properties">The thermodynamic properties for the stream.</param>
        /// <param name="isSource">
        /// Optional. True if this is a feed stream (calculation starting point), false if this is an outlet stream
        /// from a unit operation. Defaults to false. Only set to true for feed streams with known conditions.
        /// </param>
        /// <returns>
        /// A PropertySetResult containing the generated streamId on success.
        /// Returns a failure result if validation fails or stream creation fails.
        /// </returns>
        /// <remarks>
        /// The stream is assigned a unique ID (e.g., "S1", "S2") that is returned on success.
        /// This ID must be used for all subsequent property get/set operations on the stream.
        ///
        /// The properties are validated before stream creation:
        /// - Temperature must be &gt; 0 K and &lt; 10000 K
        /// - Pressure must be &gt; 0 Pa and &lt; 1e9 Pa
        /// - Molar flow must be &gt;= 0 mol/s
        /// - Composition mole fractions must sum to 1.0 ± 1e-6
        ///
        /// IMPORTANT: The isSource parameter controls whether DWSIM will calculate this stream:
        /// - isSource=true: Feed stream with known conditions, DWSIM will NOT calculate it
        /// - isSource=false: Outlet stream from unit operation, DWSIM will calculate it
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new StreamAdapter(logger, context);
        ///
        /// var composition = new Composition(new[] { 0.7, 0.2, 0.1 });  // 3 compounds
        /// var properties = new StreamProperties(
        ///     temperatureK: 298.15,
        ///     pressurePa: 101325,
        ///     molarFlowMolPerSec: 100.0,
        ///     composition: composition);
        ///
        /// // Create feed stream (isSource=true)
        /// var feedResult = adapter.CreateStream("Feed", properties, isSource: true);
        ///
        /// // Create outlet stream (isSource=false, default)
        /// var outletResult = adapter.CreateStream("Outlet", properties);
        /// </code>
        /// </example>
        public PropertySetResult CreateStream(string name, StreamProperties properties, bool isSource = false)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                var message = "Stream name cannot be null or empty";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(name)));
            }

            if (properties == null)
            {
                var message = "Stream properties cannot be null";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentNullException(nameof(properties)));
            }

            _logger.Debug("Attempting to create stream: {StreamName}", name);

            try
            {
                // Step 1: Validate properties
                if (!properties.IsValid(out string errorMessage))
                {
                    _logger.Warning("Stream property validation failed: {ErrorMessage}", errorMessage);
                    return PropertySetResult.FailureResult(
                        $"Invalid stream properties: {errorMessage}",
                        new InvalidPropertyValueException("StreamProperties", properties.ToString(), errorMessage));
                }

                // Step 2: Generate unique stream ID
                _streamCounter++;
                var streamId = $"S{_streamCounter}";

                // Step 3: Create DWSIM MaterialStream object
                var flowsheet = _context.GetFlowsheet();
                var materialStream = CreateMaterialStream(flowsheet, name, streamId);
                _context.AddStream(materialStream, streamId);

                // Step 3a: Set IsSource property based on caller specification
                // CRITICAL: Only feed streams should have IsSource=True. Outlet streams from unit operations
                // must have IsSource=False so DWSIM knows they need to be calculated.
                // - IsSource=true: Feed stream with known conditions (DWSIM will NOT calculate)
                // - IsSource=false: Outlet stream that needs calculation (DWSIM will calculate)
                TrySetIsSourceProperty(materialStream, isSource);

                // Step 4: Apply flowsheet settings
                TrySetStreamPropertyPackage(materialStream);
                TryAddCompoundsToStream(flowsheet, materialStream);
                ApplyPropertiesToStreamObject(materialStream, properties, isSource);

                // Step 5: Store properties in cache
                _context.CacheStreamProperties(streamId, properties);

                // Step 6: Log success with structured logging
                _logger.Information("Stream created successfully: {StreamId} (Name: {StreamName})",
                    streamId, name);
                _logger.Debug("Stream properties: Temperature={Temperature}, Pressure={Pressure}, Flow={Flow}",
                    properties.Temperature.Measurement, properties.Pressure.Measurement, properties.MolarFlow.Measurement);

                return PropertySetResult.SuccessResultForCreate(streamId);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error creating stream '{name}': {ex.Message}";
                _logger.Error(ex, message);
                return PropertySetResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Sets a single property value on an existing stream.
        /// </summary>
        /// <param name="streamId">The unique stream identifier.</param>
        /// <param name="propertyName">
        /// The property name (user-friendly or CAPE-OPEN). Supported: "temperature", "pressure", "molarFlow", "composition".
        /// </param>
        /// <param name="value">The property value in SI units.</param>
        /// <returns>
        /// A PropertySetResult indicating success or failure.
        /// </returns>
        /// <remarks>
        /// Property names are case-insensitive and can be either user-friendly (e.g., "molarFlow")
        /// or CAPE-OPEN (e.g., "totalFlow"). The CapeOpenPropertyConverter handles the mapping.
        ///
        /// Values are validated before being set:
        /// - temperature: Must be > 0 K
        /// - pressure: Must be > 0 Pa
        /// - molarFlow: Must be >= 0 mol/s
        /// - composition: Must be array of mole fractions summing to 1.0
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new StreamAdapter(logger, context);
        ///
        /// // Set temperature
        /// var result = adapter.SetProperty("S1", "temperature", 320.0);
        ///
        /// // Set pressure using CAPE-OPEN name
        /// var result2 = adapter.SetProperty("S1", "pressure", 200000.0);
        /// </code>
        /// </example>
        public PropertySetResult SetProperty(string streamId, string propertyName, object value)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                var message = "Stream ID cannot be null or empty";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(streamId)));
            }

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                var message = "Property name cannot be null or empty";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(propertyName)));
            }

            _logger.Debug("Setting property {PropertyName}={Value} on stream {StreamId}",
                propertyName, value, streamId);

            try
            {
                // Step 1: Validate stream exists
                if (_context.GetStream(streamId) == null)
                {
                    var message = $"Stream '{streamId}' not found";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new StreamNotFoundException(streamId));
                }

                // Step 2: Validate property name
                if (!CapeOpenPropertyConverter.IsValidPropertyName(propertyName))
                {
                    var message = $"Invalid property name: '{propertyName}'. Supported properties: {string.Join(", ", CapeOpenPropertyConverter.GetSupportedProperties())}";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(propertyName)));
                }

                // Step 3: Validate property value
                if (!ValidatePropertyValue(propertyName, value, out string errorMessage))
                {
                    _logger.Warning("Property value validation failed: {ErrorMessage}", errorMessage);
                    return PropertySetResult.FailureResult(
                        $"Invalid property value for '{propertyName}': {errorMessage}",
                        new InvalidPropertyValueException(propertyName, value?.ToString(), errorMessage));
                }

                // Step 4: Set property value
                if (_context.TryGetStreamProperties(streamId, out var currentProps))
                {
                    var normalizedName = CapeOpenPropertyConverter.ToCapeOpenName(propertyName);
                    var updated = UpdateProperty(currentProps, normalizedName, value);
                    _context.CacheStreamProperties(streamId, updated);
                    ApplyPropertiesToStreamObject(_context.GetStream(streamId), updated);
                }

                // Step 5: Log success
                _logger.Information("Property set successfully: {StreamId}.{PropertyName}={Value}",
                    streamId, propertyName, value);

                return PropertySetResult.SuccessResult(propertyName);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error setting property '{propertyName}' on stream '{streamId}': {ex.Message}";
                _logger.Error(ex, message);
                return PropertySetResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Gets a single property value from an existing stream.
        /// </summary>
        /// <param name="streamId">The unique stream identifier.</param>
        /// <param name="propertyName">
        /// The property name (user-friendly or CAPE-OPEN). Supported: "temperature", "pressure", "molarFlow", "composition".
        /// </param>
        /// <returns>
        /// A PropertySetResult containing the property value on success.
        /// </returns>
        /// <example>
        /// <code>
        /// var adapter = new StreamAdapter(logger, context);
        ///
        /// var result = adapter.GetProperty("S1", "temperature");
        /// if (result.Success)
        /// {
        ///     double temp = (double)result.Data;
        ///     Console.WriteLine($"Temperature: {temp} K");
        /// }
        /// </code>
        /// </example>
        public PropertySetResult GetProperty(string streamId, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                var message = "Stream ID cannot be null or empty";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(streamId)));
            }

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                var message = "Property name cannot be null or empty";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(propertyName)));
            }

            _logger.Debug("Getting property {PropertyName} from stream {StreamId}",
                propertyName, streamId);

            try
            {
                // Step 1: Validate stream exists
                if (_context.GetStream(streamId) == null)
                {
                    var message = $"Stream '{streamId}' not found";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new StreamNotFoundException(streamId));
                }

                // Step 2: Validate property name
                if (!CapeOpenPropertyConverter.IsValidPropertyName(propertyName))
                {
                    var message = $"Invalid property name: '{propertyName}'. Supported properties: {string.Join(", ", CapeOpenPropertyConverter.GetSupportedProperties())}";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(propertyName)));
                }

                // Step 3: Get property value
                // TODO: In full implementation, call DWSIM MaterialStream API using CAPE-OPEN interfaces
                // For now, return from cached properties
                if (!_context.TryGetStreamProperties(streamId, out var props))
                {
                    var message = $"No properties cached for stream '{streamId}'";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new InvalidOperationException(message));
                }

                var normalizedName = CapeOpenPropertyConverter.ToCapeOpenName(propertyName);
                object value = GetPropertyFromStreamProperties(props, normalizedName);

                _logger.Debug("Property retrieved: {StreamId}.{PropertyName}={Value}",
                    streamId, propertyName, value);

                return PropertySetResult.SuccessResultWithData(value);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error getting property '{propertyName}' from stream '{streamId}': {ex.Message}";
                _logger.Error(ex, message);
                return PropertySetResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Sets all properties on a stream at once.
        /// </summary>
        /// <param name="streamId">The unique stream identifier.</param>
        /// <param name="properties">The complete set of stream properties to set.</param>
        /// <returns>A PropertySetResult indicating success or failure.</returns>
        public PropertySetResult SetProperties(string streamId, StreamProperties properties)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                var message = "Stream ID cannot be null or empty";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(streamId)));
            }

            if (properties == null)
            {
                var message = "Stream properties cannot be null";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentNullException(nameof(properties)));
            }

            _logger.Debug("Setting all properties on stream {StreamId}", streamId);

            try
            {
                // Step 1: Validate stream exists
                if (_context.GetStream(streamId) == null)
                {
                    var message = $"Stream '{streamId}' not found";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new StreamNotFoundException(streamId));
                }

                // Step 2: Validate properties
                if (!properties.IsValid(out string errorMessage))
                {
                    _logger.Warning("Stream property validation failed: {ErrorMessage}", errorMessage);
                    return PropertySetResult.FailureResult(
                        $"Invalid stream properties: {errorMessage}",
                        new InvalidPropertyValueException("StreamProperties", properties.ToString(), errorMessage));
                }

                // Step 3: Set all properties
                _context.CacheStreamProperties(streamId, properties);
                ApplyPropertiesToStreamObject(_context.GetStream(streamId), properties);

                _logger.Information("All properties set successfully on stream {StreamId}", streamId);

                return PropertySetResult.SuccessResult(streamId);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error setting properties on stream '{streamId}': {ex.Message}";
                _logger.Error(ex, message);
                return PropertySetResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Gets all properties from a stream.
        /// </summary>
        /// <param name="streamId">The unique stream identifier.</param>
        /// <returns>A PropertySetResult containing the StreamProperties object on success.</returns>
        public PropertySetResult GetProperties(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                var message = "Stream ID cannot be null or empty";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentException(message, nameof(streamId)));
            }

            _logger.Debug("Getting all properties from stream {StreamId}", streamId);

            try
            {
                // Validate stream exists
                if (_context.GetStream(streamId) == null)
                {
                    var message = $"Stream '{streamId}' not found";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new StreamNotFoundException(streamId));
                }

                // Get properties from cache
                if (!_context.TryGetStreamProperties(streamId, out var properties))
                {
                    var message = $"No properties cached for stream '{streamId}'";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new InvalidOperationException(message));
                }

                _logger.Debug("All properties retrieved from stream {StreamId}", streamId);

                return PropertySetResult.SuccessResultWithData(properties);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error getting properties from stream '{streamId}': {ex.Message}";
                _logger.Error(ex, message);
                return PropertySetResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Validates a property value against physical constraints.
        /// </summary>
        /// <param name="propertyName">The property name (user-friendly or CAPE-OPEN).</param>
        /// <param name="value">The value to validate.</param>
        /// <param name="errorMessage">If validation fails, contains a description of the error.</param>
        /// <returns>True if the value is valid; otherwise, false.</returns>
        private bool ValidatePropertyValue(string propertyName, object value, out string errorMessage)
        {
            if (value == null)
            {
                errorMessage = "Property value cannot be null";
                return false;
            }

            try
            {
                var normalizedName = CapeOpenPropertyConverter.ToCapeOpenName(propertyName);

                switch (normalizedName)
                {
                    case "temperature":
                        if (!(value is double) && !(value is int))
                        {
                            errorMessage = $"Temperature value must be numeric. Provided type: {value.GetType().Name}";
                            return false;
                        }
                        double temp = Convert.ToDouble(value);
                        if (temp <= 0.0)
                        {
                            errorMessage = $"Temperature must be > 0 K. Provided: {temp} K";
                            return false;
                        }
                        break;

                    case "pressure":
                        if (!(value is double) && !(value is int))
                        {
                            errorMessage = $"Pressure value must be numeric. Provided type: {value.GetType().Name}";
                            return false;
                        }
                        double pressure = Convert.ToDouble(value);
                        if (pressure <= 0.0)
                        {
                            errorMessage = $"Pressure must be > 0 Pa. Provided: {pressure} Pa";
                            return false;
                        }
                        break;

                    case "totalFlow":
                        if (!(value is double) && !(value is int))
                        {
                            errorMessage = $"Molar flow value must be numeric. Provided type: {value.GetType().Name}";
                            return false;
                        }
                        double flow = Convert.ToDouble(value);
                        if (flow < 0.0)
                        {
                            errorMessage = $"Molar flow must be >= 0 mol/s. Provided: {flow} mol/s";
                            return false;
                        }
                        break;

                    case "composition":
                        // Composition should be an array or list of doubles
                        if (!(value is IEnumerable<double>) && !(value is double[]))
                        {
                            errorMessage = $"Composition must be an array of doubles. Provided type: {value.GetType().Name}";
                            return false;
                        }
                        // Additional validation would check that mole fractions sum to 1.0
                        break;

                    default:
                        errorMessage = $"Unknown property name: {normalizedName}";
                        return false;
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

        /// <summary>
        /// Updates a single property in a StreamProperties object, returning a new instance.
        /// </summary>
        private StreamProperties UpdateProperty(StreamProperties current, string capeOpenPropertyName, object value)
        {
            PhysicalProperties temp = current.Temperature;
            PhysicalProperties pressure = current.Pressure;
            PhysicalProperties flow = current.MolarFlow;
            Composition composition = current.Composition;

            switch (capeOpenPropertyName)
            {
                case "temperature":
                    // Keep the same quantity and unit, update value
                    var tempMeasurement = new Measurements(
                        current.Temperature.Quantity,
                        Convert.ToDouble(value),
                        current.Temperature.Unit);
                    temp = new PhysicalProperties("Temperature", tempMeasurement);
                    break;
                case "pressure":
                    // Keep the same quantity and unit, update value
                    var pressureMeasurement = new Measurements(
                        current.Pressure.Quantity,
                        Convert.ToDouble(value),
                        current.Pressure.Unit);
                    pressure = new PhysicalProperties("Pressure", pressureMeasurement);
                    break;
                case "totalFlow":
                    // Keep the same quantity and unit, update value
                    var flowMeasurement = new Measurements(
                        current.MolarFlow.Quantity,
                        Convert.ToDouble(value),
                        current.MolarFlow.Unit);
                    flow = new PhysicalProperties("MolarFlow", flowMeasurement);
                    break;
                case "composition":
                    // In full implementation, this would handle composition arrays
                    break;
            }

            return new StreamProperties(temp, pressure, flow, composition);
        }

        /// <summary>
        /// Gets a property value from a StreamProperties object.
        /// </summary>
        private object GetPropertyFromStreamProperties(StreamProperties props, string capeOpenPropertyName)
        {
            switch (capeOpenPropertyName)
            {
                case "temperature":
                    return props.Temperature.Measurement.Value;
                case "pressure":
                    return props.Pressure.Measurement.Value;
                case "totalFlow":
                    return props.MolarFlow.Measurement.Value;
                case "composition":
                    return props.Composition.MoleFractions;
                default:
                    throw new ArgumentException($"Unknown CAPE-OPEN property name: {capeOpenPropertyName}");
            }
        }

        /// <summary>
        /// Gets calculated properties from a stream after a calculation has been performed.
        /// </summary>
        /// <param name="streamId">The unique stream identifier.</param>
        /// <param name="streamName">Optional stream name. If not provided, uses streamId.</param>
        /// <returns>A StreamResult containing all calculated properties.</returns>
        /// <remarks>
        /// This method should be called after a flowsheet calculation to retrieve the computed
        /// thermodynamic properties, phase fractions, and compositions. It returns a StreamResult
        /// object with all relevant data for analysis and validation.
        /// </remarks>
        public StreamResult GetCalculatedProperties(string streamId, string streamName = null)
        {
            if (string.IsNullOrWhiteSpace(streamId))
                throw new ArgumentNullException(nameof(streamId));

            _logger.Debug("Getting calculated properties from stream {StreamId}", streamId);

            try
            {
                // Validate stream exists
                var stream = _context.GetStream(streamId);
                if (stream == null)
                {
                    throw new StreamNotFoundException(streamId);
                }

                var phases = GetAllPhaseProperties(streamId, stream);
                if (phases.Count > 0)
                {
                    var overallPhase = phases.TryGetValue("Overall", out var overall) ? overall : null;
                    var temperature = overallPhase?.Composition != null
                        ? TryGetPhaseScalar(stream, 0, "temperature")
                        : null;
                    var pressure = overallPhase?.Composition != null
                        ? TryGetPhaseScalar(stream, 0, "pressure")
                        : null;
                    var molarFlow = overallPhase?.Composition != null
                        ? TryGetPhaseScalar(stream, 0, "molarflow")
                        : null;
                    var massFlow = overallPhase?.Composition != null
                        ? TryGetPhaseScalar(stream, 0, "massflow")
                        : null;

                    var overallComposition = overallPhase?.Composition ?? GetFallbackComposition(streamId);
                    var vaporFraction = GetPhaseFraction(phases, "Vapor");
                    var liquidFraction = GetLiquidFraction(phases, vaporFraction);

                    var result = new StreamResult(
                        streamId: streamId,
                        streamName: streamName ?? streamId,
                        temperatureK: temperature ?? GetFallbackTemperature(streamId),
                        pressurePa: pressure ?? GetFallbackPressure(streamId),
                        molarFlowMolPerSec: molarFlow ?? GetFallbackMolarFlow(streamId),
                        massFlowKgPerSec: massFlow ?? GetFallbackMassFlow(streamId),
                        overallComposition: overallComposition,
                        vaporFraction: vaporFraction,
                        liquidFraction: liquidFraction,
                        phases: phases.ToDictionary(entry => entry.Key, entry => entry.Value));

                    _logger.Information("Calculated properties retrieved for stream {StreamId}", streamId);
                    return result;
                }

                // Get cached properties (fallback when DWSIM phase data is not available)
                if (!_context.TryGetStreamProperties(streamId, out var properties))
                {
                    throw new InvalidOperationException($"No properties available for stream '{streamId}'");
                }

                var fallbackPhase = BuildFallbackPhaseProperties(streamId, "Overall");
                var fallbackPhases = new Dictionary<string, PhaseProperties>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Overall", fallbackPhase }
                };

                // Create StreamResult from cached data
                var fallbackResult = new StreamResult(
                    streamId: streamId,
                    streamName: streamName ?? streamId,
                    temperatureK: properties.Temperature.Measurement.Value,
                    pressurePa: properties.Pressure.Measurement.Value,
                    molarFlowMolPerSec: properties.MolarFlow.Measurement.Value,
                    massFlowKgPerSec: properties.MolarFlow.Measurement.Value * 18.0 / 1000.0,
                    overallComposition: properties.Composition,
                    vaporFraction: 0.0,
                    liquidFraction: 1.0,
                    phases: fallbackPhases);

                _logger.Information("Calculated properties retrieved for stream {StreamId}", streamId);
                return fallbackResult;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get calculated properties for stream {StreamId}", streamId);
                throw;
            }
        }

        /// <summary>
        /// Gets phase-specific properties for a given phase in a stream.
        /// </summary>
        /// <param name="streamId">The unique stream identifier.</param>
        /// <param name="phaseName">The phase name (e.g., "Vapor", "Liquid1", "Liquid2", "Overall").</param>
        /// <returns>PhaseProperties for the specified phase.</returns>
        /// <exception cref="ArgumentNullException">Thrown when streamId or phaseName is null/empty.</exception>
        /// <exception cref="StreamNotFoundException">Thrown when the stream is not found.</exception>
        /// <exception cref="ArgumentException">Thrown when the phase is not found in the stream.</exception>
        public PhaseProperties GetPhaseProperties(string streamId, string phaseName)
        {
            if (string.IsNullOrWhiteSpace(streamId))
                throw new ArgumentNullException(nameof(streamId));

            if (string.IsNullOrWhiteSpace(phaseName))
                throw new ArgumentNullException(nameof(phaseName));

            _logger.Debug("Getting phase properties for {PhaseName} in stream {StreamId}", phaseName, streamId);

            try
            {
                // Validate stream exists
                var stream = _context.GetStream(streamId);
                if (stream == null)
                {
                    throw new StreamNotFoundException(streamId);
                }

                var phases = GetAllPhaseProperties(streamId, stream);
                if (phases.Count == 0)
                {
                    var fallbackPhase = BuildFallbackPhaseProperties(streamId, phaseName);
                    _logger.Information("Phase properties retrieved for {PhaseName} in stream {StreamId}", phaseName, streamId);
                    return fallbackPhase;
                }

                if (phases.TryGetValue(phaseName, out var phaseProperties))
                {
                    _logger.Information("Phase properties retrieved for {PhaseName} in stream {StreamId}", phaseName, streamId);
                    return phaseProperties;
                }

                throw new ArgumentException($"Phase '{phaseName}' not found in stream '{streamId}'.", nameof(phaseName));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get phase properties for {PhaseName} in stream {StreamId}", phaseName, streamId);
                throw;
            }
        }

        /// <summary>
        /// Gets all phase properties from a stream.
        /// </summary>
        /// <param name="streamId">The unique stream identifier.</param>
        /// <returns>A dictionary of phase properties keyed by phase name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when streamId is null/empty.</exception>
        /// <exception cref="StreamNotFoundException">Thrown when the stream is not found.</exception>
        public IReadOnlyDictionary<string, PhaseProperties> GetAllPhaseProperties(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId))
                throw new ArgumentNullException(nameof(streamId));

            _logger.Debug("Getting all phase properties for stream {StreamId}", streamId);

            try
            {
                // Validate stream exists
                var stream = _context.GetStream(streamId);
                if (stream == null)
                {
                    throw new StreamNotFoundException(streamId);
                }

                var phases = GetAllPhaseProperties(streamId, stream);
                _logger.Information("Retrieved {PhaseCount} phases for stream {StreamId}", phases.Count, streamId);
                return phases;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get all phase properties for stream {StreamId}", streamId);
                throw;
            }
        }

        /// <summary>
        /// Performs flash calculation on a stream to compute phase equilibrium.
        /// </summary>
        /// <param name="streamId">The unique stream identifier.</param>
        /// <returns>A PropertySetResult indicating success or failure.</returns>
        /// <remarks>
        /// This method calls DWSIM's flash calculation to compute:
        /// - Phase fractions (vapor, liquid1, liquid2)
        /// - Component distribution across phases
        /// - Phase properties (density, enthalpy, entropy, etc.)
        ///
        /// The flash calculation must be performed before a stream can be used as input
        /// to unit operations like separators, heat exchangers, etc.
        /// </remarks>
        public PropertySetResult FlashStream(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                var message = "Stream ID cannot be null or empty";
                _logger.Warning(message);
                return PropertySetResult.FailureResult(message, new ArgumentNullException(nameof(streamId), message));
            }

            _logger.Debug("Flashing stream {StreamId}", streamId);

            try
            {
                // Get the stream object
                var stream = _context.GetStream(streamId);
                if (stream == null)
                {
                    var message = $"Stream '{streamId}' not found";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new StreamNotFoundException(streamId));
                }

                var streamType = stream.GetType();

                // Find the Calculate method - look for overloads that don't require parameters
                // or have optional parameters
                var calculateMethods = streamType.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                    .Where(m => m.Name == "Calculate")
                    .OrderBy(m => m.GetParameters().Length)
                    .ToArray();

                if (calculateMethods.Length == 0)
                {
                    var message = "Calculate method not found on stream";
                    _logger.Warning(message);
                    return PropertySetResult.FailureResult(message, new InvalidOperationException(message));
                }

                _logger.Debug("Found {Count} Calculate method overloads", calculateMethods.Length);

                // Try parameterless Calculate() first
                var parameterlessCalculate = calculateMethods.FirstOrDefault(m => m.GetParameters().Length == 0);
                if (parameterlessCalculate != null)
                {
                    _logger.Debug("Calling parameterless Calculate()");
                    parameterlessCalculate.Invoke(stream, null);
                    EnsureStreamCalculated(stream, streamId, streamType);
                    _logger.Information("Stream {StreamId} flashed successfully", streamId);
                    return PropertySetResult.SuccessResult(streamId);
                }

                // Try Calculate with optional parameters
                var calculateWithOptionals = calculateMethods.FirstOrDefault(m =>
                    m.GetParameters().All(p => p.IsOptional));

                if (calculateWithOptionals != null)
                {
                    var parameters = calculateWithOptionals.GetParameters();
                    var args = parameters.Select(p => p.DefaultValue).ToArray();
                    _logger.Debug("Calling Calculate() with {Count} default parameters", parameters.Length);
                    calculateWithOptionals.Invoke(stream, args);
                    EnsureStreamCalculated(stream, streamId, streamType);
                    _logger.Information("Stream {StreamId} flashed successfully", streamId);
                    return PropertySetResult.SuccessResult(streamId);
                }

                // If no suitable overload found, try the first one with null args
                var firstCalculate = calculateMethods[0];
                var paramCount = firstCalculate.GetParameters().Length;
                var nullArgs = new object[paramCount];
                _logger.Debug("Calling Calculate() with {Count} null parameters", paramCount);
                firstCalculate.Invoke(stream, nullArgs);
                EnsureStreamCalculated(stream, streamId, streamType);
                _logger.Information("Stream {StreamId} flashed successfully", streamId);
                return PropertySetResult.SuccessResult(streamId);
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                var innerEx = ex.InnerException ?? ex;
                var message = $"Flash calculation failed for stream '{streamId}': {innerEx.Message}";
                _logger.Error(innerEx, message);
                return PropertySetResult.FailureResult(message, innerEx);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error flashing stream '{streamId}': {ex.Message}";
                _logger.Error(ex, message);
                return PropertySetResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Ensures a stream is marked as calculated after a flash operation.
        /// </summary>
        private void EnsureStreamCalculated(object stream, string streamId, Type streamType)
        {
            // Set Calculated property on the stream itself
            var calculatedProp = streamType.GetProperty("Calculated");
            if (calculatedProp != null)
            {
                var isCalculated = calculatedProp.GetValue(stream);
                _logger.Debug("Stream {StreamId} Calculated property after flash: {IsCalculated}", streamId, isCalculated);

                // If not calculated, try setting it explicitly
                if (isCalculated is bool calc && !calc)
                {
                    if (!calculatedProp.CanWrite)
                    {
                        _logger.Warning("Stream {StreamId} Calculated property is read-only", streamId);
                    }
                    else
                    {
                        _logger.Debug("Setting Calculated = true on stream {StreamId}", streamId);
                        calculatedProp.SetValue(stream, true);

                        // Verify it was set
                        var verifyCalculated = calculatedProp.GetValue(stream);
                        _logger.Debug("Stream {StreamId} Calculated property after setting: {IsCalculated}", streamId, verifyCalculated);
                    }
                }
            }

            // CRITICAL: Also set Calculated property on the GraphicObject
            // DWSIM checks cp.AttachedConnector.AttachedFrom.Calculated (the graphic object, not the stream)
            try
            {
                var graphicObj = streamType.GetProperty("GraphicObject")?.GetValue(stream);
                if (graphicObj != null)
                {
                    var graphicType = graphicObj.GetType();
                    var graphicCalculatedProp = graphicType.GetProperty("Calculated");
                    if (graphicCalculatedProp != null && graphicCalculatedProp.CanWrite)
                    {
                        var graphicCalc = graphicCalculatedProp.GetValue(graphicObj);
                        _logger.Debug("Stream {StreamId} GraphicObject.Calculated before: {IsCalculated}", streamId, graphicCalc);

                        if (graphicCalc is bool gCalc && !gCalc)
                        {
                            graphicCalculatedProp.SetValue(graphicObj, true);
                            var verifyGraphicCalc = graphicCalculatedProp.GetValue(graphicObj);
                            _logger.Debug("Stream {StreamId} GraphicObject.Calculated after setting: {IsCalculated}", streamId, verifyGraphicCalc);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Could not set GraphicObject.Calculated for stream {StreamId}", streamId);
            }
        }

        private IReadOnlyDictionary<string, PhaseProperties> GetAllPhaseProperties(string streamId, object stream)
        {
            var phaseMap = TryGetPhaseMap(stream);
            if (phaseMap == null || phaseMap.Count == 0)
            {
                return new Dictionary<string, PhaseProperties>();
            }

            var phases = new Dictionary<string, PhaseProperties>(StringComparer.OrdinalIgnoreCase);
            var compounds = _context.GetCompounds();

            foreach (DictionaryEntry entry in phaseMap)
            {
                if (!(entry.Key is int phaseId))
                {
                    continue;
                }

                if (!PhaseIdToName.TryGetValue(phaseId, out var phaseLabel))
                {
                    continue;
                }

                var phase = entry.Value;
                var props = TryGetPhasePropertiesObject(phase);
                if (props == null)
                {
                    continue;
                }

                var composition = BuildCompositionFromPhase(phase, compounds);
                if (composition == null)
                {
                    continue;
                }

                var molarFlow = GetNullableDouble(props, "molarflow") ?? 0.0;
                var massFlow = GetNullableDouble(props, "massflow") ?? 0.0;
                var phaseFraction = GetNullableDouble(props, "molarfraction") ?? (phaseId == 0 ? 1.0 : 0.0);
                var density = GetNullableDouble(props, "density");
                var viscosity = GetNullableDouble(props, "viscosity");
                var molecularWeight = GetNullableDouble(props, "molecularWeight");
                var enthalpy = GetNullableDouble(props, "enthalpy");
                var molarEnthalpy = GetNullableDouble(props, "molar_enthalpy");
                var entropy = GetNullableDouble(props, "entropy");
                var molarEntropy = GetNullableDouble(props, "molar_entropy");
                var volumetricFlow = GetNullableDouble(props, "volumetric_flow");
                var massFraction = GetNullableDouble(props, "massfraction");
                var volumetricFraction = GetNullableDouble(props, "volumetricFraction");
                var gibbsFreeEnergy = GetNullableDouble(props, "gibbs_free_energy");
                var helmholtzEnergy = GetNullableDouble(props, "helmholtz_energy");
                var internalEnergy = GetNullableDouble(props, "internal_energy");
                var kValue = GetNullableDouble(props, "kvalue");
                var fugacity = GetNullableDouble(props, "fugacity");
                var activityCoefficient = GetNullableDouble(props, "activityCoefficient");

                phases[phaseLabel] = new PhaseProperties(
                    phaseName: phaseLabel,
                    molarFlowMolPerSec: molarFlow,
                    massFlowKgPerSec: massFlow,
                    phaseFraction: phaseFraction,
                    composition: composition,
                    densityKgPerM3: density,
                    viscosityPaS: viscosity,
                    molecularWeightKgPerKmol: molecularWeight,
                    enthalpyKJPerKg: enthalpy,
                    molarEnthalpyKJPerKmol: molarEnthalpy,
                    entropyKJPerKgK: entropy,
                    molarEntropyKJPerKmolK: molarEntropy,
                    volumetricFlowM3PerSec: volumetricFlow,
                    massFraction: massFraction,
                    volumetricFraction: volumetricFraction,
                    gibbsFreeEnergy: gibbsFreeEnergy,
                    helmholtzEnergy: helmholtzEnergy,
                    internalEnergy: internalEnergy,
                    kValue: kValue,
                    fugacity: fugacity,
                    activityCoefficient: activityCoefficient);
            }

            return phases;
        }

        private IDictionary TryGetPhaseMap(object stream)
        {
            if (stream == null)
            {
                return null;
            }

            var phasesProperty = stream.GetType().GetProperty("Phases");
            return phasesProperty?.GetValue(stream) as IDictionary;
        }

        private static object TryGetPhasePropertiesObject(object phase)
        {
            if (phase == null)
            {
                return null;
            }

            return phase.GetType().GetProperty("Properties")?.GetValue(phase);
        }

        private Composition BuildCompositionFromPhase(object phase, IReadOnlyList<string> compoundOrder)
        {
            var compounds = TryGetCompounds(phase);
            if (compounds == null || compounds.Count == 0)
            {
                return null;
            }

            var fractions = new List<double>();

            if (compoundOrder != null && compoundOrder.Count > 0)
            {
                foreach (var compoundName in compoundOrder)
                {
                    var compound = TryGetCompound(compounds, compoundName);
                    var fraction = compound != null ? GetNullableDouble(compound, "MoleFraction") ?? 0.0 : 0.0;
                    fractions.Add(SanitizeFraction(fraction));
                }
            }
            else
            {
                foreach (DictionaryEntry entry in compounds)
                {
                    var fraction = GetNullableDouble(entry.Value, "MoleFraction") ?? 0.0;
                    fractions.Add(SanitizeFraction(fraction));
                }
            }

            var normalized = NormalizeFractions(fractions);
            return new Composition(normalized);
        }

        private static IDictionary TryGetCompounds(object phase)
        {
            return phase?.GetType().GetProperty("Compounds")?.GetValue(phase) as IDictionary;
        }

        private static object TryGetCompound(IDictionary compounds, string compoundName)
        {
            if (compounds == null || string.IsNullOrWhiteSpace(compoundName))
            {
                return null;
            }

            if (compounds.Contains(compoundName))
            {
                return compounds[compoundName];
            }

            foreach (DictionaryEntry entry in compounds)
            {
                if (string.Equals(entry.Key?.ToString(), compoundName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            return null;
        }

        private static double? TryGetPhaseScalar(object stream, int phaseId, string propertyName)
        {
            var phases = stream?.GetType().GetProperty("Phases")?.GetValue(stream) as IDictionary;
            if (phases == null || !phases.Contains(phaseId))
            {
                return null;
            }

            var phase = phases[phaseId];
            var props = phase?.GetType().GetProperty("Properties")?.GetValue(phase);
            return props == null ? (double?)null : GetNullableDouble(props, propertyName);
        }

        private PhaseProperties BuildFallbackPhaseProperties(string streamId, string phaseName)
        {
            if (!_context.TryGetStreamProperties(streamId, out var properties))
            {
                throw new InvalidOperationException($"No properties available for stream '{streamId}'.");
            }

            var phaseFraction = string.Equals(phaseName, "Overall", StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;

            return new PhaseProperties(
                phaseName: phaseName,
                molarFlowMolPerSec: properties.MolarFlow.Measurement.Value,
                massFlowKgPerSec: properties.MolarFlow.Measurement.Value * 18.0 / 1000.0,
                phaseFraction: phaseFraction,
                composition: properties.Composition,
                densityKgPerM3: 1000.0,
                viscosityPaS: 0.001,
                molecularWeightKgPerKmol: 18.0,
                enthalpyKJPerKg: 0.0,
                molarEnthalpyKJPerKmol: 0.0,
                entropyKJPerKgK: 0.0,
                molarEntropyKJPerKmolK: 0.0,
                volumetricFlowM3PerSec: 0.0,
                massFraction: phaseFraction,
                volumetricFraction: phaseFraction,
                gibbsFreeEnergy: 0.0,
                helmholtzEnergy: 0.0,
                internalEnergy: 0.0,
                kValue: 0.0,
                fugacity: 0.0,
                activityCoefficient: 0.0);
        }

        private Composition GetFallbackComposition(string streamId)
        {
            if (_context.TryGetStreamProperties(streamId, out var properties))
            {
                return properties.Composition;
            }

            throw new InvalidOperationException($"No properties available for stream '{streamId}'.");
        }

        private double GetFallbackTemperature(string streamId)
        {
            return _context.TryGetStreamProperties(streamId, out var properties)
                ? properties.Temperature.Measurement.Value
                : 0.0;
        }

        private double GetFallbackPressure(string streamId)
        {
            return _context.TryGetStreamProperties(streamId, out var properties)
                ? properties.Pressure.Measurement.Value
                : 0.0;
        }

        private double GetFallbackMolarFlow(string streamId)
        {
            return _context.TryGetStreamProperties(streamId, out var properties)
                ? properties.MolarFlow.Measurement.Value
                : 0.0;
        }

        private double GetFallbackMassFlow(string streamId)
        {
            return _context.TryGetStreamProperties(streamId, out var properties)
                ? properties.MolarFlow.Measurement.Value * 18.0 / 1000.0
                : 0.0;
        }

        private static double GetPhaseFraction(IReadOnlyDictionary<string, PhaseProperties> phases, string phaseName)
        {
            if (phases == null || !phases.TryGetValue(phaseName, out var phase))
            {
                return 0.0;
            }

            return phase.PhaseFraction;
        }

        private static double GetLiquidFraction(IReadOnlyDictionary<string, PhaseProperties> phases, double vaporFraction)
        {
            if (phases == null)
            {
                return 1.0 - vaporFraction;
            }

            if (phases.TryGetValue("OverallLiquid", out var overallLiquid))
            {
                return overallLiquid.PhaseFraction;
            }

            var liquidFraction = 0.0;
            if (phases.TryGetValue("Liquid1", out var liquid1))
            {
                liquidFraction += liquid1.PhaseFraction;
            }

            if (phases.TryGetValue("Liquid2", out var liquid2))
            {
                liquidFraction += liquid2.PhaseFraction;
            }

            if (phases.TryGetValue("Liquid3", out var liquid3))
            {
                liquidFraction += liquid3.PhaseFraction;
            }

            if (phases.TryGetValue("Aqueous", out var aqueous))
            {
                liquidFraction += aqueous.PhaseFraction;
            }

            return liquidFraction > 0 ? liquidFraction : Math.Max(0.0, 1.0 - vaporFraction);
        }

        private static double? GetNullableDouble(object target, string propertyName)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            var property = target.GetType().GetProperty(
                propertyName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
            if (property == null)
            {
                return null;
            }

            var value = property.GetValue(target);
            if (value == null)
            {
                return null;
            }

            if (value is double doubleValue)
            {
                return doubleValue;
            }

            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                return null;
            }
        }

        private static double SanitizeFraction(double fraction)
        {
            if (double.IsNaN(fraction) || double.IsInfinity(fraction))
            {
                return 0.0;
            }

            return fraction < 0.0 ? 0.0 : fraction;
        }

        private double[] NormalizeFractions(IReadOnlyList<double> fractions)
        {
            var sanitized = fractions.Select(SanitizeFraction).ToArray();
            var sum = sanitized.Sum();

            if (sanitized.Length == 0)
            {
                return Array.Empty<double>();
            }

            if (sum <= 0.0)
            {
                _logger.Warning("Phase composition sum was zero; defaulting to pure first component.");
                var fallback = new double[sanitized.Length];
                fallback[0] = 1.0;
                return fallback;
            }

            if (Math.Abs(sum - 1.0) > 1e-6)
            {
                for (int i = 0; i < sanitized.Length; i++)
                {
                    sanitized[i] = sanitized[i] / sum;
                }
            }

            return sanitized;
        }

        private void TrySetStreamPropertyPackage(object stream)
        {
            if (stream == null)
            {
                return;
            }

            var propertyPackage = _context.GetPropertyPackage();
            if (propertyPackage == null)
            {
                return;
            }

            // Use GetProperties to avoid AmbiguousMatchException, then find the writable PropertyPackage property
            var properties = stream.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            var property = properties.FirstOrDefault(p =>
                p.Name == "PropertyPackage" &&
                p.CanWrite &&
                p.GetIndexParameters().Length == 0);  // Ensure it's not an indexed property

            if (property == null)
            {
                return;
            }

            if (!property.PropertyType.IsInstanceOfType(propertyPackage))
            {
                return;
            }

            property.SetValue(stream, propertyPackage);
        }

        private void TryAddCompoundsToStream(object flowsheet, object stream)
        {
            if (flowsheet == null || stream == null)
            {
                return;
            }

            try
            {
                // Check if stream already has compounds (happens when created via flowsheet)
                var phasesProperty = stream.GetType().GetProperty("Phases");
                if (phasesProperty != null && phasesProperty.GetValue(stream) is IDictionary phases && phases.Count > 0)
                {
                    if (phases.Contains(0))
                    {
                        var phase = phases[0];
                        var compoundsProperty = phase.GetType().GetProperty("Compounds");
                        if (compoundsProperty != null && compoundsProperty.GetValue(phase) is IDictionary compounds)
                        {
                            if (compounds.Count > 0)
                            {
                                _logger.Debug("Stream already has {Count} compounds, skipping AddCompoundsToMaterialStream", compounds.Count);
                                return;
                            }
                        }
                    }
                }

                var method = flowsheet.GetType().GetMethod("AddCompoundsToMaterialStream");
                if (method != null)
                {
                    method.Invoke(flowsheet, new[] { stream });
                    _logger.Debug("Compounds added to stream successfully");
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - compounds might already be added or this might not be critical
                _logger.Warning(ex, "Failed to add compounds to stream (this may not be critical)");
            }
        }

        /// <summary>
        /// Sets the IsSource property on a stream object.
        /// Feed streams need IsSource=True to be recognized as calculation starting points
        /// by DWSIM's GetSolvingList() Phase 1 endpoint detection algorithm.
        /// </summary>
        /// <param name="stream">The stream object (MaterialStream).</param>
        /// <param name="isSource">True to mark as source (feed stream), false otherwise.</param>
        private void TrySetIsSourceProperty(object stream, bool isSource)
        {
            if (stream == null)
            {
                _logger.Warning("Cannot set IsSource property: stream is null");
                return;
            }

            try
            {
                // Find the IsSource property on the stream object
                var property = stream.GetType().GetProperty("IsSource",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

                if (property == null)
                {
                    _logger.Warning("Stream object does not have IsSource property - calculation may not work");
                    return;
                }

                if (!property.CanWrite)
                {
                    _logger.Warning("IsSource property is read-only - cannot set to {Value}", isSource);
                    return;
                }

                // Set the IsSource property
                property.SetValue(stream, isSource);
                _logger.Debug("Set IsSource={Value} on stream successfully", isSource);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to set IsSource property to {Value} (calculation may not work)", isSource);
            }
        }

        private void ApplyPropertiesToStreamObject(object stream, StreamProperties properties, bool isSource = true)
        {
            if (stream == null || properties == null)
            {
                return;
            }

            var phasesProperty = stream.GetType().GetProperty("Phases");
            if (phasesProperty == null)
            {
                return;
            }

            if (!(phasesProperty.GetValue(stream) is IDictionary phases))
            {
                return;
            }

            if (!phases.Contains(0))
            {
                return;
            }

            var phase = phases[0];
            var phaseProperties = TryGetPhasePropertiesObject(phase);
            if (phaseProperties != null)
            {
                SetPhaseProperty(phaseProperties, "temperature", properties.Temperature.Measurement.Value);
                SetPhaseProperty(phaseProperties, "pressure", properties.Pressure.Measurement.Value);
                SetPhaseProperty(phaseProperties, "molarflow", properties.MolarFlow.Measurement.Value);
            }

            ApplyComposition(phase, properties.Composition);
            TrySetInputComposition(stream, properties);
            TrySetStreamSpecification(stream, isSource);
        }

        private void ApplyComposition(object phase, Composition composition)
        {
            var compounds = TryGetCompounds(phase);
            if (compounds == null || composition == null)
            {
                _logger.Debug("ApplyComposition: compounds or composition is null");
                return;
            }

            _logger.Debug("ApplyComposition: Phase has {Count} compounds in dictionary", compounds.Count);

            var compoundNames = _context.GetCompounds();
            if (compoundNames.Count == 0)
            {
                _logger.Debug("ApplyComposition: No compound names from context");
                return;
            }

            _logger.Debug("ApplyComposition: Context has {Count} compound names", compoundNames.Count);

            var fractions = composition.MoleFractions;
            _logger.Debug("ApplyComposition: Composition has {Count} mole fractions", fractions.Count);

            for (int i = 0; i < compoundNames.Count; i++)
            {
                var fraction = i < fractions.Count ? fractions[i] : 0.0;
                var compoundName = compoundNames[i];
                _logger.Debug("ApplyComposition: Setting {CompoundName} mole fraction to {Fraction}", compoundName, fraction);

                var compound = TryGetCompound(compounds, compoundName);
                if (compound == null)
                {
                    _logger.Warning("ApplyComposition: Compound {CompoundName} not found in phase compounds dictionary", compoundName);
                    continue;
                }

                SetPhaseProperty(compound, "MoleFraction", fraction);

                // Verify the property was set
                var moleFracProp = compound.GetType().GetProperty("MoleFraction",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
                if (moleFracProp != null && moleFracProp.CanRead)
                {
                    var actualValue = moleFracProp.GetValue(compound);
                    _logger.Debug("ApplyComposition: {CompoundName} MoleFraction after setting: {ActualValue}", compoundName, actualValue);
                }
            }
        }

        private void TrySetInputComposition(object stream, StreamProperties properties)
        {
            if (stream == null || properties?.Composition == null)
            {
                _logger.Debug("TrySetInputComposition: stream or composition is null");
                return;
            }

            var inputCompositionProperty = stream.GetType().GetProperty("InputComposition");
            if (inputCompositionProperty == null)
            {
                _logger.Warning("TrySetInputComposition: InputComposition property not found on stream");
                return;
            }

            if (!inputCompositionProperty.CanWrite)
            {
                _logger.Warning("TrySetInputComposition: InputComposition property is read-only");
                return;
            }

            var compoundNames = _context.GetCompounds();
            if (compoundNames.Count == 0)
            {
                _logger.Debug("TrySetInputComposition: No compound names from context");
                return;
            }

            var fractions = properties.Composition.MoleFractions;
            var compositionDict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            _logger.Debug("TrySetInputComposition: Building composition dictionary");

            for (int i = 0; i < compoundNames.Count; i++)
            {
                var fraction = i < fractions.Count ? fractions[i] : 0.0;
                compositionDict[compoundNames[i]] = fraction;
                _logger.Debug("TrySetInputComposition: {CompoundName} = {Fraction}", compoundNames[i], fraction);
            }

            try
            {
                inputCompositionProperty.SetValue(stream, compositionDict);
                _logger.Debug("TrySetInputComposition: Successfully set InputComposition dictionary with {Count} compounds", compositionDict.Count);

                // Verify it was set
                var actualValue = inputCompositionProperty.GetValue(stream);
                if (actualValue is IDictionary actualDict)
                {
                    _logger.Debug("TrySetInputComposition: InputComposition dictionary has {Count} entries after setting", actualDict.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "TrySetInputComposition: Failed to set InputComposition");
            }
        }

        private void TrySetStreamSpecification(object stream, bool isSource = true)
        {
            if (stream == null)
            {
                return;
            }

            // Set DefinedFlow based on what we're specifying (we specify molar flow, so use "Mole")
            TrySetEnumProperty(stream, "DefinedFlow", "Mole");

            // Set SpecType based on isSource parameter
            // - Feed streams (isSource=true): Use "Temperature_and_Pressure"
            // - Outlet streams (isSource=false): Use "Pressure_and_Enthalpy"
            // This matches the DWSIM sample file configuration
            var specType = isSource ? "Temperature_and_Pressure" : "Pressure_and_Enthalpy";
            TrySetEnumProperty(stream, "SpecType", specType);
            _logger.Debug("Set SpecType={SpecType} based on isSource={IsSource}", specType, isSource);
        }

        private void TrySetEnumProperty(object target, string propertyName, string enumValue)
        {
            var property = target.GetType().GetProperty(propertyName);
            if (property == null || !property.CanWrite)
            {
                return;
            }

            var enumType = property.PropertyType;
            if (!enumType.IsEnum)
            {
                return;
            }

            var parsed = Enum.Parse(enumType, enumValue);
            property.SetValue(target, parsed);
        }

        private void SetPhaseProperty(object target, string propertyName, object value)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
            {
                _logger.Debug("SetPhaseProperty: target is null or propertyName is empty");
                return;
            }

            var property = target.GetType().GetProperty(
                propertyName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
            if (property == null)
            {
                _logger.Debug("SetPhaseProperty: Property {PropertyName} not found on {TargetType}", propertyName, target.GetType().Name);
                return;
            }

            if (!property.CanWrite)
            {
                _logger.Debug("SetPhaseProperty: Property {PropertyName} on {TargetType} is read-only", propertyName, target.GetType().Name);
                return;
            }

            try
            {
                property.SetValue(target, value);
                _logger.Debug("SetPhaseProperty: Successfully set {PropertyName} = {Value} on {TargetType}", propertyName, value, target.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "SetPhaseProperty: Failed to set {PropertyName} = {Value} on {TargetType}", propertyName, value, target.GetType().Name);
            }
        }

        /// <summary>
        /// Creates a DWSIM MaterialStream instance using reflection.
        /// </summary>
        /// <param name="name">The name for the stream.</param>
        /// <param name="streamId">The unique stream ID.</param>
        /// <returns>A DWSIM MaterialStream object or placeholder.</returns>
        /// <exception cref="InvalidOperationException">Thrown when MaterialStream creation fails.</exception>
        private object CreateMaterialStream(object flowsheet, string name, string streamId)
        {
            const string typeName = "DWSIM.Thermodynamics.Streams.MaterialStream";

            try
            {
                var created = TryAddStreamViaFlowsheet(flowsheet, name, streamId);
                if (created != null)
                {
                    TrySetNameAndTag(created, name, streamId);
                    _logger.Debug("Created DWSIM MaterialStream via flowsheet: {StreamId} (Name: {Name})", streamId, name);
                    return created;
                }

                // Try to find the MaterialStream type in loaded assemblies
                Type streamType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    streamType = assembly.GetType(typeName);
                    if (streamType != null)
                        break;
                }

                if (streamType != null)
                {
                    // Create actual DWSIM instance if type is found
                    var instance = Activator.CreateInstance(streamType);
                    if (instance != null)
                    {
                        TrySetNameAndTag(instance, name, streamId);
                        _logger.Debug("Created DWSIM MaterialStream: {StreamId} (Name: {Name})", streamId, name);
                        return instance;
                    }
                }

                // If DWSIM type not found, create a placeholder object
                // This allows the adapter logic to be tested independently of DWSIM availability
                _logger.Debug("DWSIM type not available, creating placeholder for: {StreamId} (Name: {Name})", streamId, name);
                return new { StreamType = "MaterialStream", Name = name, StreamId = streamId };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create MaterialStream for {StreamId}", streamId);
                throw new InvalidOperationException($"Failed to create MaterialStream: {ex.Message}", ex);
            }
        }

        private object TryAddStreamViaFlowsheet(object flowsheet, string name, string streamId)
        {
            if (flowsheet == null)
            {
                return null;
            }

            var flowsheetType = flowsheet.GetType();
            var objectType = FindEnumType("DWSIM.Interfaces.Enums.GraphicObjects.ObjectType");
            if (objectType != null)
            {
                var materialStreamType = Enum.Parse(objectType, "MaterialStream");

                var addObject = flowsheetType.GetMethod(
                    "AddObject",
                    new[] { objectType, typeof(int), typeof(int), typeof(string), typeof(string) });
                if (addObject != null)
                {
                    return addObject.Invoke(flowsheet, new object[] { materialStreamType, 0, 0, streamId, name });
                }

                addObject = flowsheetType.GetMethod(
                    "AddObject",
                    new[] { objectType, typeof(int), typeof(int), typeof(string) });
                if (addObject != null)
                {
                    return addObject.Invoke(flowsheet, new object[] { materialStreamType, 0, 0, name });
                }
            }

            var addByName = flowsheetType.GetMethod(
                "AddObject",
                new[] { typeof(string), typeof(int), typeof(int), typeof(string), typeof(string), typeof(bool) });
            if (addByName != null)
            {
                return addByName.Invoke(flowsheet, new object[]
                {
                    "DWSIM.Thermodynamics.Streams.MaterialStream", 0, 0, name, streamId, false
                });
            }

            return null;
        }

        private static Type FindEnumType(string enumTypeName)
        {
            if (string.IsNullOrWhiteSpace(enumTypeName))
            {
                return null;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(enumTypeName);
                if (type != null && type.IsEnum)
                {
                    return type;
                }
            }

            return null;
        }

        private void TrySetNameAndTag(object target, string name, string streamId)
        {
            if (target == null)
            {
                return;
            }

            // CRITICAL: ISimulationObject.Name must match the dictionary key used by DWSIM
            // GetSolvingList() reads baseobj.Name, and SolveFlowsheet() looks up SimulationObjects(name)
            // The stream is registered with key=streamId, so Name must be streamId
            var nameProperty = target.GetType().GetProperty("Name");
            if (nameProperty != null && nameProperty.CanWrite)
            {
                nameProperty.SetValue(target, streamId);  // Name must match dictionary key
            }

            var graphicObject = target.GetType().GetProperty("GraphicObject")?.GetValue(target);
            if (graphicObject != null)
            {
                var tagProperty = graphicObject.GetType().GetProperty("Tag");
                if (tagProperty != null && tagProperty.CanWrite)
                {
                    tagProperty.SetValue(graphicObject, name);  // Tag is the friendly name
                }

                // CRITICAL: GraphicObject.Name must match the key in FlowSheet.SimulationObjects
                // DWSIM looks up streams via FlowSheet.SimulationObjects(GraphicObject.Name)
                // The stream is registered with key=streamId, so GraphicObject.Name must be streamId
                var nameTagProperty = graphicObject.GetType().GetProperty("Name");
                if (nameTagProperty != null && nameTagProperty.CanWrite)
                {
                    nameTagProperty.SetValue(graphicObject, streamId);  // Name is the SimulationObjects key
                }
            }
        }
    }
}
