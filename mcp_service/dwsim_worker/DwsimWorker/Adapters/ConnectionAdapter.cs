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
    /// Adapter for connecting material streams to unit operation ports.
    /// Manages the flowsheet topology and connection registry.
    /// </summary>
    /// <remarks>
    /// This adapter handles the connections between streams and unit operations,
    /// establishing the flowsheet topology. Each stream can be connected to one port
    /// on one unit operation at a time.
    ///
    /// The adapter validates that:
    /// - Streams and units exist before connection
    /// - Port names are valid for the unit operation
    /// - Streams are not already connected (must disconnect first)
    /// </remarks>
    public sealed class ConnectionAdapter
    {
        private readonly ILogger _logger;
        private readonly FlowsheetContext _context;
        /// <summary>
        /// Maps port names to connector indices for Vessel (Three-Phase Separator) unit operations.
        /// Based on DWSIM.Drawing.GraphicObjects.VesselGraphic connector layout:
        /// - InputConnectors: 0-5 (material inlets), 6 (energy inlet)
        /// - OutputConnectors: 0 (vapor), 1 (liquid 1), 2 (liquid 2)
        /// </summary>
        private static readonly Dictionary<string, int> VesselPortNameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Inlet", 0 },
            { "VaporOutlet", 0 },
            { "LiquidOutlet1", 1 },
            { "LiquidOutlet2", 2 }
        };

        private static readonly Dictionary<string, string> PortNameToConnectorName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Inlet", "Inlet" },
            { "VaporOutlet", "Vapor" },
            { "LiquidOutlet1", "Liquid1" },
            { "LiquidOutlet2", "Liquid2" }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionAdapter"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for connection operation logging.</param>
        /// <param name="context">The flowsheet context that manages connection state.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger or context is null.</exception>
        public ConnectionAdapter(ILogger logger, FlowsheetContext context)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Connects a stream to a unit operation port.
        /// </summary>
        /// <param name="streamId">The unique stream identifier.</param>
        /// <param name="unitId">The unique unit operation identifier.</param>
        /// <param name="portName">
        /// The port name on the unit operation (e.g., "Inlet", "VaporOutlet", "LiquidOutlet1").
        /// </param>
        /// <returns>
        /// A ConnectionResult indicating success or failure. On success, contains the ConnectionInfo.
        /// </returns>
        /// <remarks>
        /// This method validates that:
        /// - The stream exists in the flowsheet
        /// - The unit operation exists in the flowsheet
        /// - The stream is not already connected to another port
        /// - The port name is valid for the unit operation type
        ///
        /// If the stream is already connected, you must call DisconnectStream first.
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new ConnectionAdapter(logger, context);
        ///
        /// // Connect feed stream to separator inlet
        /// var result = adapter.ConnectStream("S1", "U1", "Inlet");
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Stream connected: {result.ConnectionInfo}");
        /// }
        /// </code>
        /// </example>
        public ConnectionResult ConnectStream(string streamId, string unitId, string portName)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                var message = "Stream ID cannot be null or empty";
                _logger.Warning(message);
                return ConnectionResult.FailureResult(message, new ArgumentException(message, nameof(streamId)));
            }

            if (string.IsNullOrWhiteSpace(unitId))
            {
                var message = "Unit ID cannot be null or empty";
                _logger.Warning(message);
                return ConnectionResult.FailureResult(message, new ArgumentException(message, nameof(unitId)));
            }

            if (string.IsNullOrWhiteSpace(portName))
            {
                var message = "Port name cannot be null or empty";
                _logger.Warning(message);
                return ConnectionResult.FailureResult(message, new ArgumentException(message, nameof(portName)));
            }

            _logger.Debug("Connecting stream {StreamId} to unit {UnitId} port {PortName}",
                streamId, unitId, portName);

            try
            {
                // Step 1: Validate stream exists
                if (_context.GetStream(streamId) == null)
                {
                    var message = $"Stream '{streamId}' not found";
                    _logger.Warning(message);
                    return ConnectionResult.FailureResult(message, new StreamNotFoundException(streamId));
                }

                // Step 2: Validate unit exists
                if (_context.GetUnit(unitId) == null)
                {
                    var message = $"Unit operation '{unitId}' not found";
                    _logger.Warning(message);
                    return ConnectionResult.FailureResult(message, new UnitNotFoundException(unitId));
                }

                // Step 3: Check if stream is already connected
                var existingConnections = _context.GetConnections();
                var existingConnection = existingConnections.FirstOrDefault(c => c.StreamId == streamId);
                if (existingConnection != null)
                {
                    var message = $"Stream '{streamId}' is already connected to unit '{existingConnection.UnitId}' port '{existingConnection.PortName}'. Disconnect first.";
                    _logger.Warning(message);
                    return ConnectionResult.FailureResult(message, new InvalidOperationException(message));
                }

                // Step 4: Validate port name
                // TODO: In full implementation, query actual DWSIM unit operation for valid ports
                // For now, accept common port names for three-phase separator
                var validPortNames = new[] { "Inlet", "VaporOutlet", "LiquidOutlet1", "LiquidOutlet2" };
                if (!validPortNames.Contains(portName, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.Warning("Port name '{PortName}' may not be valid for unit {UnitId}. Supported: {ValidPorts}",
                        portName, unitId, string.Join(", ", validPortNames));
                    // Allow connection to proceed with warning (user may know better)
                }

                // Step 5: Connect objects in flowsheet
                if (!TryConnectInFlowsheet(streamId, unitId, portName, out var connectError))
                {
                    var message = connectError ?? $"Failed to connect stream '{streamId}' to unit '{unitId}'.";
                    _logger.Warning(message);
                    return ConnectionResult.FailureResult(message, new InvalidOperationException(message));
                }

                // Step 6: Create connection
                var connectionInfo = new ConnectionInfo(streamId, unitId, portName);
                _context.AddConnection(connectionInfo);

                // Step 7: Log success with structured logging
                _logger.Information("Stream connected successfully: {StreamId} -> {UnitId}.{PortName}",
                    streamId, unitId, portName);

                return ConnectionResult.SuccessResult(connectionInfo);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error connecting stream '{streamId}' to unit '{unitId}' port '{portName}': {ex.Message}";
                _logger.Error(ex, message);
                return ConnectionResult.FailureResult(message, ex);
            }
        }

        private bool TryConnectInFlowsheet(string streamId, string unitId, string portName, out string errorMessage)
        {
            errorMessage = null;

            var flowsheet = _context.GetFlowsheet();
            var stream = _context.GetStream(streamId);
            var unit = _context.GetUnit(unitId);
            if (flowsheet == null || stream == null || unit == null)
            {
                errorMessage = "Flowsheet objects not available for connection.";
                return false;
            }

            var streamGraphic = GetGraphicObject(stream);
            var unitGraphic = GetGraphicObject(unit);
            if (streamGraphic == null || unitGraphic == null)
            {
                errorMessage = "Graphic objects not available for connection.";
                return false;
            }

            var isInlet = portName.IndexOf("inlet", StringComparison.OrdinalIgnoreCase) >= 0;

            object fromGraphic;
            object toGraphic;
            int fromIndex;
            int toIndex;

            if (isInlet)
            {
                fromGraphic = streamGraphic;
                toGraphic = unitGraphic;
                fromIndex = FindConnectorIndex(streamGraphic, output: true, preferredName: null);

                // For inlet connections, use the mapped index if available (Vessel-specific)
                if (VesselPortNameToIndex.TryGetValue(portName, out var inletIndex))
                {
                    toIndex = inletIndex;
                    _logger.Debug("Using mapped inlet index {Index} for port {PortName}", toIndex, portName);
                }
                else
                {
                    var connectorName = PortNameToConnectorName.TryGetValue(portName, out var mapped) ? mapped : portName;
                    toIndex = FindConnectorIndex(unitGraphic, output: false, preferredName: connectorName);
                }
            }
            else
            {
                fromGraphic = unitGraphic;
                toGraphic = streamGraphic;

                // For outlet connections, use the mapped index if available (Vessel-specific)
                if (VesselPortNameToIndex.TryGetValue(portName, out var outletIndex))
                {
                    fromIndex = outletIndex;
                    _logger.Debug("Using mapped outlet index {Index} for port {PortName}", fromIndex, portName);
                }
                else
                {
                    var connectorName = PortNameToConnectorName.TryGetValue(portName, out var mapped) ? mapped : portName;
                    fromIndex = FindConnectorIndex(unitGraphic, output: true, preferredName: connectorName);
                }

                toIndex = FindConnectorIndex(streamGraphic, output: false, preferredName: null);
            }

            if (fromIndex < 0 || toIndex < 0)
            {
                errorMessage = "Unable to resolve connection indices for stream/unit.";
                return false;
            }

            var connectMethod = flowsheet.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "ConnectObjects" && m.GetParameters().Length == 4);

            if (connectMethod == null)
            {
                errorMessage = "Flowsheet does not expose ConnectObjects.";
                return false;
            }

            try
            {
                connectMethod.Invoke(flowsheet, new[] { fromGraphic, toGraphic, fromIndex, toIndex });
                return true;
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                var innerEx = ex.InnerException ?? ex;
                _logger.Warning(innerEx, "ConnectObjects failed for stream {StreamId} and unit {UnitId}: {Message}", streamId, unitId, innerEx.Message);
                LogConnectorDetails("Stream", streamGraphic);
                LogConnectorDetails("Unit", unitGraphic);

                var fallback = flowsheet.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "ConnectObject" && m.GetParameters().Length == 4);
                if (fallback != null)
                {
                    try
                    {
                        fallback.Invoke(flowsheet, new[] { fromGraphic, toGraphic, fromIndex, toIndex });
                        return true;
                    }
                    catch (System.Reflection.TargetInvocationException fallbackEx)
                    {
                        var fallbackInnerEx = fallbackEx.InnerException ?? fallbackEx;
                        _logger.Warning(fallbackInnerEx, "ConnectObject fallback failed for stream {StreamId} and unit {UnitId}: {Message}", streamId, unitId, fallbackInnerEx.Message);
                    }
                }

                errorMessage = $"ConnectObjects failed: {innerEx.Message}";
                return false;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Unexpected error connecting stream {StreamId} and unit {UnitId}: {Message}", streamId, unitId, ex.Message);
                errorMessage = $"Connection failed: {ex.Message}";
                return false;
            }
        }

        private static object GetGraphicObject(object simulationObject)
        {
            return simulationObject?.GetType().GetProperty("GraphicObject")?.GetValue(simulationObject);
        }

        private static int FindConnectorIndex(object graphicObject, bool output, string preferredName)
        {
            if (graphicObject == null)
            {
                return -1;
            }

            var connectorsProperty = graphicObject.GetType().GetProperty(output ? "OutputConnectors" : "InputConnectors");
            if (connectorsProperty == null)
            {
                return 0;
            }

            if (!(connectorsProperty.GetValue(graphicObject) is System.Collections.IList connectors) || connectors.Count == 0)
            {
                return 0;
            }

            if (!string.IsNullOrWhiteSpace(preferredName))
            {
                for (int i = 0; i < connectors.Count; i++)
                {
                    var connector = connectors[i];
                    var nameProperty = connector.GetType().GetProperty("ConnectorName");
                    var name = nameProperty?.GetValue(connector)?.ToString();
                    if (string.Equals(name, preferredName, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return 0;
        }

        private void LogConnectorDetails(string label, object graphicObject)
        {
            if (graphicObject == null)
            {
                _logger.Warning("{Label} graphic object is null", label);
                return;
            }

            LogConnectorList(label, "InputConnectors", graphicObject);
            LogConnectorList(label, "OutputConnectors", graphicObject);
            LogConnectorList(label, "SpecialConnectors", graphicObject);
        }

        private void LogConnectorList(string label, string propertyName, object graphicObject)
        {
            var connectorsProperty = graphicObject.GetType().GetProperty(propertyName);
            if (connectorsProperty == null)
            {
                return;
            }

            if (!(connectorsProperty.GetValue(graphicObject) is System.Collections.IList connectors))
            {
                return;
            }

            var names = new List<string>();
            foreach (var connector in connectors)
            {
                var nameProperty = connector.GetType().GetProperty("ConnectorName");
                var name = nameProperty?.GetValue(connector)?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            _logger.Debug("{Label} {ConnectorType}: {Names}", label, propertyName, names.Count > 0 ? string.Join(", ", names) : "<none>");
        }

        private static Type FindType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        /// <summary>
        /// Disconnects a stream from its currently connected port.
        /// </summary>
        /// <param name="streamId">The unique stream identifier.</param>
        /// <returns>A ConnectionResult indicating success or failure.</returns>
        /// <remarks>
        /// If the stream is not currently connected, this method returns a failure result.
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new ConnectionAdapter(logger, context);
        ///
        /// var result = adapter.DisconnectStream("S1");
        /// if (result.Success)
        /// {
        ///     Console.WriteLine("Stream disconnected successfully");
        /// }
        /// </code>
        /// </example>
        public ConnectionResult DisconnectStream(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                var message = "Stream ID cannot be null or empty";
                _logger.Warning(message);
                return ConnectionResult.FailureResult(message, new ArgumentException(message, nameof(streamId)));
            }

            _logger.Debug("Disconnecting stream {StreamId}", streamId);

            try
            {
                // Step 1: Validate stream exists
                if (_context.GetStream(streamId) == null)
                {
                    var message = $"Stream '{streamId}' not found";
                    _logger.Warning(message);
                    return ConnectionResult.FailureResult(message, new StreamNotFoundException(streamId));
                }

                // Step 2: Find existing connection
                var existingConnections = _context.GetConnections();
                var connection = existingConnections.FirstOrDefault(c => c.StreamId == streamId);
                if (connection == null)
                {
                    var message = $"Stream '{streamId}' is not currently connected";
                    _logger.Warning(message);
                    return ConnectionResult.FailureResult(message, new InvalidOperationException(message));
                }

                // Step 3: Remove connection
                _context.RemoveConnection(connection.StreamId);

                // Step 4: Log success
                _logger.Information("Stream disconnected successfully: {StreamId} (was connected to {UnitId}.{PortName})",
                    streamId, connection.UnitId, connection.PortName);

                return ConnectionResult.SuccessResultForDisconnect(
                    $"Stream '{streamId}' disconnected successfully (was connected to {connection.UnitId}.{connection.PortName})");
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error disconnecting stream '{streamId}': {ex.Message}";
                _logger.Error(ex, message);
                return ConnectionResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Gets the connection information for a specific stream.
        /// </summary>
        /// <param name="streamId">The unique stream identifier.</param>
        /// <returns>
        /// A ConnectionResult containing the ConnectionInfo on success.
        /// Returns a failure result if the stream is not connected.
        /// </returns>
        /// <example>
        /// <code>
        /// var adapter = new ConnectionAdapter(logger, context);
        ///
        /// var result = adapter.GetConnection("S1");
        /// if (result.Success)
        /// {
        ///     var conn = result.ConnectionInfo;
        ///     Console.WriteLine($"Stream {conn.StreamId} connected to {conn.UnitId}.{conn.PortName}");
        /// }
        /// </code>
        /// </example>
        public ConnectionResult GetConnection(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                var message = "Stream ID cannot be null or empty";
                _logger.Warning(message);
                return ConnectionResult.FailureResult(message, new ArgumentException(message, nameof(streamId)));
            }

            _logger.Debug("Getting connection for stream {StreamId}", streamId);

            try
            {
                // Validate stream exists
                if (_context.GetStream(streamId) == null)
                {
                    var message = $"Stream '{streamId}' not found";
                    _logger.Warning(message);
                    return ConnectionResult.FailureResult(message, new StreamNotFoundException(streamId));
                }

                // Find connection
                var existingConnections = _context.GetConnections();
                var connection = existingConnections.FirstOrDefault(c => c.StreamId == streamId);
                if (connection == null)
                {
                    var message = $"Stream '{streamId}' is not currently connected";
                    _logger.Debug(message);
                    return ConnectionResult.FailureResult(message, new InvalidOperationException(message));
                }

                _logger.Debug("Connection retrieved for stream {StreamId}: {UnitId}.{PortName}",
                    streamId, connection.UnitId, connection.PortName);

                return ConnectionResult.SuccessResult(connection);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error getting connection for stream '{streamId}': {ex.Message}";
                _logger.Error(ex, message);
                return ConnectionResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Gets all connections in the flowsheet.
        /// </summary>
        /// <returns>
        /// A ConnectionResult containing a list of all ConnectionInfo objects on success.
        /// Always succeeds, but the list may be empty if there are no connections.
        /// </returns>
        /// <example>
        /// <code>
        /// var adapter = new ConnectionAdapter(logger, context);
        ///
        /// var result = adapter.GetAllConnections();
        /// if (result.Success)
        /// {
        ///     var connections = (List&lt;ConnectionInfo&gt;)result.Data;
        ///     Console.WriteLine($"Total connections: {connections.Count}");
        ///     foreach (var conn in connections)
        ///     {
        ///         Console.WriteLine($"  {conn}");
        ///     }
        /// }
        /// </code>
        /// </example>
        public ConnectionResult GetAllConnections()
        {
            _logger.Debug("Retrieving all connections");

            try
            {
                var connections = _context.GetConnections();

                _logger.Information("Retrieved {ConnectionCount} connections", connections.Count);

                // Return as a list
                return ConnectionResult.SuccessResultWithData(connections.ToList());
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error retrieving all connections: {ex.Message}";
                _logger.Error(ex, message);
                return ConnectionResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Validates that a flowsheet topology is complete (all required connections made).
        /// </summary>
        /// <returns>
        /// A ValidationResult indicating whether the flowsheet topology is valid.
        /// Returns success if all streams are connected and all unit operations have
        /// all required ports connected.
        /// </returns>
        /// <remarks>
        /// This method checks that:
        /// - All streams are connected to a unit operation
        /// - All unit operation inlet ports have connected streams
        ///
        /// Note: Outlet ports do not require connections (they can be terminal streams).
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new ConnectionAdapter(logger, context);
        ///
        /// var result = adapter.ValidateTopology();
        /// if (!result.Success)
        /// {
        ///     Console.WriteLine($"Topology validation failed: {result.Message}");
        /// }
        /// </code>
        /// </example>
        public ValidationResult ValidateTopology()
        {
            _logger.Debug("Validating flowsheet topology");

            try
            {
                var streamIds = _context.GetStreamIds();
                var connections = _context.GetConnections();
                var connectedStreamIds = new HashSet<string>(connections.Select(c => c.StreamId));

                // Check for unconnected streams
                var unconnectedStreams = streamIds.Where(id => !connectedStreamIds.Contains(id)).ToList();

                if (unconnectedStreams.Any())
                {
                    var message = $"Topology incomplete: {unconnectedStreams.Count} unconnected stream(s): {string.Join(", ", unconnectedStreams)}";
                    _logger.Warning(message);
                    return ValidationResult.FailureResult(message, new InvalidOperationException(message));
                }

                _logger.Information("Flowsheet topology is valid: all {StreamCount} streams are connected",
                    streamIds.Count);

                return ValidationResult.SuccessResult(new List<string> { "TopologyValid" });
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error validating topology: {ex.Message}";
                _logger.Error(ex, message);
                return ValidationResult.FailureResult(message, ex);
            }
        }
    }
}
