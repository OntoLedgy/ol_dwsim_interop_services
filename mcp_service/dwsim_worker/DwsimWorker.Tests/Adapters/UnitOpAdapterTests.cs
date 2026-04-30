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
using Moq;
using Serilog;
using Xunit;
using DwsimWorker.Adapters;
using DwsimWorker.Engine;
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Adapters
{
    /// <summary>
    /// Unit tests for UnitOpAdapter class.
    /// Tests focus on unit operation creation and parameter management.
    /// Note: These tests require DWSIM assemblies to be installed.
    /// </summary>
    public class UnitOpAdapterTests : IDisposable
    {
        private readonly Mock<ILogger> _mockLogger;
        private FlowsheetContext _context;
        private UnitOpAdapter _adapter;

        public UnitOpAdapterTests()
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

            _adapter = new UnitOpAdapter(logger, _context);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            InitializeContext();
            if (_context == null) return; // Skip if DWSIM not available

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new UnitOpAdapter(null, _context));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullContext_ThrowsArgumentNullException()
        {
            var logger = _mockLogger.Object;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new UnitOpAdapter(logger, null));
            Assert.Equal("context", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithValidParameters_Succeeds()
        {
            InitializeContext();
            if (_context == null) return;

            var logger = _mockLogger.Object;

            // Act
            var adapter = new UnitOpAdapter(logger, _context);

            // Assert
            Assert.NotNull(adapter);
        }

        #endregion

        #region AddThreePhaseSeparator Tests

        [Fact]
        public void AddThreePhaseSeparator_WithValidName_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.AddThreePhaseSeparator("SEP-101", null);

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
            Assert.StartsWith("U", result.ObjectId); // Should contain generated unit ID (e.g., "U1")
        }

        [Fact]
        public void AddThreePhaseSeparator_WithConfig_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var config = new UnitOpConfig(new Dictionary<string, object>
            {
                { "PressureDrop", 5000.0 }
            });

            // Act
            var result = _adapter.AddThreePhaseSeparator("SEP-102", config);

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
            Assert.StartsWith("U", result.ObjectId);
        }

        [Fact]
        public void AddThreePhaseSeparator_WithNullName_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.AddThreePhaseSeparator(null, null);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Unit operation name cannot be null or empty", result.Message);
        }

        [Fact]
        public void AddThreePhaseSeparator_WithEmptyName_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.AddThreePhaseSeparator("", null);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Unit operation name cannot be null or empty", result.Message);
        }

        [Fact]
        public void AddThreePhaseSeparator_GeneratesUniqueIds()
        {
            InitializeContext();
            if (_context == null) return;

            // Act - Create two separators
            var result1 = _adapter.AddThreePhaseSeparator("SEP-101", null);
            var result2 = _adapter.AddThreePhaseSeparator("SEP-102", null);

            // Assert - Both should succeed with different IDs
            Assert.True(result1.Success, $"First separator creation failed: {result1.Message}");
            Assert.True(result2.Success, $"Second separator creation failed: {result2.Message}");

            // The generated unit IDs should be different
            Assert.NotEqual(result1.ObjectId, result2.ObjectId);
        }

        #endregion

        #region SetParameter Tests

        [Fact]
        public void SetParameter_WithValidParameters_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var createResult = _adapter.AddThreePhaseSeparator("SEP-101", null);
            Assert.True(createResult.Success);
            var unitId = createResult.ObjectId;

            // Act
            var result = _adapter.SetParameter(unitId, "PressureDrop", 10000.0);

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
        }

        [Fact]
        public void SetParameter_WithNullUnitId_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetParameter(null, "PressureDrop", 10000.0);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Unit ID cannot be null or empty", result.Message);
        }

        [Fact]
        public void SetParameter_WithEmptyUnitId_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetParameter("", "PressureDrop", 10000.0);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Unit ID cannot be null or empty", result.Message);
        }

        [Fact]
        public void SetParameter_WithNullParameterName_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var createResult = _adapter.AddThreePhaseSeparator("SEP-101", null);
            Assert.True(createResult.Success);
            var unitId = createResult.ObjectId;

            // Act
            var result = _adapter.SetParameter(unitId, null, 10000.0);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Parameter name cannot be null or empty", result.Message);
        }

        [Fact]
        public void SetParameter_WithNonExistentUnit_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.SetParameter("NONEXISTENT", "PressureDrop", 10000.0);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
        }

        #endregion

        #region GetParameter Tests

        [Fact]
        public void GetParameter_WithValidParameters_ReturnsValue()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var config = new UnitOpConfig(new Dictionary<string, object>
            {
                { "PressureDrop", 5000.0 }
            });
            var createResult = _adapter.AddThreePhaseSeparator("SEP-101", config);
            Assert.True(createResult.Success);
            var unitId = createResult.ObjectId;

            // Act
            var result = _adapter.GetParameter(unitId, "PressureDrop");

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
            Assert.NotNull(result.Data);
            var value = Convert.ToDouble(result.Data);
            Assert.Equal(5000.0, value, 0.1);
        }

        [Fact]
        public void GetParameter_WithNonExistentUnit_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.GetParameter("NONEXISTENT", "PressureDrop");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
        }

        #endregion

        #region GetPorts Tests

        [Fact]
        public void GetPorts_WithValidUnit_ReturnsPortList()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var createResult = _adapter.AddThreePhaseSeparator("SEP-101", null);
            Assert.True(createResult.Success);
            var unitId = createResult.ObjectId;

            // Act
            var result = _adapter.GetPorts(unitId);

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
            Assert.NotNull(result.ValidatedTypes);
            Assert.NotEmpty(result.ValidatedTypes);

            // Three-phase separator ports are returned in "DisplayName:InternalName" format
            Assert.Contains("Inlet:inlet", result.ValidatedTypes);
            Assert.Contains("VaporOutlet:vapor_outlet", result.ValidatedTypes);
            Assert.Contains("LiquidOutlet1:liquid_outlet", result.ValidatedTypes);
            Assert.Contains("LiquidOutlet2:heavy_liquid_outlet", result.ValidatedTypes);
        }

        [Fact]
        public void GetPorts_WithNonExistentUnit_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.GetPorts("NONEXISTENT");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
        }

        #endregion
    }
}
