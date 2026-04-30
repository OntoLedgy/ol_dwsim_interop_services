// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using Serilog.Core;
using Xunit;
using DwsimWorker.Engine;
using DwsimWorker.Adapters;
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Integration
{
    /// <summary>
    /// Golden integration test for complete three-phase separator flowsheet workflow.
    /// This test validates the end-to-end process:
    /// Context → Compounds → Property Package → Streams → Separator → Connections → Validation
    ///
    /// IMPORTANT: These tests require DWSIM assemblies to be available.
    /// Tests will be skipped if DWSIM assemblies are not found at the configured path.
    /// </summary>
    [Trait("Category", "Integration")]
    public class ThreePhaseSeparatorIntegrationTests : IDisposable
    {
        private readonly Logger _logger;
        private FlowsheetContext _context;
        private CompoundAdapter _compoundAdapter;
        private PropertyPackageAdapter _propertyPackageAdapter;
        private StreamAdapter _streamAdapter;
        private UnitOpAdapter _unitOpAdapter;
        private ConnectionAdapter _connectionAdapter;

        public ThreePhaseSeparatorIntegrationTests()
        {
            // Configure Serilog for integration tests
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
        }

        public void Dispose()
        {
            _context?.Dispose();
            _logger?.Dispose();
        }

        private void InitializeFlowsheet()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                // Skip if DWSIM not found
                return;
            }

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)
                .WithFlowsheetName("Three-Phase Separator Test Flowsheet")
                .Build();

            _context = new FlowsheetContext(_logger, config);
            _context.Initialize();

            // Initialize all adapters
            _compoundAdapter = new CompoundAdapter(_logger, _context);
            _propertyPackageAdapter = new PropertyPackageAdapter(_logger, _context);
            _streamAdapter = new StreamAdapter(_logger, _context);
            _unitOpAdapter = new UnitOpAdapter(_logger, _context);
            _connectionAdapter = new ConnectionAdapter(_logger, _context);
        }

        private StreamProperties CreateStreamProperties(
            double temperature,
            double pressure,
            double molarFlow,
            double[] composition)
        {
            var kelvinUnit = new UnitsOfMeasure("K", typeof(Temperature),
                new Ranges(0, double.PositiveInfinity));
            var pascalUnit = new UnitsOfMeasure("Pa", typeof(Pressure),
                new Ranges(0, double.PositiveInfinity));
            var molPerSecUnit = new UnitsOfMeasure("mol/s", typeof(MolarFlowRate),
                new Ranges(0, double.PositiveInfinity));

            var tempMeasurement = new Measurements(new Temperature(), temperature, kelvinUnit);
            var pressMeasurement = new Measurements(new Pressure(), pressure, pascalUnit);
            var flowMeasurement = new Measurements(new MolarFlowRate(), molarFlow, molPerSecUnit);

            var tempProperty = new PhysicalProperties("Temperature", tempMeasurement);
            var pressProperty = new PhysicalProperties("Pressure", pressMeasurement);
            var flowProperty = new PhysicalProperties("MolarFlow", flowMeasurement);

            var comp = new Composition(composition);

            return new StreamProperties(tempProperty, pressProperty, flowProperty, comp);
        }

        #region Golden Integration Test

        [Fact]
        public void GoldenTest_CompleteThreePhaseSeparatorWorkflow_Succeeds()
        {
            // ==========================================
            // GOLDEN INTEGRATION TEST
            // ==========================================
            // This test validates the complete workflow for setting up
            // a three-phase separator flowsheet from scratch.
            //
            // Workflow Steps:
            // 1. Initialize flowsheet context
            // 2. Add compounds (Water, Oil, Gas)
            // 3. Configure property package (Peng-Robinson)
            // 4. Create feed stream with mixed composition
            // 5. Create three-phase separator
            // 6. Create outlet streams (vapor, light liquid, heavy liquid)
            // 7. Connect all streams to separator
            // 8. Validate topology
            // ==========================================

            // Arrange
            InitializeFlowsheet();
            if (_context == null) return; // Skip if DWSIM not available

            _logger.Information("=== Starting Golden Integration Test ===");

            // Step 1: Add Compounds
            _logger.Information("Step 1: Adding compounds...");
            var compoundResult1 = _compoundAdapter.AddCompound("Water");
            var compoundResult2 = _compoundAdapter.AddCompound("Propane");  // Light liquid (oil-like)
            var compoundResult3 = _compoundAdapter.AddCompound("Methane");  // Vapor (gas)

            Assert.True(compoundResult1.Success, $"Water compound failed: {compoundResult1.Message}");
            Assert.True(compoundResult2.Success, $"Propane compound failed: {compoundResult2.Message}");
            Assert.True(compoundResult3.Success, $"Methane compound failed: {compoundResult3.Message}");

            var compounds = _compoundAdapter.GetCompounds();
            Assert.True(compounds.Success);
            Assert.Equal(3, compounds.ValidatedTypes.Count);

            _logger.Information("✓ Compounds added: Water (index 0), Propane (index 1), Methane (index 2)");

            // Step 2: Set Property Package
            _logger.Information("Step 2: Configuring property package...");
            var packageResult = _propertyPackageAdapter.SetPropertyPackage("Peng-Robinson");
            Assert.True(packageResult.Success, $"Property package failed: {packageResult.Message}");

            var packageCheck = _propertyPackageAdapter.GetPropertyPackageName();
            Assert.True(packageCheck.Success);
            Assert.Contains("Peng-Robinson", packageCheck.ValidatedTypes);

            _logger.Information("✓ Property package configured: Peng-Robinson");

            // Step 3: Create Feed Stream
            _logger.Information("Step 3: Creating feed stream...");
            // Mixed feed: 30% Water, 40% Propane (light liquid), 30% Methane (vapor)
            var feedProperties = CreateStreamProperties(
                temperature: 298.15,  // 25°C
                pressure: 500000,     // 5 bar
                molarFlow: 100.0,     // 100 mol/s
                composition: new[] { 0.3, 0.4, 0.3 }
            );

            var feedResult = _streamAdapter.CreateStream("FEED", feedProperties);
            Assert.True(feedResult.Success, $"Feed stream creation failed: {feedResult.ObjectId}");
            var feedStreamId = feedResult.ObjectId;

            _logger.Information($"✓ Feed stream created: {feedStreamId} (30% Water, 40% Propane, 30% Methane)");

            // Step 4: Create Three-Phase Separator
            _logger.Information("Step 4: Creating three-phase separator...");
            var separatorConfig = new UnitOpConfig(new Dictionary<string, object>
            {
                { "PressureDrop", 10000.0 }  // 0.1 bar pressure drop
            });

            var separatorResult = _unitOpAdapter.AddThreePhaseSeparator("SEP-101", separatorConfig);
            Assert.True(separatorResult.Success, $"Separator creation failed: {separatorResult.ObjectId}");
            var separatorId = separatorResult.ObjectId;

            _logger.Information($"✓ Three-phase separator created: {separatorId}");

            // Verify separator parameters
            var paramResult = _unitOpAdapter.GetParameter(separatorId, "PressureDrop");
            Assert.True(paramResult.Success);
            Assert.Equal(10000.0, Convert.ToDouble(paramResult.Data), 0.1);

            // Step 5: Create Outlet Streams
            _logger.Information("Step 5: Creating outlet streams...");

            // Vapor outlet (mostly methane)
            var vaporProperties = CreateStreamProperties(
                temperature: 298.15,
                pressure: 490000,  // Accounting for pressure drop
                molarFlow: 30.0,   // Expected ~30% of feed
                composition: new[] { 0.0, 0.0, 1.0 }  // Pure methane
            );
            var vaporResult = _streamAdapter.CreateStream("VAPOR", vaporProperties);
            Assert.True(vaporResult.Success);
            var vaporStreamId = vaporResult.ObjectId;

            // Light liquid outlet (mostly propane)
            var lightLiquidProperties = CreateStreamProperties(
                temperature: 298.15,
                pressure: 490000,
                molarFlow: 40.0,
                composition: new[] { 0.0, 1.0, 0.0 }  // Pure propane
            );
            var lightLiquidResult = _streamAdapter.CreateStream("LIGHT_LIQUID", lightLiquidProperties);
            Assert.True(lightLiquidResult.Success);
            var lightLiquidStreamId = lightLiquidResult.ObjectId;

            // Heavy liquid outlet (mostly water)
            var heavyLiquidProperties = CreateStreamProperties(
                temperature: 298.15,
                pressure: 490000,
                molarFlow: 30.0,
                composition: new[] { 1.0, 0.0, 0.0 }  // Pure water
            );
            var heavyLiquidResult = _streamAdapter.CreateStream("HEAVY_LIQUID", heavyLiquidProperties);
            Assert.True(heavyLiquidResult.Success);
            var heavyLiquidStreamId = heavyLiquidResult.ObjectId;

            _logger.Information($"✓ Outlet streams created: {vaporStreamId}, {lightLiquidStreamId}, {heavyLiquidStreamId}");

            // Step 6: Get Separator Ports
            _logger.Information("Step 6: Querying separator ports...");
            var portsResult = _unitOpAdapter.GetPorts(separatorId);
            Assert.True(portsResult.Success, $"GetPorts failed: {portsResult.Message}");
            Assert.NotNull(portsResult.ValidatedTypes);
            Assert.Equal(4, portsResult.ValidatedTypes.Count);  // Inlet + 3 outlets

            _logger.Information($"✓ Separator ports: {string.Join(", ", portsResult.ValidatedTypes)}");

            // Step 7: Connect Streams to Separator
            _logger.Information("Step 7: Connecting streams to separator...");

            // Connect feed to inlet
            var feedConnection = _connectionAdapter.ConnectStream(feedStreamId, separatorId, "Inlet");
            Assert.True(feedConnection.Success, $"Feed connection failed: {feedConnection.Message}");
            Assert.NotNull(feedConnection.Connection);
            Assert.Equal(feedStreamId, feedConnection.Connection.StreamId);
            Assert.Equal(separatorId, feedConnection.Connection.UnitId);
            Assert.Equal("Inlet", feedConnection.Connection.PortName);

            // Connect vapor to vapor outlet
            var vaporConnection = _connectionAdapter.ConnectStream(vaporStreamId, separatorId, "VaporOutlet");
            Assert.True(vaporConnection.Success, $"Vapor connection failed: {vaporConnection.Message}");

            // Connect light liquid to liquid outlet 1
            var lightLiquidConnection = _connectionAdapter.ConnectStream(lightLiquidStreamId, separatorId, "LiquidOutlet1");
            Assert.True(lightLiquidConnection.Success, $"Light liquid connection failed: {lightLiquidConnection.Message}");

            // Connect heavy liquid to liquid outlet 2
            var heavyLiquidConnection = _connectionAdapter.ConnectStream(heavyLiquidStreamId, separatorId, "LiquidOutlet2");
            Assert.True(heavyLiquidConnection.Success, $"Heavy liquid connection failed: {heavyLiquidConnection.Message}");

            _logger.Information("✓ All streams connected to separator");

            // Step 8: Verify All Connections
            _logger.Information("Step 8: Verifying connections...");
            var allConnections = _connectionAdapter.GetAllConnections();
            Assert.True(allConnections.Success);
            var connectionsList = allConnections.Data as System.Collections.IList;
            Assert.NotNull(connectionsList);
            Assert.Equal(4, connectionsList.Count);  // 1 inlet + 3 outlets

            _logger.Information($"✓ Total connections verified: {connectionsList.Count}");

            // Verify individual connections
            var feedConnectionCheck = _connectionAdapter.GetConnection(feedStreamId);
            Assert.True(feedConnectionCheck.Success);
            Assert.Equal("Inlet", feedConnectionCheck.Connection.PortName);

            var vaporConnectionCheck = _connectionAdapter.GetConnection(vaporStreamId);
            Assert.True(vaporConnectionCheck.Success);
            Assert.Equal("VaporOutlet", vaporConnectionCheck.Connection.PortName);

            // Step 9: Validate Topology
            _logger.Information("Step 9: Validating flowsheet topology...");
            var topologyResult = _connectionAdapter.ValidateTopology();
            Assert.True(topologyResult.Success, $"Topology validation failed: {topologyResult.Message}");

            _logger.Information("✓ Flowsheet topology validated successfully");

            // Final Verification
            _logger.Information("=== Golden Integration Test PASSED ===");
            _logger.Information("Flowsheet Summary:");
            _logger.Information($"  - Compounds: 3 (Water, Propane, Methane)");
            _logger.Information($"  - Property Package: Peng-Robinson");
            _logger.Information($"  - Streams: 4 (1 feed + 3 outlets)");
            _logger.Information($"  - Unit Operations: 1 (Three-phase separator)");
            _logger.Information($"  - Connections: 4 (Fully connected)");
            _logger.Information($"  - Topology: Valid");
        }

        #endregion

        #region Error Scenario Tests

        [Fact]
        public void Integration_InvalidConnectionOrder_FailsGracefully()
        {
            // Test that connecting a stream before creating the unit operation fails gracefully

            InitializeFlowsheet();
            if (_context == null) return;

            // Add compounds and create stream
            _compoundAdapter.AddCompound("Water");
            _compoundAdapter.AddCompound("Ethanol");

            var properties = CreateStreamProperties(298.15, 101325, 1.0, new[] { 0.5, 0.5 });
            var streamResult = _streamAdapter.CreateStream("FEED", properties);
            Assert.True(streamResult.Success);
            var streamId = streamResult.ObjectId;

            // Try to connect to non-existent unit
            var connectionResult = _connectionAdapter.ConnectStream(streamId, "NONEXISTENT", "Inlet");

            // Should fail gracefully
            Assert.False(connectionResult.Success);
            Assert.Contains("not found", connectionResult.Message);
        }

        [Fact]
        public void Integration_DuplicateConnection_FailsGracefully()
        {
            // Test that connecting the same stream twice fails gracefully

            InitializeFlowsheet();
            if (_context == null) return;

            // Setup
            _compoundAdapter.AddCompound("Water");
            _compoundAdapter.AddCompound("Ethanol");
            _propertyPackageAdapter.SetPropertyPackage("NRTL");

            var properties = CreateStreamProperties(298.15, 101325, 1.0, new[] { 0.5, 0.5 });
            var streamResult = _streamAdapter.CreateStream("FEED", properties);
            var streamId = streamResult.ObjectId;

            var separatorResult = _unitOpAdapter.AddThreePhaseSeparator("SEP-101", null);
            var separatorId = separatorResult.ObjectId;

            // First connection succeeds
            var connection1 = _connectionAdapter.ConnectStream(streamId, separatorId, "Inlet");
            Assert.True(connection1.Success);

            // Second connection to same stream fails
            var connection2 = _connectionAdapter.ConnectStream(streamId, separatorId, "VaporOutlet");
            Assert.False(connection2.Success);
            Assert.Contains("already connected", connection2.Message);
        }

        #endregion
    }
}
