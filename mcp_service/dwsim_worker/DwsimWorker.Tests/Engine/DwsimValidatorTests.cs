using System;
using Moq;
using Serilog;
using Xunit;
using DwsimWorker.Engine;

namespace DwsimWorker.Tests.Engine
{
    /// <summary>
    /// Unit tests for DwsimValidator class.
    /// Note: Tests that require actual DWSIM assemblies are marked with [Trait("Category", "Integration")]
    /// and may be skipped in environments without DWSIM installed.
    /// </summary>
    public class DwsimValidatorTests
    {
        private readonly Mock<ILogger> _mockLogger;

        public DwsimValidatorTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new DwsimValidator(null));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithValidLogger_Succeeds()
        {
            // Arrange
            var logger = _mockLogger.Object;

            // Act
            var validator = new DwsimValidator(logger);

            // Assert
            Assert.NotNull(validator);
        }

        #endregion

        #region ValidateInstantiation Tests (Without DWSIM)

        [Fact]
        public void ValidateInstantiation_WithoutDwsimAssemblies_ReturnsFailure()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var validator = new DwsimValidator(logger);

            // Note: This test runs without DWSIM assemblies loaded
            // Expected to fail because types won't be found

            // Act
            var result = validator.ValidateInstantiation();

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success); // Should fail without DWSIM assemblies
            Assert.NotNull(result.Message);
            Assert.NotNull(result.Error);

            // Verify error logging occurred (single-parameter overload)
            _mockLogger.Verify(
                x => x.Error(It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void ValidateInstantiation_LogsValidationStart()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var validator = new DwsimValidator(logger);

            // Act
            var result = validator.ValidateInstantiation();

            // Assert - Verify starting message was logged (single-parameter overload)
            _mockLogger.Verify(
                x => x.Information(
                    It.Is<string>(s => s.Contains("Starting DWSIM assembly validation"))),
                Times.Once);
        }

        #endregion

        #region ValidateFlowsheetCreation Tests (Without DWSIM)

        [Fact]
        public void ValidateFlowsheetCreation_WithoutDwsimAssemblies_ReturnsFailure()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var validator = new DwsimValidator(logger);

            // Act
            var result = validator.ValidateFlowsheetCreation();

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.NotNull(result.Message);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(result.Error);
            Assert.IsType<TypeLoadException>(result.Error);

            // Verify error logging occurred (single-parameter overload)
            _mockLogger.Verify(
                x => x.Error(It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void ValidateFlowsheetCreation_LogsTypeValidationAttempt()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var validator = new DwsimValidator(logger);

            // Act
            var result = validator.ValidateFlowsheetCreation();

            // Assert - Verify debug logging occurred (generic overload with string parameter)
            _mockLogger.Verify(
                x => x.Debug<string>(
                    It.Is<string>(s => s.Contains("Attempting to validate type")),
                    It.IsAny<string>()),
                Times.Once);
        }

        #endregion

        #region ValidateMaterialStreamCreation Tests (Without DWSIM)

        [Fact]
        public void ValidateMaterialStreamCreation_WithoutDwsimAssemblies_ReturnsFailure()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var validator = new DwsimValidator(logger);

            // Act
            var result = validator.ValidateMaterialStreamCreation();

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.NotNull(result.Message);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(result.Error);
            Assert.IsType<TypeLoadException>(result.Error);
        }

        [Fact]
        public void ValidateMaterialStreamCreation_LogsTypeValidationAttempt()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var validator = new DwsimValidator(logger);

            // Act
            var result = validator.ValidateMaterialStreamCreation();

            // Assert - Verify debug logging occurred (generic overload with string parameter)
            _mockLogger.Verify(
                x => x.Debug<string>(
                    It.Is<string>(s => s.Contains("Attempting to validate type")),
                    It.IsAny<string>()),
                Times.Once);
        }

        #endregion

        #region ValidationResult Contract Tests

        [Fact]
        public void ValidationResult_FailureCase_ContainsExpectedProperties()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var validator = new DwsimValidator(logger);

            // Act
            var result = validator.ValidateFlowsheetCreation();

            // Assert - Verify ValidationResult contract
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.NotNull(result.Message);
            Assert.NotEmpty(result.Message);
            Assert.NotNull(result.Error);
            Assert.Empty(result.ValidatedTypes); // No types validated on failure
        }

        [Fact]
        public void ValidateInstantiation_ReturnsValidationResult_NotNull()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var validator = new DwsimValidator(logger);

            // Act
            var result = validator.ValidateInstantiation();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ValidationResult>(result);
        }

        #endregion

        #region Logging Verification Tests

        [Fact]
        public void ValidateFlowsheetCreation_WithFailure_LogsErrorDetails()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var validator = new DwsimValidator(logger);

            // Act
            var result = validator.ValidateFlowsheetCreation();

            // Assert
            Assert.False(result.Success);

            // Verify error was logged (single-parameter overload)
            _mockLogger.Verify(
                x => x.Error(It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void ValidateMaterialStreamCreation_WithFailure_LogsErrorDetails()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var validator = new DwsimValidator(logger);

            // Act
            var result = validator.ValidateMaterialStreamCreation();

            // Assert
            Assert.False(result.Success);

            // Verify error was logged (single-parameter overload)
            _mockLogger.Verify(
                x => x.Error(It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region Exception Handling Tests

        [Fact]
        public void ValidateFlowsheetCreation_DoesNotThrow_ReturnsValidationResult()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var validator = new DwsimValidator(logger);

            // Act & Assert - Should not throw, should return ValidationResult
            var result = validator.ValidateFlowsheetCreation();

            Assert.NotNull(result);
            Assert.False(result.Success); // Fails gracefully
        }

        [Fact]
        public void ValidateMaterialStreamCreation_DoesNotThrow_ReturnsValidationResult()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var validator = new DwsimValidator(logger);

            // Act & Assert - Should not throw, should return ValidationResult
            var result = validator.ValidateMaterialStreamCreation();

            Assert.NotNull(result);
            Assert.False(result.Success); // Fails gracefully
        }

        [Fact]
        public void ValidateInstantiation_DoesNotThrow_ReturnsValidationResult()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var validator = new DwsimValidator(logger);

            // Act & Assert - Should not throw, should return ValidationResult
            var result = validator.ValidateInstantiation();

            Assert.NotNull(result);
            // Don't assert on Success value as it depends on DWSIM availability
        }

        #endregion

        #region Integration Tests (Require DWSIM)

        // Note: These tests require actual DWSIM assemblies to be loaded and available.
        // They are marked with [Trait("Category", "Integration")] so they can be excluded
        // from regular test runs in CI/CD environments without DWSIM.

        [Fact]
        [Trait("Category", "Integration")]
        public void ValidateInstantiation_WithDwsimAssemblies_ReturnsSuccess()
        {
            // Arrange - Load DWSIM assemblies first
            var loaderConfig = AssemblyLoaderConfig.Create()
                .WithValidationEnabled(false) // Skip validation in loader since we're testing validator
                .Build();
            var loaderLogger = _mockLogger.Object;
            var loader = new AssemblyLoader(loaderLogger, loaderConfig);

            // Load DWSIM assemblies
            var loadResult = loader.LoadDwsimAssemblies();
            Assert.True(loadResult.Success, $"Failed to load DWSIM assemblies: {loadResult.Message}");

            var validator = new DwsimValidator(_mockLogger.Object);

            // Act
            var result = validator.ValidateInstantiation();

            // Assert
            Assert.True(result.Success, $"Validation failed: {result.Message}");
            Assert.NotEmpty(result.ValidatedTypes);
            Assert.Contains("DWSIM.SharedClasses.FOSSEEFlowsheet", result.ValidatedTypes);
            Assert.Contains("DWSIM.Thermodynamics.Streams.MaterialStream", result.ValidatedTypes);
            Assert.Null(result.Error);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ValidateFlowsheetCreation_WithDwsimAssemblies_ReturnsSuccess()
        {
            // Arrange - Load DWSIM assemblies first
            var loaderConfig = AssemblyLoaderConfig.Create()
                .WithValidationEnabled(false)
                .Build();
            var loader = new AssemblyLoader(_mockLogger.Object, loaderConfig);
            var loadResult = loader.LoadDwsimAssemblies();
            Assert.True(loadResult.Success, $"Failed to load DWSIM assemblies: {loadResult.Message}");

            var validator = new DwsimValidator(_mockLogger.Object);

            // Act
            var result = validator.ValidateFlowsheetCreation();

            // Assert
            Assert.True(result.Success, $"Validation failed: {result.Message}");
            Assert.Single(result.ValidatedTypes);
            Assert.Contains("DWSIM.SharedClasses.FOSSEEFlowsheet", result.ValidatedTypes);
            Assert.Null(result.Error);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ValidateMaterialStreamCreation_WithDwsimAssemblies_ReturnsSuccess()
        {
            // Arrange - Load DWSIM assemblies first
            var loaderConfig = AssemblyLoaderConfig.Create()
                .WithValidationEnabled(false)
                .Build();
            var loader = new AssemblyLoader(_mockLogger.Object, loaderConfig);
            var loadResult = loader.LoadDwsimAssemblies();
            Assert.True(loadResult.Success, $"Failed to load DWSIM assemblies: {loadResult.Message}");

            var validator = new DwsimValidator(_mockLogger.Object);

            // Act
            var result = validator.ValidateMaterialStreamCreation();

            // Assert
            Assert.True(result.Success, $"Validation failed: {result.Message}");
            Assert.Single(result.ValidatedTypes);
            Assert.Contains("DWSIM.Thermodynamics.Streams.MaterialStream", result.ValidatedTypes);
            Assert.Null(result.Error);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void ValidateInstantiation_WithDwsimAssemblies_CreatesObjectsWithoutGuiContext()
        {
            // Arrange - Load DWSIM assemblies first
            // This test validates requirement 2.3: must work without GUI context (no STA thread)
            var loaderConfig = AssemblyLoaderConfig.Create()
                .WithValidationEnabled(false)
                .Build();
            var loader = new AssemblyLoader(_mockLogger.Object, loaderConfig);
            var loadResult = loader.LoadDwsimAssemblies();
            Assert.True(loadResult.Success, $"Failed to load DWSIM assemblies: {loadResult.Message}");

            var validator = new DwsimValidator(_mockLogger.Object);

            // Act
            var result = validator.ValidateInstantiation();

            // Assert
            Assert.True(result.Success, $"Validation failed: {result.Message}");
            // If this test passes without hanging or throwing, it proves objects
            // can be instantiated without GUI context
        }

        #endregion
    }
}
