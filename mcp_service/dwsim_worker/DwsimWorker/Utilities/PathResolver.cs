// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// This file is part of the OntoLedgy Thermodynamics Architecture and is
// dual-licensed:
//
//   1. Open source under the GNU Affero General Public License v3.0 or
//      later (AGPL-3.0-or-later). See the LICENSE file in the repository
//      root for the full licence text and NOTICE for attribution.
//   2. Commercial under a separate proprietary licence offered by
//      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Serilog;

[assembly: InternalsVisibleTo("DwsimWorker.Tests")]

namespace DwsimWorker.Utilities
{
    /// <summary>
    /// Static utility class for resolving DWSIM assembly paths using multiple fallback strategies.
    /// Supports environment variables, JSON config files, App.config settings, and default installation paths.
    /// </summary>
    public static class PathResolver
    {
        /// <summary>
        /// Required DWSIM assembly file names.
        /// </summary>
        internal static readonly string[] RequiredAssemblies = new[]
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
        /// JSON config class for dwsim.config.json.
        /// </summary>
        private sealed class DwsimJsonConfig
        {
            [JsonProperty("dwsim_path")]
            public string DwsimPath { get; set; }
        }

        /// <summary>
        /// Resolves the DWSIM assembly path using multiple fallback strategies:
        /// 1. DWSIM_PATH environment variable
        /// 2. dwsim.config.json (supports relative paths)
        /// 3. DwsimPath in App.config appSettings
        /// 4. Relative dwsim_binaries folder (for downloaded binaries)
        /// 5. Default installation paths
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

            // Strategy 2: JSON config file (supports relative paths)
            var jsonConfigPath = GetJsonConfigPath();
            if (!string.IsNullOrEmpty(jsonConfigPath) && ValidatePath(jsonConfigPath))
            {
                Log.Information("DWSIM path resolved from dwsim.config.json: {Path}", jsonConfigPath);
                return jsonConfigPath;
            }

            // Strategy 3: App.config
            var configPath = GetConfigPath();
            if (!string.IsNullOrEmpty(configPath) && ValidatePath(configPath))
            {
                Log.Information("DWSIM path resolved from App.config: {Path}", configPath);
                return configPath;
            }

            // Strategy 4: Relative dwsim_binaries folder (for downloaded binaries)
            var relativeBinPath = GetRelativeBinariesPath();
            if (!string.IsNullOrEmpty(relativeBinPath) && ValidatePath(relativeBinPath))
            {
                Log.Information("DWSIM path resolved from relative binaries folder: {Path}", relativeBinPath);
                return relativeBinPath;
            }

            // Strategy 5: Default installation paths
            var defaultPath = GetDefaultInstallPath();
            if (!string.IsNullOrEmpty(defaultPath) && ValidatePath(defaultPath))
            {
                Log.Information("DWSIM path resolved from default installation: {Path}", defaultPath);
                return defaultPath;
            }

            // All strategies failed
            var attemptedPaths = new List<string>();
            if (!string.IsNullOrEmpty(envPath)) attemptedPaths.Add($"Environment: {envPath}");
            if (!string.IsNullOrEmpty(jsonConfigPath)) attemptedPaths.Add($"Config: {jsonConfigPath}");
            if (!string.IsNullOrEmpty(configPath)) attemptedPaths.Add($"AppConfig: {configPath}");
            if (!string.IsNullOrEmpty(relativeBinPath)) attemptedPaths.Add($"RelativeBin: {relativeBinPath}");
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
        /// Gets the DWSIM path from dwsim.config.json file.
        /// Supports both absolute and relative paths.
        /// </summary>
        /// <returns>The resolved path from JSON config, or null if not found.</returns>
        public static string GetJsonConfigPath()
        {
            try
            {
                // Look for dwsim.config.json in the worker directory (parent of bin/Debug)
                var assemblyPath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrWhiteSpace(assemblyPath))
                {
                    Log.Debug("Cannot determine assembly location for JSON config lookup");
                    return null;
                }

                var assemblyDir = Path.GetDirectoryName(assemblyPath);
                if (assemblyDir == null)
                {
                    return null;
                }

                return GetJsonConfigPath(assemblyDir);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error reading dwsim.config.json");
                return null;
            }
        }

