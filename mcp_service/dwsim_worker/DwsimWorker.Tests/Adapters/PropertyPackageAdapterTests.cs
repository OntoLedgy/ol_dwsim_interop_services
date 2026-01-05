using System;
using System.Linq;
using Moq;
using Serilog;
using Xunit;
using DwsimWorker.Adapters;
using DwsimWorker.Engine;

namespace DwsimWorker.Tests.Adapters
{
    /// <summary>
    /// Unit tests for PropertyPackageAdapter class.
    /// Tests focus on property package configuration and validation.
    /// Note: These tests require DWSIM assemblies to be installed.
    /// </summary>
    public class PropertyPackageAdapterTests : IDisposable
    {
        private readonly Mock<ILogger> _mockLogger;
        private FlowsheetContext _context;
        private PropertyPackageAdapter _adapter;

        public PropertyPackageAdapterTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        private void InitializeContext()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;
            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            _adapter = new PropertyPackageAdapter(logger, _context);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            InitializeContext();
            if (_context == null) return;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new PropertyPackageAdapter(null, _context));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullContext_ThrowsArgumentNullException()
        {
            var logger = _mockLogger.Object;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new PropertyPackageAdapter(logger, null));
            Assert.Equal("context", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithValidParameters_Succeeds()
        {
            InitializeContext();
            if (_context == null) return;

            var logger = _mockLogger.Object;

            // Act
            var adapter = new PropertyPackageAdapter(logger, _context);

            // Assert
            Assert.NotNull(adapter);
        }

        #endregion

        #region SetPropertyPackage Tests

        [Fact]
        public void SetPropertyPackage_WithPengRobinson_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetPropertyPackage("Peng-Robinson");

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");

            // Verify it was actually set
            var getResult = _adapter.GetPropertyPackageName();
            Assert.True(getResult.Success);
            Assert.Contains("Peng-Robinson", getResult.ValidatedTypes);
        }

        [Fact]
        public void SetPropertyPackage_WithPRAlias_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetPropertyPackage("PR");

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");

