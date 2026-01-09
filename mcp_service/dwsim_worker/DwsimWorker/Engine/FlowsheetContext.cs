using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Serilog;
using DwsimWorker.Exceptions;
using DwsimWorker.Models;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Encapsulates a DWSIM flowsheet and all associated state.
    /// Manages compounds, material streams, unit operations, and connections.
    /// This class is NOT thread-safe - use one instance per thread/session.
    /// </summary>
    public sealed class FlowsheetContext : IDisposable
    {
        private readonly ILogger _logger;
        private readonly FlowsheetContextConfig _config;
        private readonly AssemblyLoader _assemblyLoader;
        private readonly DwsimValidator _validator;

        private object _flowsheet; // DWSIM.SharedClasses.Flowsheet
        private bool _isInitialized;
        private bool _disposed;

        // State management
        private readonly List<string> _compounds;
        private readonly Dictionary<string, object> _streams; // MaterialStream objects
        private readonly Dictionary<string, object> _units; // UnitOperation objects
        private readonly List<ConnectionInfo> _connections;

        /// <summary>
        /// Gets a value indicating whether the flowsheet context has been initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="FlowsheetContext"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for flowsheet context logging.</param>
        /// <param name="config">The configuration for flowsheet context behavior.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger or config is null.</exception>
        public FlowsheetContext(ILogger logger, FlowsheetContextConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _assemblyLoader = new AssemblyLoader(_logger, _config.AssemblyConfig);
            _validator = new DwsimValidator(_logger);

            _compounds = new List<string>();
            _streams = new Dictionary<string, object>();
            _units = new Dictionary<string, object>();
            _connections = new List<ConnectionInfo>();

            _logger.Information("FlowsheetContext created: {FlowsheetName}", _config.FlowsheetName);
        }

        /// <summary>
        /// Initializes the flowsheet context by loading DWSIM assemblies and creating the flowsheet instance.
        /// Must be called before using any other methods.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when already initialized or disposed.</exception>
        /// <exception cref="DwsimLoadException">Thrown when assembly loading fails.</exception>
        public void Initialize()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FlowsheetContext));

            if (_isInitialized)
                throw new InvalidOperationException("FlowsheetContext is already initialized.");

            _logger.Information("Initializing FlowsheetContext: {FlowsheetName}", _config.FlowsheetName);

            try
            {
                // Step 1: Load DWSIM assemblies
                _logger.Information("Loading DWSIM assemblies...");
                var loadResult = _assemblyLoader.LoadDwsimAssemblies();

                if (!loadResult.Success)
                {
                    _logger.Error("Failed to load DWSIM assemblies: {Message}", loadResult.Message);
                    throw new DwsimLoadException(
                        loadResult.Message,
                        "DWSIM.Assemblies",
                        _config.AssemblyConfig.AssemblyPath ?? "auto-detected",
                        ErrorCode.AssemblyLoadFailure,
                        loadResult.Error);
                }

                _logger.Information("DWSIM assemblies loaded successfully: {Count} assemblies", loadResult.LoadedAssemblies.Count);

                // Step 2: Create Flowsheet instance
                _logger.Information("Creating DWSIM Flowsheet instance...");
                _flowsheet = CreateFlowsheetInstance();

                if (_flowsheet == null)
                {
                    throw new DwsimLoadException(
                        "Failed to create Flowsheet instance (returned null)",
                        "DWSIM.SharedClasses.Flowsheet",
                        "N/A",
                        ErrorCode.ValidationFailure);
                }

                _logger.Information("Flowsheet instance created successfully");

                // Step 3: Optional validation
                if (_config.ValidateAfterInit)
                {
                    _logger.Information("Validating flowsheet...");
                    var validationResult = _validator.ValidateFlowsheetCreation();

                    if (!validationResult.Success)
                    {
                        _logger.Warning("Flowsheet validation failed: {Message}", validationResult.Message);
                        // Don't throw - validation is informational
                    }
                    else
                    {
                        _logger.Information("Flowsheet validation succeeded");
                    }
                }

                _isInitialized = true;
                _logger.Information("FlowsheetContext initialization complete: {FlowsheetName}", _config.FlowsheetName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "FlowsheetContext initialization failed");
                throw;
            }
        }

        /// <summary>
        /// Gets the DWSIM Flowsheet instance.
        /// </summary>
        /// <returns>The DWSIM Flowsheet object.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not initialized.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when disposed.</exception>
        public object GetFlowsheet()
        {
            EnsureInitialized();
            return _flowsheet;
        }

        /// <summary>
        /// Adds a compound to the flowsheet's compound list.
        /// </summary>
        /// <param name="compoundName">The name of the compound to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when compoundName is null or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not initialized.</exception>
        public void AddCompound(string compoundName)
        {
            if (string.IsNullOrWhiteSpace(compoundName))
                throw new ArgumentNullException(nameof(compoundName), "Compound name cannot be null or empty.");

            EnsureInitialized();

            if (!_compounds.Contains(compoundName))
            {
                _compounds.Add(compoundName);
                _logger.Debug("Compound added to flowsheet: {CompoundName}", compoundName);
            }
            else
            {
                _logger.Debug("Compound already exists in flowsheet: {CompoundName}", compoundName);
            }
        }

        /// <summary>
        /// Gets the list of compounds in the flowsheet.
        /// </summary>
        /// <returns>A read-only list of compound names.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not initialized.</exception>
        public IReadOnlyList<string> GetCompounds()
        {
            EnsureInitialized();
            return _compounds.AsReadOnly();
        }

        /// <summary>
        /// Saves the current flowsheet state to disk using the DWSIM flowsheet API.
        /// </summary>
        /// <param name="filePath">Destination file path.</param>
        public void SaveCase(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath), "File path cannot be null or empty.");

            EnsureInitialized();
            InvokeFlowsheetFileOperation(
                new[] { "SaveToXML", "SaveToFile", "SaveAs", "SaveSimulation", "SaveFlowsheet", "Save" },
                filePath,
                "save");
        }

        /// <summary>
        /// Loads a flowsheet state from disk using the DWSIM flowsheet API.
        /// </summary>
        /// <param name="filePath">Source file path.</param>
        public void LoadCase(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath), "File path cannot be null or empty.");

            EnsureInitialized();
            InvokeFlowsheetFileOperation(
                new[] { "LoadFromXML", "LoadFromFile", "LoadSimulation", "LoadFlowsheet", "Open", "Load" },
                filePath,
                "load");

            _compounds.Clear();
            _streams.Clear();
            _units.Clear();
            _connections.Clear();
        }

        /// <summary>
        /// Adds a material stream to the flowsheet registry.
        /// </summary>
        /// <param name="stream">The material stream object (DWSIM.Thermodynamics.Streams.MaterialStream).</param>
        /// <param name="streamId">The unique identifier for the stream.</param>
        /// <exception cref="ArgumentNullException">Thrown when stream is null or streamId is null/whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not initialized or streamId already exists.</exception>
        public void AddStream(object stream, string streamId)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (string.IsNullOrWhiteSpace(streamId))
                throw new ArgumentNullException(nameof(streamId), "Stream ID cannot be null or empty.");

            EnsureInitialized();

            if (_streams.ContainsKey(streamId))
            {
                throw new InvalidOperationException($"Stream with ID '{streamId}' already exists in flowsheet.");
            }

            _streams.Add(streamId, stream);
            _logger.Debug("Stream added to flowsheet: {StreamId}", streamId);
        }

        /// <summary>
        /// Gets a material stream by its ID.
        /// </summary>
        /// <param name="streamId">The unique identifier of the stream.</param>
        /// <returns>The material stream object, or null if not found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when streamId is null or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not initialized.</exception>
        public object GetStream(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId))
                throw new ArgumentNullException(nameof(streamId), "Stream ID cannot be null or empty.");

            EnsureInitialized();

            return _streams.TryGetValue(streamId, out object stream) ? stream : null;
        }

        /// <summary>
        /// Gets all stream IDs in the flowsheet.
        /// </summary>
        /// <returns>A read-only collection of stream IDs.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not initialized.</exception>
        public IReadOnlyCollection<string> GetStreamIds()
        {
            EnsureInitialized();
            return _streams.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// Adds a unit operation to the flowsheet registry.
        /// </summary>
        /// <param name="unit">The unit operation object (DWSIM.UnitOperations.*).</param>
        /// <param name="unitId">The unique identifier for the unit operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when unit is null or unitId is null/whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not initialized or unitId already exists.</exception>
        public void AddUnit(object unit, string unitId)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));

            if (string.IsNullOrWhiteSpace(unitId))
                throw new ArgumentNullException(nameof(unitId), "Unit ID cannot be null or empty.");

            EnsureInitialized();

            if (_units.ContainsKey(unitId))
            {
                throw new InvalidOperationException($"Unit operation with ID '{unitId}' already exists in flowsheet.");
            }

            _units.Add(unitId, unit);
            _logger.Debug("Unit operation added to flowsheet: {UnitId}", unitId);
        }

        /// <summary>
        /// Gets a unit operation by its ID.
        /// </summary>
        /// <param name="unitId">The unique identifier of the unit operation.</param>
        /// <returns>The unit operation object, or null if not found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when unitId is null or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not initialized.</exception>
        public object GetUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
                throw new ArgumentNullException(nameof(unitId), "Unit ID cannot be null or empty.");

            EnsureInitialized();

            return _units.TryGetValue(unitId, out object unit) ? unit : null;
        }

        /// <summary>
        /// Gets all unit operation IDs in the flowsheet.
        /// </summary>
        /// <returns>A read-only collection of unit operation IDs.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not initialized.</exception>
        public IReadOnlyCollection<string> GetUnitIds()
        {
            EnsureInitialized();
            return _units.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// Adds a connection to the flowsheet's connection registry.
        /// </summary>
        /// <param name="connection">The connection information.</param>
        /// <exception cref="ArgumentNullException">Thrown when connection is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not initialized.</exception>
        public void AddConnection(ConnectionInfo connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            EnsureInitialized();

            // Check for duplicate connection
            if (_connections.Any(c => c.StreamId == connection.StreamId))
            {
                _logger.Warning("Stream '{StreamId}' is already connected. Replacing connection.", connection.StreamId);
                _connections.RemoveAll(c => c.StreamId == connection.StreamId);
            }

            _connections.Add(connection);
            _logger.Debug("Connection added: Stream '{StreamId}' -> Unit '{UnitId}' Port '{PortName}'",
                connection.StreamId, connection.UnitId, connection.PortName);
        }

        /// <summary>
        /// Gets all connections in the flowsheet.
        /// </summary>
        /// <returns>A read-only list of connection information.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not initialized.</exception>
        public IReadOnlyList<ConnectionInfo> GetConnections()
        {
            EnsureInitialized();
            return _connections.AsReadOnly();
        }

        /// <summary>
        /// Gets connection information for a specific stream.
        /// </summary>
        /// <param name="streamId">The stream ID to look up.</param>
        /// <returns>The connection information, or null if the stream is not connected.</returns>
        /// <exception cref="ArgumentNullException">Thrown when streamId is null or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not initialized.</exception>
        public ConnectionInfo GetConnectionForStream(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId))
                throw new ArgumentNullException(nameof(streamId), "Stream ID cannot be null or empty.");

            EnsureInitialized();

            return _connections.FirstOrDefault(c => c.StreamId == streamId);
        }

        /// <summary>
        /// Removes a connection from the flowsheet.
        /// </summary>
        /// <param name="streamId">The stream ID whose connection to remove.</param>
        /// <returns>True if a connection was removed; false if no connection existed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when streamId is null or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not initialized.</exception>
        public bool RemoveConnection(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId))
                throw new ArgumentNullException(nameof(streamId), "Stream ID cannot be null or empty.");

            EnsureInitialized();

            int removed = _connections.RemoveAll(c => c.StreamId == streamId);
            if (removed > 0)
            {
                _logger.Debug("Connection removed for stream: {StreamId}", streamId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Creates a DWSIM Flowsheet instance using reflection.
        /// </summary>
        /// <returns>A new Flowsheet instance.</returns>
        private object CreateFlowsheetInstance()
        {
            // Try multiple possible type names for Flowsheet
            // Primary type is FOSSEEFlowsheet - the backend model class
            var possibleTypeNames = new[]
            {
                "DWSIM.SharedClasses.FOSSEEFlowsheet",  // Primary: backend Flowsheet class
                "DWSIM.SharedClasses.Flowsheet",         // Legacy fallback
                "DWSIM.Flowsheet.Flowsheet",
                "DWSIM.Simulator.Flowsheet",
                "Flowsheet"
                // Note: DWSIM.FormFlowsheet removed - it's a UI class requiring graphics dependencies
            };

            Type flowsheetType = null;
            string flowsheetTypeName = null;

            // Log all loaded assemblies for diagnostics
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            _logger.Debug("Total loaded assemblies: {Count}", allAssemblies.Length);
            var dwsimAssemblies = allAssemblies.Where(a => a.FullName.Contains("DWSIM", StringComparison.OrdinalIgnoreCase)).ToList();
            _logger.Debug("DWSIM assemblies found: {Count}", dwsimAssemblies.Count);
            foreach (var asm in dwsimAssemblies)
            {
                _logger.Debug("  - {AssemblyName}", asm.FullName);
            }

            // Try each possible type name
            foreach (var typeName in possibleTypeNames)
            {
                _logger.Debug("Trying type name: {TypeName}", typeName);

                // Step 1: Try Type.GetType() first (fast path)
                flowsheetType = Type.GetType(typeName);

                // Step 2: If not found, search through all loaded assemblies
                if (flowsheetType == null)
                {
                    foreach (var assembly in allAssemblies)
                    {
                        try
                        {
                            flowsheetType = assembly.GetType(typeName);
                            if (flowsheetType != null)
                            {
                                _logger.Debug("Found type '{TypeName}' in assembly: {AssemblyName}", typeName, assembly.FullName);
                                flowsheetTypeName = typeName;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug(ex, "Error searching for type in assembly: {AssemblyName}", assembly.FullName);
                        }
                    }
                }
                else
                {
                    _logger.Debug("Found type '{TypeName}' via Type.GetType()", typeName);
                    flowsheetTypeName = typeName;
                }

                if (flowsheetType != null)
                {
                    _logger.Information("Successfully located Flowsheet type: {TypeName}", flowsheetTypeName);
                    break;
                }
            }

            if (flowsheetType == null)
            {
                // Final diagnostic: Search for Flowsheet class in ALL DWSIM assemblies
                _logger.Debug("Searching for 'Flowsheet' type in all DWSIM assemblies:");

                foreach (var dwsimAsm in dwsimAssemblies)
                {
                    try
                    {
                        _logger.Debug("Checking assembly: {AssemblyName}", dwsimAsm.GetName().Name);

                        // Look for types with exact name "Flowsheet"
                        var flowsheetTypes = dwsimAsm.GetTypes()
                            .Where(t => t.Name.Equals("Flowsheet", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (flowsheetTypes.Any())
                        {
                            _logger.Debug("  Found {Count} types named 'Flowsheet' in {AssemblyName}:",
                                flowsheetTypes.Count, dwsimAsm.GetName().Name);
                            foreach (var t in flowsheetTypes)
                            {
                                _logger.Debug("    - {TypeFullName} (IsPublic={IsPublic}, IsClass={IsClass})",
                                    t.FullName, t.IsPublic, t.IsClass);
                            }
                        }
                        else
                        {
                            // List types containing "Flowsheet" (especially from DWSIM.exe)
                            var relatedTypes = dwsimAsm.GetTypes()
                                .Where(t => t.FullName != null && t.FullName.Contains("Flowsheet"))
                                .OrderBy(t => t.FullName)
                                .ToList();

                            if (relatedTypes.Count > 0)
                            {
                                _logger.Debug("  No exact 'Flowsheet' type, but found {Count} related types", relatedTypes.Count);

                                // For DWSIM.exe, list all the types to help identify the correct one
                                if (dwsimAsm.GetName().Name == "DWSIM")
                                {
                                    _logger.Debug("  Listing types from DWSIM.exe (first 30):");
                                    foreach (var t in relatedTypes.Take(30))
                                    {
                                        _logger.Debug("    - {TypeFullName} (Name={Name}, IsPublic={IsPublic}, IsClass={IsClass})",
                                            t.FullName, t.Name, t.IsPublic, t.IsClass);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug(ex, "  Could not list types in {AssemblyName}", dwsimAsm.GetName().Name);
                    }
                }

                throw new DwsimLoadException(
                    $"Flowsheet type not found. Tried: {string.Join(", ", possibleTypeNames)}. Searched {allAssemblies.Length} assemblies, found {dwsimAssemblies.Count} DWSIM assemblies.",
                    "Flowsheet",
                    "N/A",
                    ErrorCode.TypeLoadFailure);
            }

            // Create an instance
            try
            {
                var flowsheet = Activator.CreateInstance(flowsheetType);
                _logger.Debug("Flowsheet instance created successfully");
                return flowsheet;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create Flowsheet instance");
                throw new DwsimLoadException(
                    $"Failed to create instance of '{flowsheetTypeName}': {ex.Message}",
                    flowsheetTypeName,
                    "N/A",
                    ErrorCode.ValidationFailure,
                    ex);
            }
        }

        /// <summary>
        /// Ensures the flowsheet context is initialized and not disposed.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FlowsheetContext));

            if (!_isInitialized)
                throw new InvalidOperationException("FlowsheetContext is not initialized. Call Initialize() first.");
        }

        private void InvokeFlowsheetFileOperation(IEnumerable<string> candidateMethods, string filePath, string operationName)
        {
            var flowsheetType = _flowsheet.GetType();
            var methods = flowsheetType.GetMethods(BindingFlags.Instance | BindingFlags.Public);

            foreach (var methodName in candidateMethods)
            {
                foreach (var method in methods.Where(m => m.Name == methodName))
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                    {
                        _logger.Information("Flowsheet {Operation} using {Method}", operationName, methodName);
                        method.Invoke(_flowsheet, new object[] { filePath });
                        return;
                    }
                }
            }

            throw new InvalidOperationException($"Flowsheet does not expose a supported {operationName} method.");
        }

        /// <summary>
        /// Disposes the flowsheet context and releases all resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _logger.Information("Disposing FlowsheetContext: {FlowsheetName}", _config.FlowsheetName);

            try
            {
                // Clear all state
                _compounds.Clear();
                _streams.Clear();
                _units.Clear();
                _connections.Clear();

                // Dispose flowsheet if it implements IDisposable
                if (_flowsheet is IDisposable disposableFlowsheet)
                {
                    disposableFlowsheet.Dispose();
                }

                _flowsheet = null;
                _isInitialized = false;

                _logger.Information("FlowsheetContext disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during FlowsheetContext disposal");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
