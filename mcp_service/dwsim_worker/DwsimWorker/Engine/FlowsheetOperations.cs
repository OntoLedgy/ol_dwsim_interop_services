using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using DwsimWorker.Adapters;
using DwsimWorker.Models;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Facade exposing flowsheet-building operations for pythonnet consumers.
    /// Wraps existing adapters and returns simple types for interop.
    /// </summary>
    public sealed class FlowsheetOperations
    {
        private readonly SessionManager _sessionManager;
        private readonly ILogger _logger;

        public FlowsheetOperations(SessionManager sessionManager)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _logger = Log.ForContext<FlowsheetOperations>();
        }

        public bool AddCompound(string sessionId, string compoundName)
        {
            var context = GetContext(sessionId);
            var adapter = new CompoundAdapter(_logger, context);
            var result = adapter.AddCompound(compoundName);
            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message);
            }
            return true;
        }

        public bool SetPropertyPackage(string sessionId, string packageName)
        {
            var context = GetContext(sessionId);
            var adapter = new PropertyPackageAdapter(_logger, context);
            var result = adapter.SetPropertyPackage(packageName);
            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message);
            }
            return true;
        }

        public string AddStream(
            string sessionId,
            string name,
            double temperatureK,
            double pressurePa,
            double molarFlow,
            IDictionary<string, double> composition,
            bool isSource = false)
        {
            var context = GetContext(sessionId);
            var adapter = new StreamAdapter(_logger, context);

            try
            {
                var properties = BuildStreamProperties(context, temperatureK, pressurePa, molarFlow, composition, isSource);
                var result = adapter.CreateStream(name, properties, isSource);
                if (!result.Success || string.IsNullOrWhiteSpace(result.ObjectId))
                {
                    var errorMsg = $"Unexpected error creating stream '{name}': {result.Message}";
                    if (result.Error != null)
                    {
                        errorMsg += $" Inner: {result.Error.Message}";
                        if (result.Error.InnerException != null)
                        {
                            errorMsg += $" InnerInner: {result.Error.InnerException.Message}";
                        }
                    }
                    throw new InvalidOperationException(errorMsg, result.Error);
                }
                return result.ObjectId;
            }
            catch (InvalidOperationException)
            {
                throw;  // Re-throw our own exceptions
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to add stream '{name}': {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMsg += $" Inner: {ex.InnerException.Message}";
                }
                throw new InvalidOperationException(errorMsg, ex);
            }
        }

        public Tuple<string, string> AddUnit(string sessionId, string unitType, string name, IDictionary<string, object> parameters)
        {
            var context = GetContext(sessionId);
            var adapter = new UnitOpAdapter(_logger, context);

            switch (unitType?.Trim().ToLowerInvariant())
            {
                case "separator":
                    var config = new UnitOpConfig(parameters);
                    var result = adapter.AddThreePhaseSeparator(name, config);
                    if (!result.Success || string.IsNullOrWhiteSpace(result.ObjectId))
                    {
                        throw new InvalidOperationException(result.Message);
                    }
                    return Tuple.Create(result.ObjectId, "separator");
                default:
                    throw new NotSupportedException($"Unit type '{unitType}' is not supported.");
            }
        }

        public bool Connect(string sessionId, string sourceId, string targetId, string portName)
        {
            var context = GetContext(sessionId);
            var adapter = new ConnectionAdapter(_logger, context);
            var result = adapter.ConnectStream(sourceId, targetId, portName);
            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message);
            }
            return true;
        }

        public IDictionary<string, object> ListObjects(string sessionId)
        {
            var context = GetContext(sessionId);
            var streams = context.GetStreamIds().Select(id => new Dictionary<string, string>
            {
                { "id", id },
                { "name", id }
            }).ToList();

            var units = context.GetUnitIds().Select(id => new Dictionary<string, string>
            {
                { "id", id },
                { "name", id },
                { "unit_type", "unknown" }
            }).ToList();

            var connections = context.GetConnections().Select(conn => new Dictionary<string, string>
            {
                { "source_id", conn.StreamId },
                { "target_id", conn.UnitId },
                { "port_name", conn.PortName }
            }).ToList();

            return new Dictionary<string, object>
            {
                { "streams", streams },
                { "units", units },
                { "connections", connections }
            };
        }

        public bool FlashStream(string sessionId, string streamId)
        {
            var context = GetContext(sessionId);
            var adapter = new StreamAdapter(_logger, context);
            var result = adapter.FlashStream(streamId);
            if (!result.Success)
            {
                var errorMsg = $"Failed to flash stream '{streamId}': {result.Message}";
                if (result.Error != null)
                {
                    errorMsg += $" Inner: {result.Error.Message}";
                    if (result.Error.InnerException != null)
                    {
                        errorMsg += $" InnerInner: {result.Error.InnerException.Message}";
                    }
                }
                throw new InvalidOperationException(errorMsg, result.Error);
            }
            return true;
        }

        public bool SetBinaryInteractionParameter(string sessionId, string compound1, string compound2, double value)
        {
            var context = GetContext(sessionId);
            var adapter = new PropertyPackageAdapter(_logger, context);
            try
            {
                adapter.SetBinaryInteractionParameter(compound1, compound2, value);
                return true;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to set BIP for '{compound1}'-'{compound2}': {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMsg += $" Inner: {ex.InnerException.Message}";
                }
                throw new InvalidOperationException(errorMsg, ex);
            }
        }

        public object SetObjectParameter(string sessionId, string objectId, string parameterName, object value)
        {
            var context = GetContext(sessionId);
            var streamAdapter = new StreamAdapter(_logger, context);
            var unitAdapter = new UnitOpAdapter(_logger, context);

            var isStream = context.GetStream(objectId) != null;
            PropertySetResult result;

            if (isStream)
            {
                result = streamAdapter.SetProperty(objectId, parameterName, value);
            }
            else
            {
                result = unitAdapter.SetParameter(objectId, parameterName, value);
            }

            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message);
            }

            return new Dictionary<string, object>
            {
                { "value", value },
                { "previous_value", null }
            };
        }

        public IDictionary<string, object> DeleteObject(string sessionId, string objectId)
        {
            var context = GetContext(sessionId);
            var removedConnections = context.RemoveConnectionsForObject(objectId);
            bool deleted = false;

            if (context.GetStream(objectId) != null)
            {
                deleted = context.RemoveStream(objectId, out removedConnections);
            }
            else if (context.GetUnit(objectId) != null)
            {
                deleted = context.RemoveUnit(objectId, out removedConnections);
            }

            var removed = removedConnections
                .Select(conn => new Dictionary<string, string>
                {
                    { "source_id", conn.StreamId },
                    { "target_id", conn.UnitId },
                    { "port_name", conn.PortName }
                })
                .ToList();

            return new Dictionary<string, object>
            {
                { "object_id", objectId },
                { "deleted", deleted },
                { "removed_connections", removed }
            };
        }

        private FlowsheetContext GetContext(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentNullException(nameof(sessionId));

            Guid guid;
            if (!Guid.TryParse(sessionId, out guid))
            {
                throw new ArgumentException("Invalid session id format.", nameof(sessionId));
            }

            var result = _sessionManager.GetSession(guid);
            if (!result.Success || result.Data == null)
            {
                throw new InvalidOperationException(result.Message ?? "Session not found.");
            }

            return result.Data;
        }

        private static StreamProperties BuildStreamProperties(
            FlowsheetContext context,
            double temperatureK,
            double pressurePa,
            double molarFlow,
            IDictionary<string, double> composition,
            bool isSource)
        {
            var compounds = context.GetCompounds();

            // Auto-composition for outlet streams
            if (!isSource && (composition == null || composition.Count == 0))
            {
                if (compounds.Count == 0)
                {
                    throw new ArgumentException("Cannot create outlet stream: No compounds registered in the flowsheet.", nameof(composition));
                }

                // Auto-fill with equal fractions
                composition = new Dictionary<string, double>();
                double equalFraction = 1.0 / compounds.Count;
                foreach (var compound in compounds)
                {
                    composition[compound] = equalFraction;
                }
            }

            // Source streams must have composition
            if (composition == null || composition.Count == 0)
                throw new ArgumentException("Composition must be provided.", nameof(composition));

            var moleFractions = new List<double>(compounds.Count);
            foreach (var compound in compounds)
            {
                if (!composition.TryGetValue(compound, out double fraction))
                {
                    throw new ArgumentException($"Missing composition for compound '{compound}'.", nameof(composition));
                }
                moleFractions.Add(fraction);
            }

            var compositionModel = new Composition(moleFractions);

            var tempUnits = new UnitsOfMeasure("K", typeof(Temperature), new Ranges(0, 10000));
            var pressureUnits = new UnitsOfMeasure("Pa", typeof(Pressure), new Ranges(0, 1_000_000_000));
            var flowUnits = new UnitsOfMeasure("mol/s", typeof(MolarFlowRate), new Ranges(0, double.MaxValue));

            var temperature = new PhysicalProperties("Temperature", new Measurements(new Temperature(), temperatureK, tempUnits));
            var pressure = new PhysicalProperties("Pressure", new Measurements(new Pressure(), pressurePa, pressureUnits));
            var molarFlowRate = new PhysicalProperties("MolarFlow", new Measurements(new MolarFlowRate(), molarFlow, flowUnits));

            var properties = new StreamProperties(temperature, pressure, molarFlowRate, compositionModel);

            if (!properties.IsValid(out string errorMessage))
            {
                throw new InvalidOperationException($"Invalid stream properties: {errorMessage}");
            }

            return properties;
        }
    }
}
