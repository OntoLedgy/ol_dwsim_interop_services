// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

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
    /// Unit tests for CompoundAdapter class.
    /// Tests focus on compound database access, validation, and retrieval.
    /// Note: These tests require DWSIM assemblies to be installed (integration test flavor).
    /// </summary>
    public class CompoundAdapterTests : IDisposable
    {
        private readonly Mock<ILogger> _mockLogger;
        private FlowsheetContext _context;
        private CompoundAdapter _adapter;

        public CompoundAdapterTests()
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
                .WithValidation(false)  // Disable validation for faster tests
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            _adapter = new CompoundAdapter(logger, _context);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new CompoundAdapter(null, _context));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullContext_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = _mockLogger.Object;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new CompoundAdapter(logger, null));
            Assert.Equal("context", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithValidParameters_Succeeds()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act
            var adapter = new CompoundAdapter(_mockLogger.Object, _context);

            // Assert
            Assert.NotNull(adapter);
        }

        #endregion

        #region AddCompound Tests

        [Fact]
        public void AddCompound_ValidName_ReturnsSuccess()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act
            var result = _adapter.AddCompound("Methane");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
        }

        [Fact]
        public void AddCompound_InvalidName_ReturnsFailure()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act
            var result = _adapter.AddCompound("InvalidCompound123");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success, "Expected failure for invalid compound name");
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AddCompound_NullName_ReturnsFailure()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act
            var result = _adapter.AddCompound(null);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("cannot be null", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AddCompound_EmptyName_ReturnsFailure()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act
            var result = _adapter.AddCompound("");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("cannot be null", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AddCompound_MultipleCompounds_AllAdded()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            var compounds = new[] { "Methane", "Ethane", "Propane", "Water" };

            // Act
            foreach (var compound in compounds)
            {
                var result = _adapter.AddCompound(compound);
                Assert.True(result.Success, $"Failed to add {compound}: {result.Message}");
            }

            // Assert - verify all compounds were added
            var getResult = _adapter.GetCompounds();
            Assert.True(getResult.Success);
            Assert.Equal(compounds.Length, getResult.ValidatedTypes.Count);

            foreach (var compound in compounds)
            {
                Assert.Contains(compound, getResult.ValidatedTypes);
            }
        }

        [Fact]
        public void AddCompound_CaseInsensitive_ReturnsSuccess()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act - try different case variations
            var result1 = _adapter.AddCompound("methane");
            var result2 = _adapter.AddCompound("ETHANE");
            var result3 = _adapter.AddCompound("Propane");

            // Assert
            Assert.True(result1.Success, "Lowercase should work");
            Assert.True(result2.Success, "Uppercase should work");
            Assert.True(result3.Success, "Mixed case should work");
        }

        [Fact]
        public void AddCompound_DuplicateCompound_HandlesGracefully()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act - add same compound twice
            var result1 = _adapter.AddCompound("Methane");
            var result2 = _adapter.AddCompound("Methane");

            // Assert - both should succeed (idempotent operation)
            Assert.True(result1.Success);
            Assert.True(result2.Success);

            // Verify only one instance in list
            var getResult = _adapter.GetCompounds();
            var methaneCount = getResult.ValidatedTypes.Count(c => c == "Methane");
            Assert.Equal(1, methaneCount);
        }

        #endregion

        #region GetCompounds Tests

        [Fact]
        public void GetCompounds_AfterAdding_ReturnsAllCompounds()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            _adapter.AddCompound("Methane");
            _adapter.AddCompound("Ethane");
            _adapter.AddCompound("Propane");

            // Act
            var result = _adapter.GetCompounds();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(3, result.ValidatedTypes.Count);
            Assert.Contains("Methane", result.ValidatedTypes);
            Assert.Contains("Ethane", result.ValidatedTypes);
            Assert.Contains("Propane", result.ValidatedTypes);
        }

        [Fact]
        public void GetCompounds_EmptyFlowsheet_ReturnsEmptyList()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act
            var result = _adapter.GetCompounds();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Empty(result.ValidatedTypes);
        }

        #endregion

        #region ValidateCompoundName Tests

        [Fact]
        public void ValidateCompoundName_ValidName_ReturnsTrue()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act
            var result = _adapter.ValidateCompoundName("Methane");

            // Assert
            Assert.True(result, "Methane should be a valid compound");
        }

        [Fact]
        public void ValidateCompoundName_InvalidName_ReturnsFalse()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act
            var result = _adapter.ValidateCompoundName("InvalidCompound123");

            // Assert
            Assert.False(result, "Invalid compound should return false");
        }

        [Fact]
        public void ValidateCompoundName_NullName_ReturnsFalse()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act
            var result = _adapter.ValidateCompoundName(null);

            // Assert
            Assert.False(result, "Null should return false");
        }

        [Fact]
        public void ValidateCompoundName_EmptyName_ReturnsFalse()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act
            var result = _adapter.ValidateCompoundName("");

            // Assert
            Assert.False(result, "Empty string should return false");
        }

        [Fact]
        public void ValidateCompoundName_CaseInsensitive_ReturnsTrue()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act & Assert
            Assert.True(_adapter.ValidateCompoundName("methane"), "Lowercase should work");
            Assert.True(_adapter.ValidateCompoundName("METHANE"), "Uppercase should work");
            Assert.True(_adapter.ValidateCompoundName("Methane"), "Mixed case should work");
        }

        [Fact]
        public void ValidateCompoundName_CommonCompounds_AllValid()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            var commonCompounds = new[]
            {
                "Methane", "Ethane", "Propane", "n-Butane",
                "Water", "Nitrogen", "Oxygen", "Carbon Dioxide",
                "Benzene", "Toluene", "Methanol", "Ethanol"
            };

            // Act & Assert
            foreach (var compound in commonCompounds)
            {
                var isValid = _adapter.ValidateCompoundName(compound);
                Assert.True(isValid, $"Compound '{compound}' should be valid");
            }
        }

        #endregion

        #region GetAvailableCompounds Tests

        [Fact]
        public void GetAvailableCompounds_ReturnsNonEmptyList()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act
            var result = _adapter.GetAvailableCompounds();

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.True(result.Count >= 50, $"Expected at least 50 compounds, got {result.Count}");
        }

        [Fact]
        public void GetAvailableCompounds_ContainsCommonCompounds()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act
            var result = _adapter.GetAvailableCompounds();

            // Assert - verify common compounds are present
            Assert.Contains("Methane", result);
            Assert.Contains("Water", result);
            Assert.Contains("Benzene", result);
        }

        [Fact]
        public void GetAvailableCompounds_IsSorted()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
                return;

            InitializeContext();

            // Act
            var result = _adapter.GetAvailableCompounds();

            // Assert - verify list is sorted alphabetically
            var sortedList = result.OrderBy(c => c).ToList();
            Assert.Equal(sortedList, result);
        }

        #endregion
    }
}