        internal static string GetJsonConfigPath(string assemblyDir)
        {
            // Navigate up from bin/Debug to DwsimWorker, then to dwsim_worker
            var workerDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", ".."));
            // Probe multiple candidate locations for the config file:
            // 1. Directly in workerDir (source-tree layout)
            // 2. In workerDir/dwsim_worker (wheel-install layout)
            var configCandidates = new[]
            {
                Path.Combine(workerDir, "dwsim.config.json"),
                Path.Combine(workerDir, "dwsim_worker", "dwsim.config.json"),
            };

            var configPath = configCandidates.FirstOrDefault(File.Exists);

            if (configPath == null)
            {
                Log.Debug("dwsim.config.json not found at any candidate path");
                return null;
            }

            var text = File.ReadAllText(configPath);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var config = JsonConvert.DeserializeObject<DwsimJsonConfig>(text);
            if (string.IsNullOrWhiteSpace(config?.DwsimPath))
            {
                Log.Debug("dwsim_path not found or empty in JSON config");
                return null;
            }

            var dwsimPath = config.DwsimPath.Trim();

            // Handle relative paths - resolve relative to config file directory
            if (!Path.IsPathRooted(dwsimPath))
            {
                var configDir = Path.GetDirectoryName(configPath);
                dwsimPath = Path.GetFullPath(Path.Combine(configDir, dwsimPath));
                Log.Debug("Resolved relative path to: {Path}", dwsimPath);
            }

            Log.Debug("Found dwsim_path in JSON config: {Path}", dwsimPath);
            return dwsimPath;
        }

        /// <summary>
        /// Gets the DWSIM path from the relative dwsim_binaries folder.
        /// This is used when binaries are downloaded to the standard location.
        /// </summary>
        /// <returns>The path to relative binaries, or null if not found.</returns>
        public static string GetRelativeBinariesPath()
        {
            try
            {
                var assemblyPath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrWhiteSpace(assemblyPath))
                {
                    return null;
                }

                var assemblyDir = Path.GetDirectoryName(assemblyPath);
                if (assemblyDir == null)
                {
                    return null;
                }

                return GetRelativeBinariesPath(assemblyDir);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error checking relative binaries path");
                return null;
            }
        }

        internal static string GetRelativeBinariesPath(string assemblyDir)
        {
            // Navigate up from assembly dir and probe multiple layouts:
            // 1. workerDir/dwsim_binaries/x64/Debug (source-tree layout)
            // 2. workerDir/dwsim_worker/dwsim_binaries/x64/Debug (wheel-install layout)
            var workerDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", ".."));
            var binCandidates = new[]
            {
                Path.GetFullPath(Path.Combine(workerDir, "dwsim_binaries", "x64", "Debug")),
                Path.GetFullPath(Path.Combine(workerDir, "dwsim_worker", "dwsim_binaries", "x64", "Debug")),
            };

            var binPath = binCandidates.FirstOrDefault(Directory.Exists);

            if (binPath == null)
            {
                Log.Debug("Relative binaries folder not found at any candidate path");
                return null;
            }

            Log.Debug("Found relative binaries folder: {Path}", binPath);
            return binPath;
        }

        /// <summary>
        /// Gets the DWSIM path from App.config appSettings["DwsimPath"].
        /// </summary>
        /// <returns>The path from configuration, or null if not set.</returns>
        public static string GetConfigPath()
        {
            var mappedPath = GetMappedConfigValue("DwsimPath");
            if (!string.IsNullOrWhiteSpace(mappedPath))
            {
                Log.Debug("Found DwsimPath in mapped config: {Path}", mappedPath);
                return mappedPath;
            }

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
        /// Gets extra dependency probe paths from App.config appSettings["DwsimDependencyPaths"].
        /// </summary>
        /// <returns>Sequence of dependency paths.</returns>
        public static IReadOnlyList<string> GetDependencyPaths()
        {
            var mappedValue = GetMappedConfigValue("DwsimDependencyPaths");
            var appValue = ConfigurationManager.AppSettings["DwsimDependencyPaths"];

            var combined = string.Join(";", new[] { mappedValue, appValue }.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (string.IsNullOrWhiteSpace(combined))
            {
                return Array.Empty<string>();
            }

            return combined
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => path.Trim())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList()
                .AsReadOnly();
        }

        private static string GetMappedConfigValue(string key)
        {
            try
            {
                var configPath = GetAssemblyConfigPath();
                if (configPath == null)
                {
                    return null;
                }

                var map = new ExeConfigurationFileMap { ExeConfigFilename = configPath };
                var config = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
                var setting = config.AppSettings.Settings[key]?.Value;
                return string.IsNullOrWhiteSpace(setting) ? null : setting;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error reading mapped config for {Key}", key);
                return null;
            }
        }

        private static string GetAssemblyConfigPath()
        {
            try
            {
                var assemblyPath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrWhiteSpace(assemblyPath))
                {
                    return null;
                }

                var configPath = assemblyPath + ".config";
                return File.Exists(configPath) ? configPath : null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error locating assembly config file");
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
