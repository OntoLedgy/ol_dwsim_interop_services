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
        private readonly Dictionary<string, StreamProperties> _streamProperties;
        private object _propertyPackage;
        private string _propertyPackageName;
        private readonly object _calculationCacheLock = new object();
        private CalculationResult _cachedCalculationResult;
        private ConvergenceStatus _cachedConvergenceStatus;
        private DateTime? _lastCalculationTimestamp;

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
            _streamProperties = new Dictionary<string, StreamProperties>();
            _cachedConvergenceStatus = ConvergenceStatus.NotStarted();
            _propertyPackage = null;
            _propertyPackageName = null;

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

                // Step 3: Initialize compound database
                try
                {
                    _logger.Information("Initializing DWSIM compound database...");
                    InitializeCompoundDatabase();
                    _logger.Information("Compound database initialized successfully");
                }
                catch (Exception dbEx)
                {
                    _logger.Warning(dbEx, "Failed to initialize compound database - compounds may not be available");
                    // Don't throw - allow flowsheet to work even if compound database fails
                }

                // Step 4: Optional validation
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
                InvalidateCalculationCache("compound added");
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
            _streamProperties.Clear();
            _propertyPackage = null;
            _propertyPackageName = null;
            InvalidateCalculationCache("flowsheet loaded");
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
            InvalidateCalculationCache("stream added");
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
        /// Stores stream properties for later retrieval.
        /// </summary>
        public void CacheStreamProperties(string streamId, StreamProperties properties)
        {
            if (string.IsNullOrWhiteSpace(streamId))
                throw new ArgumentNullException(nameof(streamId), "Stream ID cannot be null or empty.");

            if (properties == null)
                throw new ArgumentNullException(nameof(properties));

            EnsureInitialized();
            _streamProperties[streamId] = properties;
        }

        /// <summary>
        /// Attempts to retrieve cached stream properties.
        /// </summary>
        public bool TryGetStreamProperties(string streamId, out StreamProperties properties)
        {
            if (string.IsNullOrWhiteSpace(streamId))
                throw new ArgumentNullException(nameof(streamId), "Stream ID cannot be null or empty.");

            EnsureInitialized();
            return _streamProperties.TryGetValue(streamId, out properties);
        }

        /// <summary>
        /// Removes a stream and any associated connections.
        /// </summary>
        /// <param name="streamId">The stream identifier to remove.</param>
        /// <param name="removedConnections">Connections removed as part of cleanup.</param>
        /// <returns>True if the stream was removed; otherwise, false.</returns>
        public bool RemoveStream(string streamId, out IReadOnlyList<ConnectionInfo> removedConnections)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(streamId))
                throw new ArgumentNullException(nameof(streamId), "Stream ID cannot be null or empty.");

            removedConnections = RemoveConnectionsForObject(streamId);
            var removed = _streams.Remove(streamId);
            if (removed)
            {
                _streamProperties.Remove(streamId);
                InvalidateCalculationCache("stream removed");
            }

            return removed;
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
            InvalidateCalculationCache("unit added");
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
        /// Stores the current property package for the flowsheet.
        /// </summary>
        public void SetPropertyPackage(object propertyPackage, string packageName)
        {
            if (propertyPackage == null)
                throw new ArgumentNullException(nameof(propertyPackage));

            if (string.IsNullOrWhiteSpace(packageName))
                throw new ArgumentNullException(nameof(packageName), "Property package name cannot be null or empty.");

            EnsureInitialized();

            _propertyPackage = propertyPackage;
            _propertyPackageName = packageName;
            InvalidateCalculationCache("property package set");
        }

        /// <summary>
        /// Gets the current property package instance, if any.
        /// </summary>
        public object GetPropertyPackage()
        {
            EnsureInitialized();
            return _propertyPackage;
        }

        /// <summary>
        /// Gets the name of the current property package, if any.
        /// </summary>
        public string GetPropertyPackageName()
        {
            EnsureInitialized();
            return _propertyPackageName;
        }

        /// <summary>
        /// Removes a unit operation and any associated connections.
        /// </summary>
        /// <param name="unitId">The unit identifier to remove.</param>
        /// <param name="removedConnections">Connections removed as part of cleanup.</param>
        /// <returns>True if the unit was removed; otherwise, false.</returns>
        public bool RemoveUnit(string unitId, out IReadOnlyList<ConnectionInfo> removedConnections)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(unitId))
                throw new ArgumentNullException(nameof(unitId), "Unit ID cannot be null or empty.");

            removedConnections = RemoveConnectionsForObject(unitId);
            var removed = _units.Remove(unitId);
            if (removed)
            {
                InvalidateCalculationCache("unit removed");
            }

            return removed;
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
            InvalidateCalculationCache("connection added");
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
        /// Removes all connections associated with a stream or unit identifier.
        /// </summary>
        /// <param name="objectId">Stream or unit identifier.</param>
        /// <returns>Removed connections.</returns>
        public IReadOnlyList<ConnectionInfo> RemoveConnectionsForObject(string objectId)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(objectId))
                throw new ArgumentNullException(nameof(objectId), "Object ID cannot be null or empty.");

            var removed = _connections
                .Where(c => string.Equals(c.StreamId, objectId, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(c.UnitId, objectId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (removed.Count == 0)
            {
                return removed.AsReadOnly();
            }

            _connections.RemoveAll(c =>
                string.Equals(c.StreamId, objectId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.UnitId, objectId, StringComparison.OrdinalIgnoreCase));

            InvalidateCalculationCache("connections removed");
            return removed.AsReadOnly();
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
                InvalidateCalculationCache("connection removed");
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
                "DWSIM.FlowsheetBase.FlowsheetBase",    // Headless flowsheet base class
                "DWSIM.UI.Desktop.Shared.Flowsheet",    // Concrete flowsheet implementation
                "DWSIM.SharedClasses.FOSSEEFlowsheet",  // Legacy metadata-only flowsheet
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

            // Try each possible type name and instantiate immediately
            foreach (var typeName in possibleTypeNames)
            {
                _logger.Debug("Trying type name: {TypeName}", typeName);

                // Step 1: Try Type.GetType() first (fast path)
                flowsheetType = Type.GetType(typeName);
                if (flowsheetType != null && flowsheetType.IsAbstract)
                {
                    flowsheetType = null;
                }

                // Step 2: If not found, search through all loaded assemblies
                if (flowsheetType == null)
                {
                    foreach (var assembly in allAssemblies)
                    {
                        try
                        {
                            flowsheetType = assembly.GetType(typeName);
                            if (flowsheetType != null && flowsheetType.IsAbstract)
                            {
                                flowsheetType = null;
                            }

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

                    try
                    {
                        var instance = Activator.CreateInstance(flowsheetType);
                        if (!HasRequiredFlowsheetApi(instance))
                        {
                            _logger.Warning("Flowsheet type {TypeName} does not expose required methods; trying next type", flowsheetTypeName);
                            flowsheetType = null;
                            flowsheetTypeName = null;
                            continue;
                        }

                        _logger.Debug("Flowsheet instance created successfully");
                        return instance;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to create flowsheet instance for {TypeName}, trying next type", flowsheetTypeName);
                        flowsheetType = null;
                        flowsheetTypeName = null;
                    }
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
            return null;
        }

        private bool HasRequiredFlowsheetApi(object flowsheet)
        {
            if (flowsheet == null)
            {
                return false;
            }

            var type = flowsheet.GetType();
            var addObject = type.GetMethods().Any(m => m.Name == "AddObject");
            var connectObjects = type.GetMethods().Any(m => m.Name == "ConnectObjects");
            return addObject && connectObjects;
        }

        /// <summary>
        /// Initializes the DWSIM compound database to enable compound lookups.
        /// </summary>
        private void InitializeCompoundDatabase()
        {
            if (_flowsheet == null)
            {
                _logger.Warning("Cannot initialize compound database - flowsheet is null");
                return;
            }

            try
            {
                // Try to find and call the compound database initialization
                // In DWSIM, this is typically done via Databases.Load() or similar
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                // Look for the ChEDL database loader
                foreach (var assembly in assemblies)
                {
                    if (!assembly.FullName.Contains("DWSIM", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Try to find Databases class
                    var databasesType = assembly.GetType("DWSIM.Thermodynamics.Databases.ChEDL.Databases");
                    if (databasesType != null)
                    {
                        var loadMethod = databasesType.GetMethod("Load", BindingFlags.Static | BindingFlags.Public);
                        if (loadMethod != null)
                        {
                            _logger.Debug("Calling DWSIM.Thermodynamics.Databases.ChEDL.Databases.Load()");
                            loadMethod.Invoke(null, null);
                            _logger.Information("Compound database loaded via ChEDL.Databases.Load()");
                            return;
                        }
                    }

                    // Try alternative database initialization
                    var altDatabasesType = assembly.GetType("DWSIM.Thermodynamics.Databases.Databases");
                    if (altDatabasesType != null)
                    {
                        var loadMethod = altDatabasesType.GetMethod("Load", BindingFlags.Static | BindingFlags.Public);
                        if (loadMethod != null)
                        {
                            _logger.Debug("Calling DWSIM.Thermodynamics.Databases.Databases.Load()");
                            loadMethod.Invoke(null, null);
                            _logger.Information("Compound database loaded via Databases.Load()");
                            return;
                        }
                    }
                }

                // Try to initialize via flowsheet properties
                var flowsheetType = _flowsheet.GetType();

                // Look for LoadCompounds or similar methods
                var allMethods = flowsheetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var compoundMethods = allMethods.Where(m =>
                    m.Name.Contains("Compound", StringComparison.OrdinalIgnoreCase) ||
                    m.Name.Contains("Database", StringComparison.OrdinalIgnoreCase)).ToList();

                _logger.Debug("Found {Count} compound-related methods on flowsheet", compoundMethods.Count);
                foreach (var method in compoundMethods)
                {
                    _logger.Debug("  - {MethodName}({Parameters})",
                        method.Name,
                        string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}")));
                }

                // Try to call LoadCompounds method
                var loadCompoundsMethod = flowsheetType.GetMethod("LoadCompounds",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (loadCompoundsMethod != null && loadCompoundsMethod.GetParameters().Length == 0)
                {
                    _logger.Debug("Calling LoadCompounds method");
                    loadCompoundsMethod.Invoke(_flowsheet, null);
                    _logger.Information("Compound database initialized via LoadCompounds()");
                    return;
                }

                // Check if there's an AvailableCompounds property
                var availableCompoundsProperty = flowsheetType.GetProperty("AvailableCompounds");
                if (availableCompoundsProperty != null)
                {
                    var availableCompounds = availableCompoundsProperty.GetValue(_flowsheet);
                    _logger.Debug("AvailableCompounds type: {Type}", availableCompounds?.GetType().FullName ?? "null");

                    if (availableCompounds != null)
                    {
                        // Check if it's a dictionary and check its count
                        if (availableCompounds is System.Collections.IDictionary dict)
                        {
                            _logger.Debug("AvailableCompounds count: {Count}", dict.Count);

                            if (dict.Count == 0)
                            {
                                _logger.Warning("AvailableCompounds dictionary is empty - need to populate it");

                                // Try to add compounds to the dictionary directly
                                // In DWSIM, compounds are typically loaded from embedded resources
                                TryPopulateCompoundDatabase(dict);
                            }
                            else
                            {
                                _logger.Information("AvailableCompounds already contains {Count} compounds", dict.Count);
                                return;
                            }
                        }
                    }
                }

                _logger.Warning("Could not find compound database initialization method - compounds may need to be loaded manually");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error initializing compound database");
                throw;
            }
        }

        /// <summary>
        /// Tries to populate the compound database from DWSIM's compound databases.
        /// This follows the exact initialization pattern from DWSIM.Automation.Automation3 class.
        /// </summary>
        private void TryPopulateCompoundDatabase(System.Collections.IDictionary targetDictionary)
        {
            try
            {
                _logger.Information("Loading compounds from DWSIM databases (following DWSIM.Automation pattern)");

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var thermodynamicsAssembly = assemblies.FirstOrDefault(a => a.FullName.Contains("DWSIM.Thermodynamics"));

                if (thermodynamicsAssembly == null)
                {
                    _logger.Warning("DWSIM.Thermodynamics assembly not found");
                    return;
                }

                // Load compounds from ChemSep database
                LoadFromDatabase(thermodynamicsAssembly, "DWSIM.Thermodynamics.Databases.ChemSep", targetDictionary, "ChemSep");

                // Load compounds from CoolProp database
                LoadFromDatabase(thermodynamicsAssembly, "DWSIM.Thermodynamics.Databases.CoolProp", targetDictionary, "CoolProp");

                // Load compounds from Biodiesel database
                LoadFromDatabase(thermodynamicsAssembly, "DWSIM.Thermodynamics.Databases.Biodiesel", targetDictionary, "Biodiesel");

                // Load compounds from ChEDL_Thermo database
                LoadFromDatabase(thermodynamicsAssembly, "DWSIM.Thermodynamics.Databases.ChEDL_Thermo", targetDictionary, "ChEDL_Thermo");

                // Load compounds from Electrolyte database
                LoadFromDatabase(thermodynamicsAssembly, "DWSIM.Thermodynamics.Databases.Electrolyte", targetDictionary, "Electrolyte");

                _logger.Information("Compound database populated with {Count} compounds from DWSIM databases", targetDictionary.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error populating compound database");
            }
        }

        /// <summary>
        /// Loads compounds from a specific DWSIM database type.
        /// Follows the pattern: new Database() -> Load() -> Transfer() -> Add to dictionary
        /// </summary>
        private void LoadFromDatabase(Assembly assembly, string typeName, System.Collections.IDictionary targetDictionary, string databaseName)
        {
            try
            {
                var dbType = assembly.GetType(typeName);
                if (dbType == null)
                {
                    _logger.Debug("Database type {TypeName} not found", typeName);
                    return;
                }

                _logger.Debug("Loading compounds from {DatabaseName}", databaseName);

                // Create instance of database
                var dbInstance = Activator.CreateInstance(dbType);

                // Call Load() method
                var loadMethod = dbType.GetMethod("Load", BindingFlags.Instance | BindingFlags.Public);
                if (loadMethod == null)
                {
                    _logger.Warning("Load() method not found on {DatabaseName}", databaseName);
                    return;
                }

                loadMethod.Invoke(dbInstance, null);

                // Call Transfer() method to get compounds
                var transferMethod = dbType.GetMethod("Transfer", BindingFlags.Instance | BindingFlags.Public);
                if (transferMethod == null)
                {
                    _logger.Warning("Transfer() method not found on {DatabaseName}", databaseName);
                    return;
                }

                var compounds = transferMethod.Invoke(dbInstance, null);

                // Add compounds to dictionary
                int addedCount = 0;
                if (compounds is System.Collections.IEnumerable enumerable)
                {
                    foreach (var compound in enumerable)
                    {
                        if (compound == null) continue;

                        // Get the Name property
                        var nameProperty = compound.GetType().GetProperty("Name");
                        if (nameProperty == null) continue;

                        var name = nameProperty.GetValue(compound) as string;
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        // Add to dictionary if not already present
                        if (!targetDictionary.Contains(name))
                        {
                            targetDictionary.Add(name, compound);
                            addedCount++;
                        }
                    }
                }

                _logger.Information("Loaded {Count} compounds from {DatabaseName}", addedCount, databaseName);

                // Dispose if needed
                if (dbInstance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to load compounds from {DatabaseName}", databaseName);
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
        /// Gets the cached calculation result, if any.
        /// </summary>
        /// <returns>The cached calculation result, or null if not available.</returns>
        public CalculationResult GetCachedCalculationResult()
        {
            EnsureInitialized();
            lock (_calculationCacheLock)
            {
                return _cachedCalculationResult;
            }
        }

        /// <summary>
        /// Gets the cached convergence status, or NotStarted if none is cached.
        /// </summary>
        /// <returns>The cached convergence status.</returns>
        public ConvergenceStatus GetCachedConvergenceStatus()
        {
            EnsureInitialized();
            lock (_calculationCacheLock)
            {
                return _cachedConvergenceStatus ?? ConvergenceStatus.NotStarted();
            }
        }

        /// <summary>
        /// Gets the timestamp of the most recent calculation, if available.
        /// </summary>
        /// <returns>The UTC timestamp of the last calculation, or null if not available.</returns>
        public DateTime? GetLastCalculationTimestamp()
        {
            EnsureInitialized();
            lock (_calculationCacheLock)
            {
                return _lastCalculationTimestamp;
            }
        }

        /// <summary>
        /// Caches the calculation result and associated status.
        /// </summary>
        /// <param name="result">The calculation result to cache.</param>
        public void CacheCalculationResult(CalculationResult result)
        {
            EnsureInitialized();
            lock (_calculationCacheLock)
            {
                _cachedCalculationResult = result;
                _cachedConvergenceStatus = result?.ConvergenceStatus ?? ConvergenceStatus.NotStarted();
                _lastCalculationTimestamp = result != null ? DateTime.UtcNow : (DateTime?)null;
            }
        }

        /// <summary>
        /// Updates the cached convergence status without overwriting the cached result.
        /// </summary>
        /// <param name="status">The status to cache.</param>
        public void UpdateConvergenceStatus(ConvergenceStatus status)
        {
            EnsureInitialized();
            lock (_calculationCacheLock)
            {
                _cachedConvergenceStatus = status ?? ConvergenceStatus.NotStarted();
            }
        }

        /// <summary>
        /// Invalidates the cached calculation result and status.
        /// </summary>
        /// <param name="reason">Optional reason for invalidation.</param>
        public void InvalidateCalculationCache(string reason = null)
        {
            lock (_calculationCacheLock)
            {
                _cachedCalculationResult = null;
                _cachedConvergenceStatus = ConvergenceStatus.NotStarted();
                _lastCalculationTimestamp = null;
            }

            _logger.Debug("Calculation cache invalidated: {Reason}", reason ?? "flowsheet modified");
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
                _streamProperties.Clear();
                _propertyPackage = null;
                _propertyPackageName = null;

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
