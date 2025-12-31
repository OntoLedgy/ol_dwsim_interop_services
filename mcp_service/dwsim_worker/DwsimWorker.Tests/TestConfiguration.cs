using System;
using System.IO;

namespace DwsimWorker.Tests
{
    /// <summary>
    /// Central configuration for test suite.
    /// Provides consistent DWSIM assembly path across all test types.
    /// </summary>
    public static class TestConfiguration
    {
        /// <summary>
        /// Path to DWSIM assemblies for testing.
        /// Points to main DWSIM application Debug build which contains all required DLLs.
        /// </summary>
        public static readonly string DwsimAssemblyPath = @"D:\S\C#\dwsim\DWSIM\bin\Debug";

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
                return $"DWSIM assembly path does not exist: {DwsimAssemblyPath}";
            }

            return $"DWSIM assembly path exists but required DLLs are missing: {DwsimAssemblyPath}";
        }
    }
}
