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
using Moq;
using Serilog;
using Xunit;
using DwsimWorker.Adapters;
using DwsimWorker.Engine;
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Adapters
{
    /// <summary>
    /// Unit tests for StreamAdapter class.
    /// Tests focus on stream creation, property get/set, and composition management.
    /// Note: These tests require DWSIM assemblies to be installed.
    /// </summary>
    public class StreamAdapterTests : IDisposable
    {
        private readonly Mock<ILogger> _mockLogger;
        private FlowsheetContext _context;
        private StreamAdapter _adapter;

        public StreamAdapterTests()
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

            // Add compounds to flowsheet (required before creating streams with compositions)
            // Composition in CreateTestStreamProperties has 2 mole fractions, so we need 2 compounds
            var compoundAdapter = new CompoundAdapter(logger, _context);
            compoundAdapter.AddCompound("Water");    // Index 0
            compoundAdapter.AddCompound("Ethanol");  // Index 1

            _adapter = new StreamAdapter(logger, _context);
        }

        private StreamProperties CreateTestStreamProperties()
        {
            // Create units
            var kelvinUnit = new UnitsOfMeasure("K", typeof(Temperature),
                new Ranges(0, double.PositiveInfinity));
            var pascalUnit = new UnitsOfMeasure("Pa", typeof(Pressure),
                new Ranges(0, double.PositiveInfinity));
            var molPerSecUnit = new UnitsOfMeasure("mol/s", typeof(MolarFlowRate),
                new Ranges(0, double.PositiveInfinity));

            // Create measurements
            var tempMeasurement = new Measurements(new Temperature(), 298.15, kelvinUnit);
            var pressMeasurement = new Measurements(new Pressure(), 101325, pascalUnit);
            var flowMeasurement = new Measurements(new MolarFlowRate(), 1.0, molPerSecUnit);

            // Create physical properties
            var tempProperty = new PhysicalProperties("Temperature", tempMeasurement);
            var pressProperty = new PhysicalProperties("Pressure", pressMeasurement);
            var flowProperty = new PhysicalProperties("MolarFlow", flowMeasurement);

            // Create composition (mole fractions for compounds in flowsheet order)
            // Note: Assumes compounds have been added to flowsheet already
            var composition = new Composition(new[] { 0.5, 0.5 }); // 50% each compound

            return new StreamProperties(tempProperty, pressProperty, flowProperty, composition);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            InitializeContext();
            if (_context == null) return; // Skip if DWSIM not available

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new StreamAdapter(null, _context));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullContext_ThrowsArgumentNullException()
        {
            var logger = _mockLogger.Object;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new StreamAdapter(logger, null));
            Assert.Equal("context", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithValidParameters_Succeeds()
        {
            InitializeContext();
            if (_context == null) return;

            var logger = _mockLogger.Object;

            // Act
            var adapter = new StreamAdapter(logger, _context);

            // Assert
            Assert.NotNull(adapter);
        }

        #endregion

        #region CreateStream Tests

        [Fact]
        public void CreateStream_WithValidProperties_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();

            // Act
            var result = _adapter.CreateStream("FEED", properties);

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
            Assert.StartsWith("S", result.ObjectId); // Should contain generated stream ID (e.g., "S1")
        }

        [Fact]
        public void CreateStream_WithNullName_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();

            // Act
            var result = _adapter.CreateStream(null, properties);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Stream name cannot be null or empty", result.Message);
        }

        [Fact]
        public void CreateStream_WithEmptyName_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();

            // Act
            var result = _adapter.CreateStream("", properties);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Stream name cannot be null or empty", result.Message);
        }

        [Fact]
        public void CreateStream_WithNullProperties_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.CreateStream("FEED", null);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Stream properties cannot be null", result.Message);
        }

        [Fact]
        public void CreateStream_WithSameName_GeneratesUniqueIds()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();

            // Act - Create two streams with same name
            // Note: Implementation generates unique IDs (S1, S2, etc.) so duplicate names are allowed
            var result1 = _adapter.CreateStream("FEED", properties);
            var result2 = _adapter.CreateStream("FEED", properties);

            // Assert - Both should succeed with different IDs
            Assert.True(result1.Success, $"First stream creation failed: {result1.ObjectId}");
            Assert.True(result2.Success, $"Second stream creation failed: {result2.ObjectId}");

            // The generated stream IDs should be different
            Assert.NotEqual(result1.ObjectId, result2.ObjectId);
        }

        #endregion

        #region SetProperty Tests

        [Fact]
        public void SetProperty_Temperature_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();
            var createResult = _adapter.CreateStream("FEED", properties);
            Assert.True(createResult.Success);
            var streamId = createResult.ObjectId;

            // Act
            var result = _adapter.SetProperty(streamId, "temperature", 323.15);

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
        }

        [Fact]
        public void SetProperty_Pressure_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();
            var createResult = _adapter.CreateStream("FEED", properties);
            Assert.True(createResult.Success);
            var streamId = createResult.ObjectId;

            // Act
            var result = _adapter.SetProperty(streamId, "pressure", 200000);

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
        }

        [Fact]
        public void SetProperty_NonExistentStream_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetProperty("NONEXISTENT", "temperature", 298.15);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
        }

        [Fact]
        public void SetProperty_InvalidPropertyName_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();
            var createResult = _adapter.CreateStream("FEED", properties);
            Assert.True(createResult.Success);
            var streamId = createResult.ObjectId;

            // Act
            var result = _adapter.SetProperty(streamId, "invalid_property", 123);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Invalid property name", result.Message);
        }

        #endregion

        #region GetProperty Tests

        [Fact]
        public void GetProperty_Temperature_ReturnsValue()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();
            var createResult = _adapter.CreateStream("FEED", properties);
            Assert.True(createResult.Success);
            var streamId = createResult.ObjectId;

            // Act
            var result = _adapter.GetProperty(streamId, "temperature");

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
            Assert.NotNull(result.Data);
            var temp = Convert.ToDouble(result.Data);
            Assert.True(temp > 0, "Temperature should be positive");
        }

        [Fact]
        public void GetProperty_Pressure_ReturnsValue()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();
            var createResult = _adapter.CreateStream("FEED", properties);
            Assert.True(createResult.Success);
            var streamId = createResult.ObjectId;

            // Act
            var result = _adapter.GetProperty(streamId, "pressure");

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
            Assert.NotNull(result.Data);
            var pressure = Convert.ToDouble(result.Data);
            Assert.True(pressure > 0, "Pressure should be positive");
        }

        [Fact]
        public void GetProperty_NonExistentStream_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.GetProperty("NONEXISTENT", "temperature");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
        }

        #endregion

        #region SetProperties Tests

        [Fact]
        public void SetProperties_UpdatesAllProperties_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var initialProps = CreateTestStreamProperties();
            var createResult = _adapter.CreateStream("FEED", initialProps);
            Assert.True(createResult.Success);
            var streamId = createResult.ObjectId;

            // Create new properties with different values
            var kelvinUnit = new UnitsOfMeasure("K", typeof(Temperature),
                new Ranges(0, double.PositiveInfinity));
            var pascalUnit = new UnitsOfMeasure("Pa", typeof(Pressure),
                new Ranges(0, double.PositiveInfinity));
            var molPerSecUnit = new UnitsOfMeasure("mol/s", typeof(MolarFlowRate),
                new Ranges(0, double.PositiveInfinity));

            var newTempMeasurement = new Measurements(new Temperature(), 350.0, kelvinUnit);
            var newPressMeasurement = new Measurements(new Pressure(), 300000, pascalUnit);
            var newFlowMeasurement = new Measurements(new MolarFlowRate(), 2.0, molPerSecUnit);

            var newTempProperty = new PhysicalProperties("Temperature", newTempMeasurement);
            var newPressProperty = new PhysicalProperties("Pressure", newPressMeasurement);
            var newFlowProperty = new PhysicalProperties("MolarFlow", newFlowMeasurement);

            var newComposition = new Composition(new[] { 0.7, 0.3 }); // 70/30 split

            var newProps = new StreamProperties(newTempProperty, newPressProperty,
                newFlowProperty, newComposition);

            // Act
            var result = _adapter.SetProperties(streamId, newProps);

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
        }

        [Fact]
        public void SetProperties_NonExistentStream_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();

            // Act
            var result = _adapter.SetProperties("NONEXISTENT", properties);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
        }

        #endregion

        #region GetProperties Tests

        [Fact]
        public void GetProperties_ReturnsStreamProperties()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();
            var createResult = _adapter.CreateStream("FEED", properties);
            Assert.True(createResult.Success);
            var streamId = createResult.ObjectId;

            // Act
            var result = _adapter.GetProperties(streamId);

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
            Assert.NotNull(result.Data);

            var retrievedProps = result.Data as StreamProperties;
            Assert.NotNull(retrievedProps);
            Assert.NotNull(retrievedProps.Temperature);
            Assert.NotNull(retrievedProps.Pressure);
            Assert.NotNull(retrievedProps.MolarFlow);
            Assert.NotNull(retrievedProps.Composition);
        }

        [Fact]
        public void GetProperties_NonExistentStream_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.GetProperties("NONEXISTENT");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
        }

        #endregion

        #region Composition Tests

        [Fact]
        public void CreateStream_WithInvalidComposition_ThrowsArgumentException()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange - Try to create composition with invalid mole fractions (doesn't sum to 1.0)
            // This should throw ArgumentException from Composition constructor

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                var composition = new Composition(new[] { 0.5, 0.3 }); // Sum = 0.8, not 1.0
            });

            Assert.Contains("sum to 1.0", ex.Message);
        }

        #endregion
    }
}
