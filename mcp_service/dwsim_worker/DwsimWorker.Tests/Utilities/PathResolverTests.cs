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
using System.IO;
using System.Linq;
using Xunit;
using DwsimWorker.Utilities;

namespace DwsimWorker.Tests.Utilities
{
    /// <summary>
    /// Unit tests for PathResolver static utility class.
    /// Tests all resolution strategies: environment variable, App.config, and default paths.
    /// Uses temporary directories to simulate DWSIM installations without requiring actual DWSIM.
    /// </summary>
    public class PathResolverTests : IDisposable
    {
        private readonly string _tempTestDir;
        private readonly string _originalEnvVar;

        public PathResolverTests()
        {
            // Create temporary directory for test mock DWSIM installations
            _tempTestDir = Path.Combine(Path.GetTempPath(), $"DwsimTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempTestDir);

            // Save original environment variable value
            _originalEnvVar = Environment.GetEnvironmentVariable("DWSIM_PATH");
        }

        public void Dispose()
        {
            // Restore original environment variable
            if (_originalEnvVar != null)
            {
                Environment.SetEnvironmentVariable("DWSIM_PATH", _originalEnvVar);
            }
            else
            {
                Environment.SetEnvironmentVariable("DWSIM_PATH", null);
            }

            // Clean up temporary test directory
            if (Directory.Exists(_tempTestDir))
            {
                try
                {
                    Directory.Delete(_tempTestDir, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        #region ValidatePath Tests

        [Fact]
        public void ValidatePath_WithNullPath_ReturnsFalse()
        {
            // Act
            var result = PathResolver.ValidatePath(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidatePath_WithEmptyPath_ReturnsFalse()
        {
            // Act
            var result = PathResolver.ValidatePath(string.Empty);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidatePath_WithNonExistentPath_ReturnsFalse()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_tempTestDir, "NonExistent");

            // Act
            var result = PathResolver.ValidatePath(nonExistentPath);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidatePath_WithEmptyDirectory_ReturnsFalse()
        {
            // Arrange - create empty directory
            var emptyDir = Path.Combine(_tempTestDir, "EmptyDir");
            Directory.CreateDirectory(emptyDir);

            // Act
            var result = PathResolver.ValidatePath(emptyDir);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidatePath_WithValidDwsimPath_ReturnsTrue()
        {
            // Arrange - create mock DWSIM directory with required DLLs
            var mockDwsimPath = Path.Combine(_tempTestDir, "ValidDwsim");
            Directory.CreateDirectory(mockDwsimPath);

            // Create mock DWSIM assemblies
            CreateMockAssembly(mockDwsimPath, "DWSIM.Interfaces.dll");
            CreateMockAssembly(mockDwsimPath, "DWSIM.Thermodynamics.dll");
            CreateMockAssembly(mockDwsimPath, "DWSIM.SharedClasses.dll");

            // Act
            var result = PathResolver.ValidatePath(mockDwsimPath);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidatePath_WithPartialDwsimInstall_ReturnsFalse()
        {
            // Arrange - create directory with only one DWSIM assembly (incomplete)
            var partialInstallPath = Path.Combine(_tempTestDir, "PartialDwsim");
            Directory.CreateDirectory(partialInstallPath);
            CreateMockAssembly(partialInstallPath, "DWSIM.Interfaces.dll");

            // Act
            var result = PathResolver.ValidatePath(partialInstallPath);

            // Assert
            Assert.False(result); // Should fail because not all required assemblies present
        }

        #endregion

        #region FindAssemblies Tests

        [Fact]
        public void FindAssemblies_WithValidPath_ReturnsAssemblyNames()
        {
            // Arrange
            var mockDwsimPath = Path.Combine(_tempTestDir, "FindAssembliesTest");
            Directory.CreateDirectory(mockDwsimPath);

            CreateMockAssembly(mockDwsimPath, "DWSIM.Interfaces.dll");
            CreateMockAssembly(mockDwsimPath, "DWSIM.Thermodynamics.dll");
            CreateMockAssembly(mockDwsimPath, "DWSIM.SharedClasses.dll");

            // Act
            var assemblies = PathResolver.FindAssemblies(mockDwsimPath).ToList();

            // Assert
            Assert.NotEmpty(assemblies);
            Assert.Contains(assemblies, a => a.Contains("DWSIM.Interfaces.dll"));
            Assert.Contains(assemblies, a => a.Contains("DWSIM.Thermodynamics.dll"));
            Assert.Contains(assemblies, a => a.Contains("DWSIM.SharedClasses.dll"));
        }

        [Fact]
        public void FindAssemblies_WithEmptyDirectory_ReturnsEmpty()
        {
            // Arrange
            var emptyDir = Path.Combine(_tempTestDir, "EmptyForFind");
            Directory.CreateDirectory(emptyDir);

            // Act
            var assemblies = PathResolver.FindAssemblies(emptyDir).ToList();

            // Assert
            Assert.Empty(assemblies);
        }

        [Fact]
        public void FindAssemblies_WithNonExistentPath_ReturnsEmpty()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_tempTestDir, "NonExistentForFind");

            // Act
            var assemblies = PathResolver.FindAssemblies(nonExistentPath).ToList();

            // Assert
            Assert.Empty(assemblies);
        }

        #endregion

        #region GetEnvironmentPath Tests

        [Fact]
        public void GetEnvironmentPath_WithEnvironmentVariableSet_ReturnsPath()
        {
            // Arrange
            var expectedPath = Path.Combine(_tempTestDir, "EnvVarTest");
            Directory.CreateDirectory(expectedPath);
            Environment.SetEnvironmentVariable("DWSIM_PATH", expectedPath);

            // Act
            var result = PathResolver.GetEnvironmentPath();

            // Assert
            Assert.Equal(expectedPath, result);
        }

        [Fact]
        public void GetEnvironmentPath_WithoutEnvironmentVariable_ReturnsNull()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DWSIM_PATH", null);

            // Act
            var result = PathResolver.GetEnvironmentPath();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetEnvironmentPath_WithEmptyEnvironmentVariable_ReturnsNull()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DWSIM_PATH", "");

            // Act
            var result = PathResolver.GetEnvironmentPath();

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region GetDefaultInstallPath Tests

        [Fact]
        public void GetDefaultInstallPath_ReturnsPathOrNull()
        {
            // Act
            var result = PathResolver.GetDefaultInstallPath();

            // Assert
            // Path can be null if DWSIM isn't installed in default location
            if (result != null)
            {
                Assert.Contains("DWSIM", result);
                // Default path should be in Program Files
                Assert.True(result.Contains("Program Files") || result.Contains("ProgramFiles"));
            }
            // Test passes whether result is null or a valid path
        }

        #endregion

        #region GetAssemblyPath Tests

        [Fact]
        public void GetAssemblyPath_WithValidAssemblyName_ReturnsFullPath()
        {
            // This test verifies that GetAssemblyPath can find assemblies using default resolution
            // (App.config, environment variables, or default paths)

            // Arrange
            Environment.SetEnvironmentVariable("DWSIM_PATH", null); // Clear env var to test fallback
            var assemblyName = "DWSIM.Interfaces";

            // Act
            var result = PathResolver.GetAssemblyPath(assemblyName);

            // Assert
            Assert.NotNull(result);
            Assert.EndsWith("DWSIM.Interfaces.dll", result);
            Assert.True(File.Exists(result), $"Assembly file should exist at: {result}");
        }

        [Fact]
        public void GetAssemblyPath_WithNullAssemblyName_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => PathResolver.GetAssemblyPath(null));
        }

        [Fact]
        public void GetAssemblyPath_WithEmptyAssemblyName_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => PathResolver.GetAssemblyPath(""));
        }

        #endregion

        #region ResolveDwsimPath Tests

        [Fact]
        public void ResolveDwsimPath_WithValidEnvironmentVariable_ReturnsEnvPath()
        {
            // Arrange - create valid mock DWSIM path and set environment variable
            var mockDwsimPath = Path.Combine(_tempTestDir, "EnvResolutionTest");
            Directory.CreateDirectory(mockDwsimPath);
            CreateMockAssembly(mockDwsimPath, "DWSIM.Interfaces.dll");
            CreateMockAssembly(mockDwsimPath, "DWSIM.Thermodynamics.dll");
            CreateMockAssembly(mockDwsimPath, "DWSIM.SharedClasses.dll");

            Environment.SetEnvironmentVariable("DWSIM_PATH", mockDwsimPath);

            // Act
            var result = PathResolver.ResolveDwsimPath();

            // Assert
            Assert.Equal(mockDwsimPath, result);
        }

        [Fact]
        public void ResolveDwsimPath_WithoutEnvironmentVariable_UsesAppConfigFallback()
        {
            // Arrange - clear environment variable to test App.config fallback
            Environment.SetEnvironmentVariable("DWSIM_PATH", null);

            // Act
            var result = PathResolver.ResolveDwsimPath();

            // Assert - Should successfully find path via App.config fallback
            Assert.NotNull(result);
            Assert.True(Directory.Exists(result),
                $"Should resolve path via App.config fallback, got: {result}");
        }

        [Fact]
        public void ResolveDwsimPath_WithInvalidEnvironmentVariable_FallsBackToDefault()
        {
            // Arrange - set invalid environment variable to test fallback behavior
            Environment.SetEnvironmentVariable("DWSIM_PATH", "C:\\InvalidNonExistent\\Path");

            // Act
            var result = PathResolver.ResolveDwsimPath();

            // Assert - Should successfully fall back to App.config or default paths
            Assert.NotNull(result);
            Assert.True(Directory.Exists(result),
                $"Fallback resolution should find valid DWSIM path, got: {result}");

            // Verify it's not the invalid env var path
            Assert.NotEqual("C:\\InvalidNonExistent\\Path", result);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void PathResolution_FullWorkflow_WithMockInstallation()
        {
            // Arrange - simulate complete DWSIM installation
            var mockInstallPath = Path.Combine(_tempTestDir, "FullWorkflowTest");
            Directory.CreateDirectory(mockInstallPath);

            CreateMockAssembly(mockInstallPath, "DWSIM.Interfaces.dll");
            CreateMockAssembly(mockInstallPath, "DWSIM.Thermodynamics.dll");
            CreateMockAssembly(mockInstallPath, "DWSIM.SharedClasses.dll");
            CreateMockAssembly(mockInstallPath, "CapeOpen.dll");

            Environment.SetEnvironmentVariable("DWSIM_PATH", mockInstallPath);

            // Act - resolve path, validate, find assemblies
            var resolvedPath = PathResolver.ResolveDwsimPath();
            var isValid = PathResolver.ValidatePath(resolvedPath);
            var foundAssemblies = PathResolver.FindAssemblies(resolvedPath).ToList();

            // Assert
            Assert.Equal(mockInstallPath, resolvedPath);
            Assert.True(isValid);
            Assert.NotEmpty(foundAssemblies);
            Assert.True(foundAssemblies.Count >= 3); // At least the three core assemblies
        }

        #endregion

        #region Wheel-Install Layout Tests

        [Fact]
        public void GetJsonConfigPath_WithWheelInstallConfigCandidate_ReturnsConfiguredPath()
        {
            // Arrange
            var assemblyDir = CreateWheelInstallAssemblyDirectory(out var wheelWorkerDir);
            var expectedDwsimPath = Path.GetFullPath(Path.Combine(wheelWorkerDir, "dwsim_binaries", "x64", "Debug"));
            var configPath = Path.Combine(wheelWorkerDir, "dwsim.config.json");
            File.WriteAllText(configPath, "{ \"dwsim_path\": \"dwsim_binaries/x64/Debug\" }");

            // Act
            var result = PathResolver.GetJsonConfigPath(assemblyDir);

            // Assert
            Assert.Equal(expectedDwsimPath, result);
        }

        [Fact]
        public void GetRelativeBinariesPath_WithWheelInstallBinariesCandidate_ReturnsBinariesPath()
        {
            // Arrange
            var assemblyDir = CreateWheelInstallAssemblyDirectory(out var wheelWorkerDir);
            var expectedBinariesPath = Path.GetFullPath(Path.Combine(wheelWorkerDir, "dwsim_binaries", "x64", "Debug"));
            Directory.CreateDirectory(expectedBinariesPath);
            CreateRequiredMockAssemblies(expectedBinariesPath);

            // Act
            var result = PathResolver.GetRelativeBinariesPath(assemblyDir);

            // Assert
            Assert.Equal(expectedBinariesPath, result);
        }

        #endregion

        #region Helper Methods

        private string CreateWheelInstallAssemblyDirectory(out string wheelWorkerDir)
        {
            var workerDir = Path.Combine(_tempTestDir, "WheelInstall", "dwsim_mcp_server", "_prebuilt");
            var assemblyDir = Path.Combine(workerDir, "DwsimWorker", "bin", "Debug");
            wheelWorkerDir = Path.Combine(workerDir, "dwsim_worker");

            Directory.CreateDirectory(assemblyDir);
            Directory.CreateDirectory(wheelWorkerDir);

            return assemblyDir;
        }

        private void CreateRequiredMockAssemblies(string directory)
        {
            foreach (var assemblyName in PathResolver.RequiredAssemblies)
            {
                CreateMockAssembly(directory, assemblyName);
            }
        }

        /// <summary>
        /// Creates an empty mock assembly file for testing purposes.
        /// </summary>
        private void CreateMockAssembly(string directory, string fileName)
        {
            var filePath = Path.Combine(directory, fileName);
            File.WriteAllText(filePath, "Mock assembly for testing");
        }

        #endregion
    }
}
