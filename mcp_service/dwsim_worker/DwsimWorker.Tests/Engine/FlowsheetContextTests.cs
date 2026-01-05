using System;
using Moq;
using Serilog;
using Xunit;
using DwsimWorker.Engine;
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Engine
{
    /// <summary>
    /// Unit tests for FlowsheetContext class.
    /// Tests focus on initialization, state management, and resource cleanup.
    /// Note: These tests require DWSIM assemblies to be installed (integration test flavor).
    /// </summary>
    public class FlowsheetContextTests : IDisposable
    {
        private readonly Mock<ILogger> _mockLogger;
        private FlowsheetContext _context;

        public FlowsheetContextTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var config = FlowsheetContextConfig.CreateDefault();

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new FlowsheetContext(null, config));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullConfig_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = _mockLogger.Object;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new FlowsheetContext(logger, null));
            Assert.Equal("config", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithValidParameters_Succeeds()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var config = FlowsheetContextConfig.CreateDefault();

            // Act
            _context = new FlowsheetContext(logger, config);

            // Assert
            Assert.NotNull(_context);
            Assert.False(_context.IsInitialized);
        }

        #endregion

        #region Initialize Tests

        [Fact]
        public void Initialize_WithValidConfig_LoadsAssemblies()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;
            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);

            // Act
            _context.Initialize();

            // Assert
            Assert.True(_context.IsInitialized);

            // Verify initialization logging occurred
            _mockLogger.Verify(
                x => x.Information<string>(
                    It.Is<string>(s => s.Contains("Initializing FlowsheetContext")),
                    It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public void Initialize_WithValidConfig_CreatesFlowsheet()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;
            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);

            // Act
            _context.Initialize();

            // Assert
            Assert.True(_context.IsInitialized);
            var flowsheet = _context.GetFlowsheet();
            Assert.NotNull(flowsheet);

            // Verify flowsheet creation was logged
            _mockLogger.Verify(
                x => x.Information(It.Is<string>(s => s.Contains("Flowsheet instance created successfully"))),
                Times.Once);
        }

        [Fact]
        public void Initialize_CalledTwice_ThrowsInvalidOperationException()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => _context.Initialize());
            Assert.Contains("already initialized", ex.Message);
        }

        [Fact]
        public void Initialize_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var config = FlowsheetContextConfig.CreateDefault();
            _context = new FlowsheetContext(logger, config);
            _context.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => _context.Initialize());
        }

        #endregion

        #region GetFlowsheet Tests

        [Fact]
        public void GetFlowsheet_BeforeInitialize_ThrowsInvalidOperationException()
        {
            // Arrange
            var logger = _mockLogger.Object;
            var config = FlowsheetContextConfig.CreateDefault();
            _context = new FlowsheetContext(logger, config);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => _context.GetFlowsheet());
            Assert.Contains("not initialized", ex.Message);
        }

        [Fact]
        public void GetFlowsheet_AfterInitialize_ReturnsFlowsheetInstance()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act
            var flowsheet = _context.GetFlowsheet();

            // Assert
            Assert.NotNull(flowsheet);
            Assert.Equal("DWSIM.SharedClasses.FOSSEEFlowsheet", flowsheet.GetType().FullName);
        }

        [Fact]
        public void GetFlowsheet_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();
            _context.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => _context.GetFlowsheet());
        }

        #endregion

        #region Compound Management Tests

        [Fact]
        public void AddCompound_ValidName_AddsToList()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act
            _context.AddCompound("Water");
            _context.AddCompound("Methane");

            // Assert
            var compounds = _context.GetCompounds();
            Assert.Equal(2, compounds.Count);
            Assert.Contains("Water", compounds);
            Assert.Contains("Methane", compounds);
        }

        [Fact]
        public void AddCompound_DuplicateName_DoesNotAddAgain()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            // Use real console logger for diagnostics instead of mock
            var logger = new Serilog.LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act
            _context.AddCompound("Water");
            _context.AddCompound("Water");

            // Assert
            var compounds = _context.GetCompounds();
            Assert.Single(compounds);
            Assert.Contains("Water", compounds);
        }

        [Fact]
        public void AddCompound_NullName_ThrowsArgumentNullException()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => _context.AddCompound(null));
            Assert.Equal("compoundName", ex.ParamName);
        }

        [Fact]
        public void AddCompound_EmptyName_ThrowsArgumentNullException()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => _context.AddCompound(""));
            Assert.Equal("compoundName", ex.ParamName);
        }

        [Fact]
        public void GetCompounds_AfterAdding_ReturnsAllCompounds()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act
            _context.AddCompound("Water");
            _context.AddCompound("Ethanol");
            _context.AddCompound("Methane");
            var compounds = _context.GetCompounds();

            // Assert
            Assert.Equal(3, compounds.Count);
            Assert.Contains("Water", compounds);
            Assert.Contains("Ethanol", compounds);
            Assert.Contains("Methane", compounds);
        }

        [Fact]
        public void GetCompounds_BeforeAdding_ReturnsEmptyList()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act
            var compounds = _context.GetCompounds();

            // Assert
            Assert.Empty(compounds);
        }

        #endregion

        #region Stream Management Tests

        [Fact]
        public void AddStream_ValidStream_AddsToRegistry()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            var mockStream = new object(); // Placeholder for MaterialStream

            // Act
            _context.AddStream(mockStream, "STREAM-1");

            // Assert
            var streamIds = _context.GetStreamIds();
            Assert.Single(streamIds);
            Assert.Contains("STREAM-1", streamIds);
        }

        [Fact]
        public void AddStream_DuplicateId_ThrowsInvalidOperationException()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            var mockStream1 = new object();
            var mockStream2 = new object();

            // Act
            _context.AddStream(mockStream1, "STREAM-1");

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => _context.AddStream(mockStream2, "STREAM-1"));
            Assert.Contains("already exists", ex.Message);
        }

        [Fact]
        public void AddStream_NullStream_ThrowsArgumentNullException()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => _context.AddStream(null, "STREAM-1"));
            Assert.Equal("stream", ex.ParamName);
        }

        [Fact]
        public void AddStream_NullStreamId_ThrowsArgumentNullException()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            var mockStream = new object();

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => _context.AddStream(mockStream, null));
            Assert.Equal("streamId", ex.ParamName);
        }

        [Fact]
        public void GetStream_ValidId_ReturnsStream()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            var mockStream = new object();
            _context.AddStream(mockStream, "STREAM-1");

            // Act
            var retrievedStream = _context.GetStream("STREAM-1");

            // Assert
            Assert.NotNull(retrievedStream);
            Assert.Same(mockStream, retrievedStream);
        }

        [Fact]
        public void GetStream_InvalidId_ReturnsNull()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act
            var stream = _context.GetStream("NON-EXISTENT-STREAM");

            // Assert
            Assert.Null(stream);
        }

        [Fact]
        public void GetStream_NullId_ThrowsArgumentNullException()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => _context.GetStream(null));
            Assert.Equal("streamId", ex.ParamName);
        }

        [Fact]
        public void GetStreamIds_AfterAddingStreams_ReturnsAllIds()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            _context.AddStream(new object(), "STREAM-1");
            _context.AddStream(new object(), "STREAM-2");
            _context.AddStream(new object(), "STREAM-3");

            // Act
            var streamIds = _context.GetStreamIds();

            // Assert
            Assert.Equal(3, streamIds.Count);
            Assert.Contains("STREAM-1", streamIds);
            Assert.Contains("STREAM-2", streamIds);
            Assert.Contains("STREAM-3", streamIds);
        }

        #endregion

        #region Unit Operation Management Tests

        [Fact]
        public void AddUnit_ValidUnit_AddsToRegistry()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            var mockUnit = new object(); // Placeholder for UnitOperation

            // Act
            _context.AddUnit(mockUnit, "SEP-100");

            // Assert
            var unitIds = _context.GetUnitIds();
            Assert.Single(unitIds);
            Assert.Contains("SEP-100", unitIds);
        }

        [Fact]
        public void AddUnit_DuplicateId_ThrowsInvalidOperationException()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            var mockUnit1 = new object();
            var mockUnit2 = new object();

            // Act
            _context.AddUnit(mockUnit1, "SEP-100");

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => _context.AddUnit(mockUnit2, "SEP-100"));
            Assert.Contains("already exists", ex.Message);
        }

        [Fact]
        public void GetUnit_ValidId_ReturnsUnit()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            var mockUnit = new object();
            _context.AddUnit(mockUnit, "SEP-100");

            // Act
            var retrievedUnit = _context.GetUnit("SEP-100");

            // Assert
            Assert.NotNull(retrievedUnit);
            Assert.Same(mockUnit, retrievedUnit);
        }

        [Fact]
        public void GetUnit_InvalidId_ReturnsNull()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act
            var unit = _context.GetUnit("NON-EXISTENT-UNIT");

            // Assert
            Assert.Null(unit);
        }

        #endregion

        #region Connection Management Tests

        [Fact]
        public void AddConnection_ValidConnection_AddsToRegistry()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            var connection = new ConnectionInfo("STREAM-1", "SEP-100", "Inlet1");

            // Act
            _context.AddConnection(connection);

            // Assert
            var connections = _context.GetConnections();
            Assert.Single(connections);
            Assert.Equal("STREAM-1", connections[0].StreamId);
            Assert.Equal("SEP-100", connections[0].UnitId);
            Assert.Equal("Inlet1", connections[0].PortName);
        }

        [Fact]
        public void AddConnection_DuplicateStreamId_ReplacesConnection()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            var connection1 = new ConnectionInfo("STREAM-1", "SEP-100", "Inlet1");
            var connection2 = new ConnectionInfo("STREAM-1", "SEP-200", "Inlet2");

            // Act
            _context.AddConnection(connection1);
            _context.AddConnection(connection2);

            // Assert
            var connections = _context.GetConnections();
            Assert.Single(connections);
            Assert.Equal("SEP-200", connections[0].UnitId);
            Assert.Equal("Inlet2", connections[0].PortName);
        }

        [Fact]
        public void GetConnectionForStream_ValidStreamId_ReturnsConnection()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            var connection = new ConnectionInfo("STREAM-1", "SEP-100", "Inlet1");
            _context.AddConnection(connection);

            // Act
            var retrievedConnection = _context.GetConnectionForStream("STREAM-1");

            // Assert
            Assert.NotNull(retrievedConnection);
            Assert.Equal("STREAM-1", retrievedConnection.StreamId);
            Assert.Equal("SEP-100", retrievedConnection.UnitId);
        }

        [Fact]
        public void GetConnectionForStream_InvalidStreamId_ReturnsNull()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act
            var connection = _context.GetConnectionForStream("NON-EXISTENT-STREAM");

            // Assert
            Assert.Null(connection);
        }

        [Fact]
        public void RemoveConnection_ValidStreamId_RemovesConnection()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            var connection = new ConnectionInfo("STREAM-1", "SEP-100", "Inlet1");
            _context.AddConnection(connection);

            // Act
            var removed = _context.RemoveConnection("STREAM-1");

            // Assert
            Assert.True(removed);
            var connections = _context.GetConnections();
            Assert.Empty(connections);
        }

        [Fact]
        public void RemoveConnection_InvalidStreamId_ReturnsFalse()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act
            var removed = _context.RemoveConnection("NON-EXISTENT-STREAM");

            // Assert
            Assert.False(removed);
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_CleansUpResources()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            _context.AddCompound("Water");
            _context.AddStream(new object(), "STREAM-1");
            _context.AddUnit(new object(), "SEP-100");
            _context.AddConnection(new ConnectionInfo("STREAM-1", "SEP-100", "Inlet1"));

            // Act
            _context.Dispose();

            // Assert - After disposal, operations should throw ObjectDisposedException
            Assert.Throws<ObjectDisposedException>(() => _context.GetFlowsheet());
            Assert.Throws<ObjectDisposedException>(() => _context.AddCompound("Methane"));
            Assert.Throws<ObjectDisposedException>(() => _context.GetCompounds());

            // Verify disposal logging occurred
            _mockLogger.Verify(
                x => x.Information<string>(
                    It.Is<string>(s => s.Contains("Disposing FlowsheetContext")),
                    It.IsAny<string>()),
                Times.Once);

            _mockLogger.Verify(
                x => x.Information(It.Is<string>(s => s.Contains("disposed successfully"))),
                Times.Once);
        }

        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            // Arrange
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip test if DWSIM not found
                return;
            }

            var logger = _mockLogger.Object;

            var assemblyConfig = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(assemblyConfig)
                .WithValidation(false)
                .Build();

            _context = new FlowsheetContext(logger, config);
            _context.Initialize();

            // Act - Dispose twice
            _context.Dispose();

            // Assert - Second dispose should not throw
            _context.Dispose();

            // Verify disposal was logged only once (second call returns early)
            _mockLogger.Verify(
                x => x.Information<string>(
                    It.Is<string>(s => s.Contains("Disposing FlowsheetContext")),
                    It.IsAny<string>()),
                Times.Once);
        }

        #endregion
    }
}