            // Verify it was normalized to canonical name
            var getResult = _adapter.GetPropertyPackageName();
            Assert.True(getResult.Success);
            Assert.Contains("Peng-Robinson", getResult.ValidatedTypes);
        }

        [Fact]
        public void SetPropertyPackage_WithSRK_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetPropertyPackage("SRK");

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");

            // Verify it was set
            var getResult = _adapter.GetPropertyPackageName();
            Assert.True(getResult.Success);
            Assert.Contains("SRK", getResult.ValidatedTypes);
        }

        [Fact]
        public void SetPropertyPackage_WithSoaveRedlichKwong_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetPropertyPackage("Soave-Redlich-Kwong");

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");

            // Verify it was normalized to SRK
            var getResult = _adapter.GetPropertyPackageName();
            Assert.True(getResult.Success);
            Assert.Contains("SRK", getResult.ValidatedTypes);
        }

        [Fact]
        public void SetPropertyPackage_WithNRTL_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetPropertyPackage("NRTL");

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");

            // Verify it was set
            var getResult = _adapter.GetPropertyPackageName();
            Assert.True(getResult.Success);
            Assert.Contains("NRTL", getResult.ValidatedTypes);
        }

        [Fact]
        public void SetPropertyPackage_WithUNIFAC_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetPropertyPackage("UNIFAC");

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");

            // Verify it was set
            var getResult = _adapter.GetPropertyPackageName();
            Assert.True(getResult.Success);
            Assert.Contains("UNIFAC", getResult.ValidatedTypes);
        }

        [Fact]
        public void SetPropertyPackage_CaseInsensitive_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Act - Test various case combinations
            var result1 = _adapter.SetPropertyPackage("peng-robinson");
            var result2 = _adapter.SetPropertyPackage("PENG-ROBINSON");
            var result3 = _adapter.SetPropertyPackage("pr");
            var result4 = _adapter.SetPropertyPackage("PR");

            // Assert
            Assert.True(result1.Success);
            Assert.True(result2.Success);
            Assert.True(result3.Success);
            Assert.True(result4.Success);
        }

        [Fact]
        public void SetPropertyPackage_WithNullName_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetPropertyPackage(null);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Property package name cannot be null or empty", result.Message);
        }

        [Fact]
        public void SetPropertyPackage_WithEmptyName_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetPropertyPackage("");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Property package name cannot be null or empty", result.Message);
        }

        [Fact]
        public void SetPropertyPackage_WithWhitespaceName_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetPropertyPackage("   ");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Property package name cannot be null or empty", result.Message);
        }

        [Fact]
        public void SetPropertyPackage_WithUnsupportedPackage_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetPropertyPackage("InvalidPackage");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Unsupported property package", result.Message);
        }

        #endregion

        #region GetPropertyPackageName Tests

        [Fact]
        public void GetPropertyPackageName_AfterSet_ReturnsPackageName()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            _adapter.SetPropertyPackage("Peng-Robinson");

            // Act
            var result = _adapter.GetPropertyPackageName();

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
            Assert.NotNull(result.ValidatedTypes);
            Assert.Single(result.ValidatedTypes);
            Assert.Contains("Peng-Robinson", result.ValidatedTypes);
        }

        [Fact]
        public void GetPropertyPackageName_WithAlias_ReturnsCanonicalName()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange - Set using alias
            _adapter.SetPropertyPackage("PR");

            // Act
            var result = _adapter.GetPropertyPackageName();

            // Assert
            Assert.True(result.Success);
            // Should return canonical name
            Assert.Contains("Peng-Robinson", result.ValidatedTypes);
        }

        [Fact]
        public void GetPropertyPackageName_BeforeSet_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.GetPropertyPackageName();

            // Assert
            Assert.False(result.Success);
            Assert.Contains("No property package has been configured", result.Message);
            Assert.NotNull(result.Error);
        }

        #endregion

        #region GetAvailablePackages Tests

        [Fact]
        public void GetAvailablePackages_ReturnsCanonicalNames()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.GetAvailablePackages();

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
            Assert.NotNull(result.ValidatedTypes);
            Assert.NotEmpty(result.ValidatedTypes);

            // Should contain the canonical package names (not aliases)
            Assert.Contains("Peng-Robinson", result.ValidatedTypes);
            Assert.Contains("SRK", result.ValidatedTypes);
            Assert.Contains("NRTL", result.ValidatedTypes);
            Assert.Contains("UNIFAC", result.ValidatedTypes);
        }

        [Fact]
        public void GetAvailablePackages_DoesNotIncludeAliases()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.GetAvailablePackages();

            // Assert
            Assert.True(result.Success);

            // Should NOT contain aliases (only canonical names)
            Assert.DoesNotContain("PR", result.ValidatedTypes); // Alias for Peng-Robinson
            Assert.DoesNotContain("Soave-Redlich-Kwong", result.ValidatedTypes); // Alias for SRK

            // Should only have 4 unique canonical names
            Assert.Equal(4, result.ValidatedTypes.Count);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void PropertyPackage_ChangePackage_UpdatesSuccessfully()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            _adapter.SetPropertyPackage("Peng-Robinson");
            var result1 = _adapter.GetPropertyPackageName();
            Assert.Contains("Peng-Robinson", result1.ValidatedTypes);

            // Act - Change to different package
            var setResult = _adapter.SetPropertyPackage("SRK");
            var result2 = _adapter.GetPropertyPackageName();

            // Assert
            Assert.True(setResult.Success);
            Assert.Contains("SRK", result2.ValidatedTypes);
            Assert.DoesNotContain("Peng-Robinson", result2.ValidatedTypes);
        }

        [Fact]
        public void PropertyPackage_SetMultipleTimes_LastSetWins()
        {
            InitializeContext();
            if (_context == null) return;

            // Act - Set multiple times
            _adapter.SetPropertyPackage("Peng-Robinson");
            _adapter.SetPropertyPackage("NRTL");
            _adapter.SetPropertyPackage("SRK");

            var result = _adapter.GetPropertyPackageName();

            // Assert
            Assert.Contains("SRK", result.ValidatedTypes);
        }

        #endregion
    }
}
