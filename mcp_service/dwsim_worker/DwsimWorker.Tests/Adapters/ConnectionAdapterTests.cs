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
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Adapters
{
    /// <summary>
    /// Unit tests for ConnectionAdapter class.
    /// Tests focus on stream-to-unit connections and topology validation.
    /// Note: These tests require DWSIM assemblies to be installed.
    /// </summary>
    public class ConnectionAdapterTests : IDisposable
    {
        private readonly Mock<ILogger> _mockLogger;
        private FlowsheetContext _context;
        private ConnectionAdapter _adapter;
        private StreamAdapter _streamAdapter;
        private UnitOpAdapter _unitOpAdapter;

        public ConnectionAdapterTests()
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

            // Add compounds for stream creation
            var compoundAdapter = new CompoundAdapter(logger, _context);
            compoundAdapter.AddCompound("Water");
            compoundAdapter.AddCompound("Ethanol");

            _adapter = new ConnectionAdapter(logger, _context);
            _streamAdapter = new StreamAdapter(logger, _context);
            _unitOpAdapter = new UnitOpAdapter(logger, _context);
        }

        private StreamProperties CreateTestStreamProperties()
        {
            var kelvinUnit = new UnitsOfMeasure("K", typeof(Temperature),
                new Ranges(0, double.PositiveInfinity));
            var pascalUnit = new UnitsOfMeasure("Pa", typeof(Pressure),
                new Ranges(0, double.PositiveInfinity));
            var molPerSecUnit = new UnitsOfMeasure("mol/s", typeof(MolarFlowRate),
                new Ranges(0, double.PositiveInfinity));

            var tempMeasurement = new Measurements(new Temperature(), 298.15, kelvinUnit);
            var pressMeasurement = new Measurements(new Pressure(), 101325, pascalUnit);
            var flowMeasurement = new Measurements(new MolarFlowRate(), 1.0, molPerSecUnit);

            var tempProperty = new PhysicalProperties("Temperature", tempMeasurement);
            var pressProperty = new PhysicalProperties("Pressure", pressMeasurement);
            var flowProperty = new PhysicalProperties("MolarFlow", flowMeasurement);

            var composition = new Composition(new[] { 0.5, 0.5 });

            return new StreamProperties(tempProperty, pressProperty, flowProperty, composition);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            InitializeContext();
            if (_context == null) return;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ConnectionAdapter(null, _context));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullContext_ThrowsArgumentNullException()
        {
            var logger = _mockLogger.Object;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ConnectionAdapter(logger, null));
            Assert.Equal("context", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithValidParameters_Succeeds()
        {
            InitializeContext();
            if (_context == null) return;

            var logger = _mockLogger.Object;

            // Act
            var adapter = new ConnectionAdapter(logger, _context);

            // Assert
            Assert.NotNull(adapter);
        }

        #endregion

        #region ConnectStream Tests

        [Fact]
        public void ConnectStream_WithValidParameters_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();
            var streamResult = _streamAdapter.CreateStream("FEED", properties);
            Assert.True(streamResult.Success);
            var streamId = streamResult.ObjectId;

            var unitResult = _unitOpAdapter.AddThreePhaseSeparator("SEP-101", null);
            Assert.True(unitResult.Success);
            var unitId = unitResult.ObjectId;

            // Act
            var result = _adapter.ConnectStream(streamId, unitId, "Inlet");

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
            Assert.NotNull(result.Connection);
            Assert.Equal(streamId, result.Connection.StreamId);
            Assert.Equal(unitId, result.Connection.UnitId);
            Assert.Equal("Inlet", result.Connection.PortName);
        }

        [Fact]
        public void ConnectStream_WithNullStreamId_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var unitResult = _unitOpAdapter.AddThreePhaseSeparator("SEP-101", null);
            Assert.True(unitResult.Success);
            var unitId = unitResult.ObjectId;

            // Act
            var result = _adapter.ConnectStream(null, unitId, "Inlet");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Stream ID cannot be null or empty", result.Message);
        }

        [Fact]
        public void ConnectStream_WithEmptyStreamId_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var unitResult = _unitOpAdapter.AddThreePhaseSeparator("SEP-101", null);
            Assert.True(unitResult.Success);
            var unitId = unitResult.ObjectId;

            // Act
            var result = _adapter.ConnectStream("", unitId, "Inlet");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Stream ID cannot be null or empty", result.Message);
        }

        [Fact]
        public void ConnectStream_WithNullUnitId_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();
            var streamResult = _streamAdapter.CreateStream("FEED", properties);
            Assert.True(streamResult.Success);
            var streamId = streamResult.ObjectId;

            // Act
            var result = _adapter.ConnectStream(streamId, null, "Inlet");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Unit ID cannot be null or empty", result.Message);
        }

        [Fact]
        public void ConnectStream_WithNullPortName_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();
            var streamResult = _streamAdapter.CreateStream("FEED", properties);
            Assert.True(streamResult.Success);
            var streamId = streamResult.ObjectId;

            var unitResult = _unitOpAdapter.AddThreePhaseSeparator("SEP-101", null);
            Assert.True(unitResult.Success);
            var unitId = unitResult.ObjectId;

            // Act
            var result = _adapter.ConnectStream(streamId, unitId, null);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Port name cannot be null or empty", result.Message);
        }

        [Fact]
        public void ConnectStream_WithNonExistentStream_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var unitResult = _unitOpAdapter.AddThreePhaseSeparator("SEP-101", null);
            Assert.True(unitResult.Success);
            var unitId = unitResult.ObjectId;

            // Act
            var result = _adapter.ConnectStream("NONEXISTENT", unitId, "Inlet");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
        }

        [Fact]
        public void ConnectStream_WithNonExistentUnit_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();
            var streamResult = _streamAdapter.CreateStream("FEED", properties);
            Assert.True(streamResult.Success);
            var streamId = streamResult.ObjectId;

            // Act
            var result = _adapter.ConnectStream(streamId, "NONEXISTENT", "Inlet");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
        }

        [Fact]
        public void ConnectStream_WhenAlreadyConnected_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();
            var streamResult = _streamAdapter.CreateStream("FEED", properties);
            Assert.True(streamResult.Success);
            var streamId = streamResult.ObjectId;

            var unitResult1 = _unitOpAdapter.AddThreePhaseSeparator("SEP-101", null);
            Assert.True(unitResult1.Success);
            var unitId1 = unitResult1.ObjectId;

            var unitResult2 = _unitOpAdapter.AddThreePhaseSeparator("SEP-102", null);
            Assert.True(unitResult2.Success);
            var unitId2 = unitResult2.ObjectId;

            // Connect to first unit
            var connectResult1 = _adapter.ConnectStream(streamId, unitId1, "Inlet");
            Assert.True(connectResult1.Success);

            // Act - Try to connect same stream to second unit
            var result = _adapter.ConnectStream(streamId, unitId2, "Inlet");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("already connected", result.Message);
        }

        #endregion

        #region DisconnectStream Tests

        [Fact]
        public void DisconnectStream_WithValidStreamId_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();
            var streamResult = _streamAdapter.CreateStream("FEED", properties);
            Assert.True(streamResult.Success);
            var streamId = streamResult.ObjectId;

            var unitResult = _unitOpAdapter.AddThreePhaseSeparator("SEP-101", null);
            Assert.True(unitResult.Success);
            var unitId = unitResult.ObjectId;

            var connectResult = _adapter.ConnectStream(streamId, unitId, "Inlet");
            Assert.True(connectResult.Success);

            // Act
            var result = _adapter.DisconnectStream(streamId);

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
        }

        [Fact]
        public void DisconnectStream_WithNullStreamId_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.DisconnectStream(null);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Stream ID cannot be null or empty", result.Message);
        }

        [Fact]
        public void DisconnectStream_WithNonExistentStream_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.DisconnectStream("NONEXISTENT");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
        }

        [Fact]
        public void DisconnectStream_WhenNotConnected_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange - Create stream but don't connect it
            var properties = CreateTestStreamProperties();
            var streamResult = _streamAdapter.CreateStream("FEED", properties);
            Assert.True(streamResult.Success);
            var streamId = streamResult.ObjectId;

            // Act
            var result = _adapter.DisconnectStream(streamId);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not currently connected", result.Message);
        }

        #endregion

        #region GetConnection Tests

        [Fact]
        public void GetConnection_WithValidStreamId_ReturnsConnectionInfo()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange
            var properties = CreateTestStreamProperties();
            var streamResult = _streamAdapter.CreateStream("FEED", properties);
            Assert.True(streamResult.Success);
            var streamId = streamResult.ObjectId;

            var unitResult = _unitOpAdapter.AddThreePhaseSeparator("SEP-101", null);
            Assert.True(unitResult.Success);
            var unitId = unitResult.ObjectId;

            var connectResult = _adapter.ConnectStream(streamId, unitId, "Inlet");
            Assert.True(connectResult.Success);

            // Act
            var result = _adapter.GetConnection(streamId);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Connection);
            Assert.Equal(streamId, result.Connection.StreamId);
            Assert.Equal(unitId, result.Connection.UnitId);
            Assert.Equal("Inlet", result.Connection.PortName);
        }

        [Fact]
        public void GetConnection_WithNonExistentStream_ReturnsFailure()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.GetConnection("NONEXISTENT");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
        }

        #endregion

        #region GetAllConnections Tests

        [Fact]
        public void GetAllConnections_WithNoConnections_ReturnsEmptyList()
        {
            InitializeContext();
            if (_context == null) return;

            // Act
            var result = _adapter.GetAllConnections();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            var connections = result.Data as System.Collections.IList;
            Assert.NotNull(connections);
            Assert.Empty(connections);
        }

        [Fact]
        public void GetAllConnections_WithMultipleConnections_ReturnsAllConnections()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange - Create multiple streams and connections
            var properties = CreateTestStreamProperties();

            var stream1Result = _streamAdapter.CreateStream("FEED", properties);
            Assert.True(stream1Result.Success);
            var stream1Id = stream1Result.ObjectId;

            var stream2Result = _streamAdapter.CreateStream("VAPOR", properties);
            Assert.True(stream2Result.Success);
            var stream2Id = stream2Result.ObjectId;

            var unitResult = _unitOpAdapter.AddThreePhaseSeparator("SEP-101", null);
            Assert.True(unitResult.Success);
            var unitId = unitResult.ObjectId;

            _adapter.ConnectStream(stream1Id, unitId, "Inlet");
            _adapter.ConnectStream(stream2Id, unitId, "VaporOutlet");

            // Act
            var result = _adapter.GetAllConnections();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            var connections = result.Data as System.Collections.IList;
            Assert.NotNull(connections);
            Assert.Equal(2, connections.Count);
        }

        #endregion

        #region ValidateTopology Tests

        [Fact]
        public void ValidateTopology_WithValidTopology_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Arrange - Create a simple valid topology
            var properties = CreateTestStreamProperties();
            var streamResult = _streamAdapter.CreateStream("FEED", properties);
            Assert.True(streamResult.Success);
            var streamId = streamResult.ObjectId;

            var unitResult = _unitOpAdapter.AddThreePhaseSeparator("SEP-101", null);
            Assert.True(unitResult.Success);
            var unitId = unitResult.ObjectId;

            var connectResult = _adapter.ConnectStream(streamId, unitId, "Inlet");
            Assert.True(connectResult.Success);

            // Act
            var result = _adapter.ValidateTopology();

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
        }

        [Fact]
        public void ValidateTopology_WithNoConnections_ReturnsSuccess()
        {
            InitializeContext();
            if (_context == null) return;

            // Act - Validate empty topology
            var result = _adapter.ValidateTopology();

            // Assert
            // Empty topology is valid (no errors)
            Assert.True(result.Success);
        }

        #endregion
    }
}
