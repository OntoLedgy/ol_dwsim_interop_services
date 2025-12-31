using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using Serilog;

namespace DwsimWorker.Utilities
{
    /// <summary>
    /// Static utility class for resolving DWSIM assembly paths using multiple fallback strategies.
    /// Supports environment variables, App.config settings, and default installation paths.
    /// </summary>
    public static class PathResolver
    {
        /// <summary>
        /// Required DWSIM assembly file names.
        /// </summary>
        private static readonly string[] RequiredAssemblies = new[]
        {
            "DWSIM.Interfaces.dll",
            "DWSIM.Thermodynamics.dll",
            "DWSIM.SharedClasses.dll"
        };

        /// <summary>
        /// Default DWSIM installation paths to check.
        /// </summary>
        private static readonly string[] DefaultInstallPaths = new[]
        {
            @"C:\Program Files\DWSIM",
            @"C:\Program Files (x86)\DWSIM",
            @"C:\DWSIM"
        };

        /// <summary>
        /// Resolves the DWSIM assembly path using multiple fallback strategies:
        /// 1. DWSIM_PATH environment variable
        /// 2. DwsimPath in App.config appSettings
        /// 3. Default installation paths
        /// </summary>
        /// <returns>The resolved DWSIM assembly path.</returns>
        /// <exception cref="DirectoryNotFoundException">
        /// Thrown when no valid DWSIM path is found after trying all strategies.
        /// </exception>
        public static string ResolveDwsimPath()
        {
            Log.Information("Resolving DWSIM assembly path...");

            // Strategy 1: Environment variable
            var envPath = GetEnvironmentPath();
            if (!string.IsNullOrEmpty(envPath) && ValidatePath(envPath))
            {
                Log.Information("DWSIM path resolved from environment variable: {Path}", envPath);
                return envPath;
            }

            // Strategy 2: App.config
            var configPath = GetConfigPath();
            if (!string.IsNullOrEmpty(configPath) && ValidatePath(configPath))
            {
                Log.Information("DWSIM path resolved from App.config: {Path}", configPath);
                return configPath;
            }

            // Strategy 3: Default installation paths
            var defaultPath = GetDefaultInstallPath();
            if (!string.IsNullOrEmpty(defaultPath) && ValidatePath(defaultPath))
            {
                Log.Information("DWSIM path resolved from default installation: {Path}", defaultPath);
                return defaultPath;
            }

            // All strategies failed
            var attemptedPaths = new List<string>();
            if (!string.IsNullOrEmpty(envPath)) attemptedPaths.Add($"Environment: {envPath}");
            if (!string.IsNullOrEmpty(configPath)) attemptedPaths.Add($"Config: {configPath}");
            attemptedPaths.AddRange(DefaultInstallPaths.Select(p => $"Default: {p}"));

            var errorMessage = $"DWSIM assemblies not found. Attempted paths:\n  {string.Join("\n  ", attemptedPaths)}\n" +
                             "Please install DWSIM or set DWSIM_PATH environment variable.";

            Log.Error(errorMessage);
            throw new DirectoryNotFoundException(errorMessage);
        }

        /// <summary>
        /// Gets the DWSIM path from the DWSIM_PATH environment variable.
        /// </summary>
        /// <returns>The path from environment variable, or null if not set.</returns>
        public static string GetEnvironmentPath()
        {
            var path = Environment.GetEnvironmentVariable("DWSIM_PATH");

            if (!string.IsNullOrEmpty(path))
            {
                Log.Debug("Found DWSIM_PATH environment variable: {Path}", path);
            }
            else
            {
                Log.Debug("DWSIM_PATH environment variable not set");
            }

            return path;
        }

        /// <summary>
        /// Gets the DWSIM path from App.config appSettings["DwsimPath"].
        /// </summary>
        /// <returns>The path from configuration, or null if not set.</returns>
        public static string GetConfigPath()
        {
            try
            {
                var path = ConfigurationManager.AppSettings["DwsimPath"];

                if (!string.IsNullOrEmpty(path))
                {
                    Log.Debug("Found DwsimPath in App.config: {Path}", path);
                }
                else
                {
                    Log.Debug("DwsimPath not found in App.config");
                }

                return path;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error reading App.config");
                return null;
            }
        }

        /// <summary>
        /// Gets the DWSIM path from default installation locations.
        /// Checks common Windows installation paths.
        /// </summary>
        /// <returns>The first valid default path found, or null if none exist.</returns>
        public static string GetDefaultInstallPath()
        {
            foreach (var path in DefaultInstallPaths)
            {
                if (Directory.Exists(path))
                {
                    Log.Debug("Found DWSIM at default location: {Path}", path);
                    return path;
                }
            }

            Log.Debug("No DWSIM installation found at default paths");
            return null;
        }

        /// <summary>
        /// Validates that a path exists and contains the required DWSIM assemblies.
        /// </summary>
        /// <param name="path">The directory path to validate.</param>
        /// <returns>True if the path is valid and contains required assemblies; otherwise, false.</returns>
        public static bool ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (!Directory.Exists(path))
            {
                Log.Debug("Path does not exist: {Path}", path);
                return false;
            }

            // Check for required assemblies
            var foundAssemblies = FindAssemblies(path);
            var missingAssemblies = RequiredAssemblies.Except(foundAssemblies).ToList();

            if (missingAssemblies.Any())
            {
                Log.Debug("Path validation failed. Missing assemblies in {Path}: {MissingAssemblies}",
                    path, string.Join(", ", missingAssemblies));
                return false;
            }

            Log.Debug("Path validation succeeded: {Path}", path);
            return true;
        }

        /// <summary>
        /// Finds DWSIM assembly files in the specified directory.
        /// </summary>
        /// <param name="basePath">The directory to search for assemblies.</param>
        /// <returns>A list of found assembly file names.</returns>
        public static IEnumerable<string> FindAssemblies(string basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath) || !Directory.Exists(basePath))
            {
                return Enumerable.Empty<string>();
            }

            try
            {
                var foundAssemblies = new List<string>();

                foreach (var assemblyName in RequiredAssemblies)
                {
                    var assemblyPath = Path.Combine(basePath, assemblyName);
                    if (File.Exists(assemblyPath))
                    {
                        foundAssemblies.Add(assemblyName);
                        Log.Debug("Found assembly: {AssemblyPath}", assemblyPath);
                    }
                }

                return foundAssemblies;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error searching for assemblies in {BasePath}", basePath);
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// Gets the full path to a specific DWSIM assembly file.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly (e.g., "DWSIM.Interfaces" or "DWSIM.Interfaces.dll").</param>
        /// <returns>The full path to the assembly file.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the assembly cannot be found.</exception>
        public static string GetAssemblyPath(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                throw new ArgumentException("Assembly name cannot be null or empty", nameof(assemblyName));

            // Ensure .dll extension is present
            var fileName = assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? assemblyName
                : assemblyName + ".dll";

            var basePath = ResolveDwsimPath();
            var assemblyPath = Path.Combine(basePath, fileName);

            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException($"Assembly '{assemblyName}' not found at path: {assemblyPath}", assemblyPath);
            }

            return assemblyPath;
        }
    }
}
