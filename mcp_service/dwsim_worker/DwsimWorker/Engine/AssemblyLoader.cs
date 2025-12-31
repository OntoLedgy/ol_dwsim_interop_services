using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Serilog;
using DwsimWorker.Utilities;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Orchestrates the loading and validation of DWSIM assemblies.
    /// Main entry point for assembly loading operations.
    /// </summary>
    public sealed class AssemblyLoader
    {
        private readonly ILogger _logger;
        private readonly AssemblyLoaderConfig _config;
        private readonly DwsimValidator _validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyLoader"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for assembly loading logging.</param>
        /// <param name="config">The configuration for assembly loading behavior.</param>
        public AssemblyLoader(ILogger logger, AssemblyLoaderConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _validator = new DwsimValidator(logger);
        }

        /// <summary>
        /// Loads all required DWSIM assemblies using configured settings.
        /// </summary>
        /// <returns>A LoadResult containing loaded assemblies or error details.</returns>
        public LoadResult LoadDwsimAssemblies()
        {
            _logger.Information("Starting DWSIM assembly loading process...");
            _logger.Information("Configuration: ValidateAfterLoad={ValidateAfterLoad}, LoadTimeout={LoadTimeout}",
                _config.ValidateAfterLoad, _config.LoadTimeout);

            try
            {
                // Step 1: Resolve DWSIM assembly path
                string basePath;
                if (!string.IsNullOrEmpty(_config.AssemblyPath))
                {
                    basePath = _config.AssemblyPath;
                    _logger.Information("Using configured assembly path: {Path}", basePath);
                }
                else
                {
                    _logger.Information("Resolving DWSIM assembly path...");
                    basePath = PathResolver.ResolveDwsimPath();
                }

                if (!Directory.Exists(basePath))
                {
                    var message = $"DWSIM assembly path does not exist: {basePath}";
                    _logger.Error(message);
                    throw new DwsimLoadException(
                        message,
                        "DWSIM.Assemblies",
                        basePath,
                        ErrorCode.AssemblyNotFound);
                }

                // Step 2: Load each required assembly
                var loadedAssemblies = new List<AssemblyInfo>();
                var startTime = DateTime.UtcNow;

                foreach (var assemblyName in _config.RequiredAssemblies)
                {
                    _logger.Information("Loading assembly: {AssemblyName}...", assemblyName);

                    var assemblyPath = Path.Combine(basePath, $"{assemblyName}.dll");
                    var assembly = LoadAssembly(assemblyName, assemblyPath);

                    var assemblyInfo = new AssemblyInfo(
                        name: assemblyName,
                        version: assembly.GetName().Version,
                        path: assemblyPath,
                        loadedAt: DateTime.UtcNow);

                    loadedAssemblies.Add(assemblyInfo);

                    _logger.Information("Successfully loaded {AssemblyName} v{Version}",
                        assemblyName, assembly.GetName().Version);

                    // Check timeout
                    var elapsed = DateTime.UtcNow - startTime;
                    if (elapsed > _config.LoadTimeout)
                    {
                        var message = $"Assembly loading timed out after {elapsed.TotalSeconds:F2} seconds";
                        _logger.Error(message);
                        throw new DwsimLoadException(
                            message,
                            assemblyName,
                            assemblyPath,
                            ErrorCode.AssemblyLoadFailure);
                    }
                }

                _logger.Information("All assemblies loaded successfully. Total count: {Count}", loadedAssemblies.Count);

                // Step 3: Validate assemblies if configured
                if (_config.ValidateAfterLoad)
                {
                    _logger.Information("Starting post-load validation...");
                    var validationResult = _validator.ValidateInstantiation();

                    if (!validationResult.Success)
                    {
                        _logger.Error("Assembly validation failed: {Message}", validationResult.Message);
                        return LoadResult.ValidationFailureResult(
                            $"Assembly validation failed: {validationResult.Message}",
                            loadedAssemblies,
                            validationResult.Error);
                    }

                    _logger.Information("Validation succeeded. Validated types: {ValidatedTypes}",
                        string.Join(", ", validationResult.ValidatedTypes));
                }
                else
                {
                    _logger.Information("Validation skipped (ValidateAfterLoad=false)");
                }

                // Return success result
                return LoadResult.SuccessResult(loadedAssemblies);
            }
            catch (DwsimLoadException ex)
            {
                _logger.Error(ex, "DWSIM assembly loading failed: {Message}", ex.Message);
                return LoadResult.FailureResult(ex.Message, ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.Error(ex, "DWSIM assembly path not found");
                return LoadResult.FailureResult(
                    "DWSIM assemblies not found. Please install DWSIM or set DWSIM_PATH environment variable.",
                    ex);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during assembly loading");
                return LoadResult.FailureResult($"Unexpected error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads a specific DWSIM assembly from the given path.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly (without .dll extension).</param>
        /// <param name="assemblyPath">The full path to the assembly file.</param>
        /// <returns>The loaded Assembly.</returns>
        /// <exception cref="DwsimLoadException">Thrown when assembly loading fails.</exception>
        public Assembly LoadAssembly(string assemblyName, string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                throw new ArgumentException("Assembly name cannot be null or empty", nameof(assemblyName));

            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentException("Assembly path cannot be null or empty", nameof(assemblyPath));

            try
            {
                // Check if file exists
                if (!File.Exists(assemblyPath))
                {
                    var message = $"Assembly file not found: {assemblyPath}";
                    _logger.Error(message);
                    throw new DwsimLoadException(
                        message,
                        assemblyName,
                        assemblyPath,
                        ErrorCode.AssemblyNotFound,
                        new FileNotFoundException(message, assemblyPath));
                }

                _logger.Debug("Loading assembly from path: {AssemblyPath}", assemblyPath);

                // Load the assembly
                var assembly = Assembly.LoadFrom(assemblyPath);

                if (assembly == null)
                {
                    var message = $"Assembly.LoadFrom returned null for {assemblyName}";
                    _logger.Error(message);
                    throw new DwsimLoadException(
                        message,
                        assemblyName,
                        assemblyPath,
                        ErrorCode.AssemblyLoadFailure);
                }

                // Log assembly details
                var assemblyName2 = assembly.GetName();
                _logger.Debug("Assembly loaded: {Name} v{Version} from {Location}",
                    assemblyName2.Name, assemblyName2.Version, assembly.Location);

                return assembly;
            }
            catch (FileNotFoundException ex)
            {
                _logger.Error(ex, "Assembly file not found: {AssemblyPath}", assemblyPath);
                throw new DwsimLoadException(
                    $"Assembly '{assemblyName}' not found at path: {assemblyPath}",
                    assemblyName,
                    assemblyPath,
                    ErrorCode.AssemblyNotFound,
                    ex);
            }
            catch (FileLoadException ex)
            {
                _logger.Error(ex, "Assembly load failed (version conflict or corruption): {AssemblyPath}", assemblyPath);

                // Check for version mismatch in exception message
                var isVersionMismatch = ex.Message.Contains("version", StringComparison.OrdinalIgnoreCase);
                if (isVersionMismatch)
                {
                    _logger.Warning("Possible version mismatch for {AssemblyName}. Check binding redirects in App.config", assemblyName);
                }

                throw new DwsimLoadException(
                    $"Failed to load '{assemblyName}': {ex.Message}",
                    assemblyName,
                    assemblyPath,
                    ErrorCode.AssemblyLoadFailure,
                    ex);
            }
            catch (BadImageFormatException ex)
            {
                _logger.Error(ex, "Assembly has wrong format (bitness mismatch or corrupt): {AssemblyPath}", assemblyPath);

                var message = $"Assembly '{assemblyName}' has incorrect format. " +
                            "This may be due to x86/x64 bitness mismatch or a corrupted file.";

                throw new DwsimLoadException(
                    message,
                    assemblyName,
                    assemblyPath,
                    ErrorCode.AssemblyLoadFailure,
                    ex);
            }
            catch (TypeLoadException ex)
            {
                _logger.Error(ex, "Type load exception for assembly: {AssemblyPath}", assemblyPath);
                throw new DwsimLoadException(
                    $"Failed to load types from '{assemblyName}': {ex.Message}",
                    assemblyName,
                    assemblyPath,
                    ErrorCode.TypeLoadFailure,
                    ex);
            }
            catch (DwsimLoadException)
            {
                // Re-throw DwsimLoadException as-is
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error loading assembly: {AssemblyPath}", assemblyPath);
                throw new DwsimLoadException(
                    $"Unexpected error loading '{assemblyName}': {ex.Message}",
                    assemblyName,
                    assemblyPath,
                    ErrorCode.AssemblyLoadFailure,
                    ex);
            }
        }
    }
}
