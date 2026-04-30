// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.IO;
using Newtonsoft.Json;

namespace DwsimWorker.Tests
{
    /// <summary>
    /// Central configuration for test suite.
    /// Provides consistent DWSIM assembly path across all test types.
    /// </summary>
    public static class TestConfiguration
    {
        private static readonly string ConfigFilePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "dwsim.config.json"));

        /// <summary>
        /// Path to DWSIM assemblies for testing.
        /// Prefers local config file (gitignored), falls back to repo-copied binaries.
        /// </summary>
        public static readonly string DwsimAssemblyPath = ResolveDwsimPath();

        /// <summary>
        /// Validates that the DWSIM assembly path exists and contains required DLLs.
        /// </summary>
        /// <returns>True if valid; otherwise, false.</returns>
        public static bool ValidateDwsimPath()
        {
            if (!Directory.Exists(DwsimAssemblyPath))
            {
                return false;
            }

            // Check for required assemblies
            var requiredAssemblies = new[]
            {
                "DWSIM.Interfaces.dll",
                "DWSIM.Thermodynamics.dll",
                "DWSIM.SharedClasses.dll"
            };

            foreach (var assembly in requiredAssemblies)
            {
                var assemblyPath = Path.Combine(DwsimAssemblyPath, assembly);
                if (!File.Exists(assemblyPath))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets a descriptive error message if DWSIM path validation fails.
        /// </summary>
        public static string GetValidationErrorMessage()
        {
            if (!Directory.Exists(DwsimAssemblyPath))
            {
                return $"DWSIM assembly path does not exist: {DwsimAssemblyPath} (set in dwsim.config.json)";
            }

            return $"DWSIM assembly path exists but required DLLs are missing: {DwsimAssemblyPath}";
        }

        private static string ResolveDwsimPath()
        {
            var configured = ReadConfigPath();
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "dwsim_binaries",
                "x64",
                "Debug"));
        }

        private static string ReadConfigPath()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    return null;
                }

                var text = File.ReadAllText(ConfigFilePath);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                var config = JsonConvert.DeserializeObject<DwsimConfig>(text);
                if (string.IsNullOrWhiteSpace(config?.DwsimPath))
                {
                    return null;
                }

                var dwsimPath = config.DwsimPath.Trim();

                // Handle relative paths - resolve relative to config file directory
                if (!Path.IsPathRooted(dwsimPath))
                {
                    var configDir = Path.GetDirectoryName(ConfigFilePath);
                    if (!string.IsNullOrEmpty(configDir))
                    {
                        dwsimPath = Path.GetFullPath(Path.Combine(configDir, dwsimPath));
                    }
                }

                return dwsimPath;
            }
            catch
            {
                return null;
            }
        }

        private sealed class DwsimConfig
        {
            [JsonProperty("dwsim_path")]
            public string DwsimPath { get; set; }
        }
    }
}
