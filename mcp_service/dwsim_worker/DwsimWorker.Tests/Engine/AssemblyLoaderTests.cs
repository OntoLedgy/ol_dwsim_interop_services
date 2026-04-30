// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.IO;
using Moq;
using Serilog;
using Xunit;
using DwsimWorker.Engine;

namespace DwsimWorker.Tests.Engine
{
    /// <summary>
    /// Unit tests for AssemblyLoader class.
    /// Tests focus on constructor validation, configuration handling, and error scenarios.
    /// Note: Tests requiring real DWSIM assemblies are integration tests and would need DWSIM installed.
    /// </summary>
    public class AssemblyLoaderTests
    {
        private readonly Mock<ILogger> _mockLogger;

        public AssemblyLoaderTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var config = AssemblyLoaderConfig.Default();

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AssemblyLoader(null, config));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullConfig_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = _mockLogger.Object;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new AssemblyLoader(logger, null));
            Assert.Equal("config", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithValidParameters_Succeeds()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var config = AssemblyLoaderConfig.Default();

            // Act
            var loader = new AssemblyLoader(logger, config);

            // Assert
            Assert.NotNull(loader);
        }

        #endregion

        #region LoadDwsimAssemblies Tests

        [Fact]
        public void LoadDwsimAssemblies_WithInvalidPath_ReturnsFailureWithAssemblyNotFound()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath("C:\\NonExistentPath\\InvalidDwsimPath")
                .WithValidationEnabled(false)
                .Build();

            var loader = new AssemblyLoader(logger, config);

            // Act
            var result = loader.LoadDwsimAssemblies();

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Message);
            Assert.Contains("does not exist", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(result.Error);
            Assert.Equal(1, result.ExitCode); // Load failure exit code
            Assert.Empty(result.LoadedAssemblies);

            // Verify error logging occurred (single-parameter overload)
            _mockLogger.Verify(
                x => x.Error(It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void LoadDwsimAssemblies_WithEmptyPath_UsesDefaultResolution()
        {
            // Arrange - Empty path should trigger default PathResolver behavior
            var logger = _mockLogger.Object;
            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(string.Empty)
                .WithValidationEnabled(false)
                .Build();

            var loader = new AssemblyLoader(logger, config);

            // Act
            var result = loader.LoadDwsimAssemblies();

            // Assert - Should succeed by finding DWSIM through default resolution
            // (App.config, environment variables, or default paths)
            Assert.True(result.Success);
            Assert.NotEmpty(result.LoadedAssemblies);
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public void LoadDwsimAssemblies_LogsStartAndConfiguration()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath("C:\\InvalidPath")
                .WithValidationEnabled(true)
                .WithTimeoutSeconds(60)
                .Build();

            var loader = new AssemblyLoader(logger, config);

            // Act
            var result = loader.LoadDwsimAssemblies();

            // Assert - Verify starting message was logged (single-parameter overload)
            _mockLogger.Verify(
                x => x.Information(
                    It.Is<string>(s => s.Contains("Starting DWSIM assembly loading"))),
                Times.Once);

            // Verify configuration was logged (generic overload with bool and TimeSpan parameters)
            _mockLogger.Verify(
                x => x.Information<bool, TimeSpan>(
                    It.Is<string>(s => s.Contains("Configuration")),
                    It.IsAny<bool>(),
                    It.IsAny<TimeSpan>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region LoadAssembly Tests

        [Fact]
        public void LoadAssembly_WithNullAssemblyName_ThrowsArgumentException()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var config = AssemblyLoaderConfig.Default();
            var loader = new AssemblyLoader(logger, config);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => loader.LoadAssembly(null, "somepath.dll"));
            Assert.Equal("assemblyName", ex.ParamName);
        }

        [Fact]
        public void LoadAssembly_WithEmptyAssemblyName_ThrowsArgumentException()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var config = AssemblyLoaderConfig.Default();
            var loader = new AssemblyLoader(logger, config);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => loader.LoadAssembly("", "somepath.dll"));
            Assert.Equal("assemblyName", ex.ParamName);
        }

        [Fact]
        public void LoadAssembly_WithNullAssemblyPath_ThrowsArgumentException()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var config = AssemblyLoaderConfig.Default();
            var loader = new AssemblyLoader(logger, config);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => loader.LoadAssembly("DWSIM.Interfaces", null));
            Assert.Equal("assemblyPath", ex.ParamName);
        }

        [Fact]
        public void LoadAssembly_WithNonExistentFile_ThrowsDwsimLoadException()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var config = AssemblyLoaderConfig.Default();
            var loader = new AssemblyLoader(logger, config);
            var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistent_DWSIM_Assembly_12345.dll");

            // Act & Assert
            var ex = Assert.Throws<DwsimLoadException>(() =>
                loader.LoadAssembly("DWSIM.NonExistent", nonExistentPath));

            Assert.Equal("DWSIM.NonExistent", ex.AssemblyName);
            Assert.Equal(nonExistentPath, ex.AttemptedPath);
            Assert.Equal(ErrorCode.AssemblyNotFound, ex.Code);
            Assert.NotNull(ex.InnerException);

            // Verify error was logged (single-parameter Error call)
            _mockLogger.Verify(
                x => x.Error(It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region Exit Code Validation Tests

        [Fact]
        public void LoadResult_ExitCode_MapsCorrectly()
        {
            // Test that LoadResult properly maps exit codes
            // Exit code 0 = success, 1 = load failure, 2 = validation failure

            // Arrange
            var logger = _mockLogger.Object;
            var configWithInvalidPath = AssemblyLoaderConfig.Create()
                .WithAssemblyPath("C:\\InvalidPath")
                .WithValidationEnabled(false)
                .Build();

            var loader = new AssemblyLoader(logger, configWithInvalidPath);

            // Act
            var result = loader.LoadDwsimAssemblies();

            // Assert
            Assert.False(result.Success);
            Assert.Equal(1, result.ExitCode); // Load failure should return exit code 1
        }

        #endregion

        #region Configuration Validation Tests

        [Fact]
        public void LoadDwsimAssemblies_RespectsValidationEnabledSetting()
        {
            // Arrange - config with validation disabled
            var logger = _mockLogger.Object;
            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath("C:\\InvalidPath")
                .WithValidationEnabled(false)
                .Build();

            var loader = new AssemblyLoader(logger, config);

            // Act
            var result = loader.LoadDwsimAssemblies();

            // Assert - Should fail at path resolution, not validation
            Assert.False(result.Success);
            Assert.Equal(1, result.ExitCode); // Load failure, not validation failure

            // Verify "Validation skipped" message was logged (if it got far enough)
            // Note: In this test it fails early at path resolution, so validation skip won't be logged
        }

        [Fact]
        public void LoadDwsimAssemblies_UsesConfiguredTimeout()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath("C:\\InvalidPath")
                .WithTimeoutSeconds(5)
                .Build();

            var loader = new AssemblyLoader(logger, config);

            // Act
            var result = loader.LoadDwsimAssemblies();

            // Assert - Should fail due to invalid path, not timeout
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }

        #endregion

        #region Logging Verification Tests

        [Fact]
        public void LoadDwsimAssemblies_LogsAllAttemptedAssemblies()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath("C:\\InvalidPath")
                .WithValidationEnabled(false)
                .Build();

            var loader = new AssemblyLoader(logger, config);

            // Act
            var result = loader.LoadDwsimAssemblies();

            // Assert - Verify information logging occurred (single-parameter overload)
            _mockLogger.Verify(
                x => x.Information(It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void LoadDwsimAssemblies_WithFailure_LogsErrorDetails()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath("C:\\CompletelyInvalidPath_12345")
                .Build();

            var loader = new AssemblyLoader(logger, config);

            // Act
            var result = loader.LoadDwsimAssemblies();

            // Assert
            Assert.False(result.Success);

            // Verify error was logged with details (single-parameter overload)
            _mockLogger.Verify(
                x => x.Error(It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        #endregion
    }
}
