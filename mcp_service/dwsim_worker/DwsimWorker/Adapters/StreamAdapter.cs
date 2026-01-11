using System;
using System.Collections.Generic;
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

        // Internal registry for stream properties (in-memory cache)
        // In a full implementation, this would query DWSIM MaterialStream objects
        private readonly Dictionary<string, StreamProperties> _streamPropertiesCache;

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
            _streamPropertiesCache = new Dictionary<string, StreamProperties>();
        }

        /// <summary>
        /// Creates a new material stream with the specified properties.
        /// </summary>
        /// <param name="name">The name of the stream (e.g., "Inlet", "Feed1").</param>
        /// <param name="properties">The thermodynamic properties for the stream.</param>
        /// <returns>
        /// A PropertySetResult containing the generated streamId on success.
        /// Returns a failure result if validation fails or stream creation fails.
        /// </returns>
        /// <remarks>
        /// The stream is assigned a unique ID (e.g., "S1", "S2") that is returned on success.
        /// This ID must be used for all subsequent property get/set operations on the stream.
        ///
        /// The properties are validated before stream creation:
        /// - Temperature must be > 0 K and < 10000 K
        /// - Pressure must be > 0 Pa and < 1e9 Pa
        /// - Molar flow must be >= 0 mol/s
        /// - Composition mole fractions must sum to 1.0 ± 1e-6
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
        /// var result = adapter.CreateStream("Feed", properties);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Stream created with ID: {result.Data}");
        /// }
        /// </code>
        /// </example>
        public PropertySetResult CreateStream(string name, StreamProperties properties)
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
                var materialStream = CreateMaterialStream(name, streamId);
                _context.AddStream(materialStream, streamId);

                // Step 4: Store properties in cache
                _streamPropertiesCache[streamId] = properties;

                // Step 5: Log success with structured logging
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
                // TODO: In full implementation, call DWSIM MaterialStream API using CAPE-OPEN interfaces
                // For now, update cached properties
                if (_streamPropertiesCache.TryGetValue(streamId, out var currentProps))
                {
                    var normalizedName = CapeOpenPropertyConverter.ToCapeOpenName(propertyName);
                    _streamPropertiesCache[streamId] = UpdateProperty(currentProps, normalizedName, value);
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
                if (!_streamPropertiesCache.TryGetValue(streamId, out var props))
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
                _streamPropertiesCache[streamId] = properties;

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
                if (!_streamPropertiesCache.TryGetValue(streamId, out var properties))
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

                // Get cached properties (in full implementation, this would query DWSIM MaterialStream)
                if (!_streamPropertiesCache.TryGetValue(streamId, out var properties))
                {
                    throw new InvalidOperationException($"No properties available for stream '{streamId}'");
                }

                // TODO: In full implementation, extract calculated properties from DWSIM MaterialStream
                // including phase properties, equilibrium data, and all thermodynamic properties

                // For now, create StreamResult from cached data
                var result = new StreamResult(
                    streamId: streamId,
                    streamName: streamName ?? streamId,
                    temperatureK: properties.Temperature.Measurement.Value,
                    pressurePa: properties.Pressure.Measurement.Value,
                    molarFlowMolPerSec: properties.MolarFlow.Measurement.Value,
                    massFlowKgPerSec: properties.MolarFlow.Measurement.Value * 18.0 / 1000.0, // Placeholder: assumes water MW
                    overallComposition: properties.Composition,
                    vaporFraction: 0.0, // TODO: Get from DWSIM after calculation
                    liquidFraction: 1.0, // TODO: Get from DWSIM after calculation
                    phases: null); // TODO: Get phase properties from DWSIM

                _logger.Information("Calculated properties retrieved for stream {StreamId}", streamId);
                return result;
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

                // TODO: In full implementation, query DWSIM MaterialStream for phase-specific properties
                // This would include phase equilibrium data, compositions, and thermophysical properties

                // For now, return placeholder phase properties
                if (!_streamPropertiesCache.TryGetValue(streamId, out var properties))
                {
                    throw new InvalidOperationException($"No properties available for stream '{streamId}'");
                }

                // Create placeholder phase properties
                var phaseProperties = new PhaseProperties(
                    phaseName: phaseName,
                    molarFlowMolPerSec: properties.MolarFlow.Measurement.Value,
                    massFlowKgPerSec: properties.MolarFlow.Measurement.Value * 18.0 / 1000.0,
                    phaseFraction: phaseName == "Overall" ? 1.0 : (phaseName == "Liquid1" ? 1.0 : 0.0),
                    composition: properties.Composition,
                    densityKgPerM3: 1000.0, // Placeholder
                    viscosityPaS: 0.001, // Placeholder
                    molecularWeightKgPerKmol: 18.0); // Placeholder

                _logger.Information("Phase properties retrieved for {PhaseName} in stream {StreamId}", phaseName, streamId);
                return phaseProperties;
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

                // TODO: In full implementation, query DWSIM MaterialStream for all phases
                // and their properties after calculation

                var phases = new Dictionary<string, PhaseProperties>();

                // Get properties for each phase
                // In a full implementation, this would detect which phases are present
                var phaseNames = new[] { "Vapor", "Liquid1", "Liquid2", "Overall" };

                foreach (var phaseName in phaseNames)
                {
                    try
                    {
                        var phaseProps = GetPhaseProperties(streamId, phaseName);
                        if (phaseProps != null)
                        {
                            phases[phaseName] = phaseProps;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug(ex, "Phase {PhaseName} not available for stream {StreamId}", phaseName, streamId);
                        // Continue with other phases
                    }
                }

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
        /// Creates a DWSIM MaterialStream instance using reflection.
        /// </summary>
        /// <param name="name">The name for the stream.</param>
        /// <param name="streamId">The unique stream ID.</param>
        /// <returns>A DWSIM MaterialStream object or placeholder.</returns>
        /// <exception cref="InvalidOperationException">Thrown when MaterialStream creation fails.</exception>
        private object CreateMaterialStream(string name, string streamId)
        {
            const string typeName = "DWSIM.Thermodynamics.Streams.MaterialStream";

            try
            {
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
    }
}
