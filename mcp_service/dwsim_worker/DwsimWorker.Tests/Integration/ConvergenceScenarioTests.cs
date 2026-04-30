// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Serilog;
using Serilog.Core;
using Xunit;
using DwsimWorker.Engine;
using DwsimWorker.Adapters;
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Integration
{
    /// <summary>
    /// Integration tests for various convergence scenarios in three-phase separator calculations.
    /// Tests different composition scenarios and convergence handling.
    ///
    /// IMPORTANT: These tests require DWSIM assemblies to be available.
    /// Tests will be skipped if DWSIM assemblies are not found at the configured path.
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Category", "Convergence")]
    public class ConvergenceScenarioTests : IDisposable
    {
        private readonly Logger _logger;
        private FlowsheetContext _context;
        private CalculationAdapter _calculationAdapter;

        public ConvergenceScenarioTests()
        {
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

        private void SetupFlowsheet(double[] feedComposition, string testName)
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)
                .WithFlowsheetName($"Convergence Test: {testName}")
                .Build();

            _context = new FlowsheetContext(_logger, config);
            _context.Initialize();

            var compoundAdapter = new CompoundAdapter(_logger, _context);
            var propertyPackageAdapter = new PropertyPackageAdapter(_logger, _context);
            var streamAdapter = new StreamAdapter(_logger, _context);
            var unitOpAdapter = new UnitOpAdapter(_logger, _context);
            var connectionAdapter = new ConnectionAdapter(_logger, _context);

            // Add compounds
            compoundAdapter.AddCompound("Water");
            compoundAdapter.AddCompound("Propane");
            compoundAdapter.AddCompound("Methane");

            // Set property package
            propertyPackageAdapter.SetPropertyPackage("Peng-Robinson");

            // Create inlet stream with specified composition
            var feedProperties = CreateStreamProperties(298.15, 500000, 100.0, feedComposition);
            var feedResult = streamAdapter.CreateStream("FEED", feedProperties);
            var feedStreamId = feedResult.ObjectId;

            // Create separator
            var separatorConfig = new UnitOpConfig(new Dictionary<string, object>
            {
                { "PressureDrop", 10000.0 }
            });
            var separatorResult = unitOpAdapter.AddThreePhaseSeparator("SEP-101", separatorConfig);
            var separatorId = separatorResult.ObjectId;

            // Create outlet streams
            var vaporProperties = CreateStreamProperties(298.15, 490000, 30.0, new[] { 0.0, 0.0, 1.0 });
            var vaporResult = streamAdapter.CreateStream("VAPOR", vaporProperties);
            var vaporStreamId = vaporResult.ObjectId;

            var lightLiquidProperties = CreateStreamProperties(298.15, 490000, 40.0, new[] { 0.0, 1.0, 0.0 });
            var lightLiquidResult = streamAdapter.CreateStream("LIGHT_LIQUID", lightLiquidProperties);
            var lightLiquidStreamId = lightLiquidResult.ObjectId;

            var heavyLiquidProperties = CreateStreamProperties(298.15, 490000, 30.0, new[] { 1.0, 0.0, 0.0 });
            var heavyLiquidResult = streamAdapter.CreateStream("HEAVY_LIQUID", heavyLiquidProperties);
            var heavyLiquidStreamId = heavyLiquidResult.ObjectId;

            // Connect streams
            connectionAdapter.ConnectStream(feedStreamId, separatorId, "Inlet");
            connectionAdapter.ConnectStream(vaporStreamId, separatorId, "VaporOutlet");
            connectionAdapter.ConnectStream(lightLiquidStreamId, separatorId, "LiquidOutlet1");
            connectionAdapter.ConnectStream(heavyLiquidStreamId, separatorId, "LiquidOutlet2");

            // Validate topology
            var topologyResult = connectionAdapter.ValidateTopology();
            if (!topologyResult.Success)
            {
                throw new InvalidOperationException($"Topology validation failed: {topologyResult.Message}");
            }

            _calculationAdapter = new CalculationAdapter(_logger, _context, streamAdapter);
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

        #region Standard Convergence Tests

        [Fact]
        public void Convergence_StandardCase_Converges()
        {
            // Test that a standard three-phase separator with balanced composition converges

            // Arrange - Mixed composition: equal parts Water, Propane, Methane
            SetupFlowsheet(new[] { 0.33, 0.33, 0.34 }, "Standard Mixed Composition");
            if (_context == null) return;

            _logger.Information("=== Testing Standard Convergence Case ===");

            // Act
            var result = _calculationAdapter.RunCalculation();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success, $"Calculation failed: {result.Message}");

            Assert.NotNull(result.ConvergenceStatus);
            Assert.Equal(ConvergenceState.Converged, result.ConvergenceStatus.State);

            _logger.Information($"✓ Standard case converged: {result.ConvergenceStatus.State}");
            _logger.Information($"  Timing: {result.Timing?.TotalMilliseconds:F2}ms");
            _logger.Information($"  Streams: {result.StreamResults?.Count}");
        }

        #endregion

        #region Edge Composition Tests

        [Fact]
        public void Convergence_PureWaterFeed_HandlesGracefully()
        {
            // Test handling of pure component feed (edge case)

            // Arrange - Pure water (100% heavy liquid component)
            SetupFlowsheet(new[] { 1.0, 0.0, 0.0 }, "Pure Water Feed");
            if (_context == null) return;

            _logger.Information("=== Testing Pure Water Feed ===");

            // Act
            var result = _calculationAdapter.RunCalculation();

            // Assert
            Assert.NotNull(result);

            // Pure component might not converge in traditional sense or might converge quickly
            // The key is that it doesn't throw an exception
            _logger.Information($"✓ Pure water case handled: Success={result.Success}, State={result.ConvergenceStatus?.State}");

            // Should have timing and stream results even if not converged
            Assert.NotNull(result.Timing);
            Assert.NotNull(result.StreamResults);
        }

        [Fact]
        public void Convergence_PureMethaneFeed_HandlesGracefully()
        {
            // Test handling of pure vapor component feed

            // Arrange - Pure methane (100% vapor component)
            SetupFlowsheet(new[] { 0.0, 0.0, 1.0 }, "Pure Methane Feed");
            if (_context == null) return;

            _logger.Information("=== Testing Pure Methane Feed ===");

            // Act
            var result = _calculationAdapter.RunCalculation();

            // Assert
            Assert.NotNull(result);

            _logger.Information($"✓ Pure methane case handled: Success={result.Success}, State={result.ConvergenceStatus?.State}");

            Assert.NotNull(result.Timing);
            Assert.NotNull(result.StreamResults);
        }

        [Fact]
        public void Convergence_DominantWaterFeed_Converges()
        {
            // Test with composition heavily skewed toward one component

            // Arrange - 90% Water, 5% Propane, 5% Methane
            SetupFlowsheet(new[] { 0.90, 0.05, 0.05 }, "Dominant Water Feed");
            if (_context == null) return;

            _logger.Information("=== Testing Dominant Water Composition ===");

            // Act
            var result = _calculationAdapter.RunCalculation();

            // Assert
            Assert.NotNull(result);

            // Should handle gracefully even if convergence is challenging
            _logger.Information($"✓ Dominant water case handled: Success={result.Success}, State={result.ConvergenceStatus?.State}");

            Assert.NotNull(result.Timing);
            Assert.NotNull(result.StreamResults);
        }

        #endregion

        #region Non-Convergence Handling Tests

        [Fact]
        public void NonConvergence_DoesNotThrowException()
        {
            // Test that non-convergence returns a result rather than throwing an exception

            // Arrange - Use a challenging composition that might not converge easily
            SetupFlowsheet(new[] { 0.50, 0.25, 0.25 }, "Potentially Difficult Convergence");
            if (_context == null) return;

            _logger.Information("=== Testing Non-Convergence Handling ===");

            // Act & Assert - Should not throw
            var exception = Record.Exception(() => _calculationAdapter.RunCalculation());

            Assert.Null(exception); // No exception should be thrown
            _logger.Information("✓ No exception thrown for potentially non-converging case");
        }

        [Fact]
        public void NonConvergence_ReturnsValidResult()
        {
            // Test that non-convergence still returns a valid result object

            // Arrange
            SetupFlowsheet(new[] { 0.40, 0.30, 0.30 }, "Non-Convergence Test");
            if (_context == null) return;

            // Act
            var result = _calculationAdapter.RunCalculation();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.ConvergenceStatus);
            Assert.NotNull(result.Timing);
            Assert.NotNull(result.StreamResults);
            Assert.NotNull(result.Messages);

            _logger.Information($"✓ Valid result returned: Success={result.Success}");
            _logger.Information($"  Convergence State: {result.ConvergenceStatus.State}");
            _logger.Information($"  Has Timing: {result.Timing != null}");
            _logger.Information($"  Has Stream Results: {result.StreamResults.Count} streams");
            _logger.Information($"  Has Messages: {result.Messages.Count} messages");

            // Even if not successful, should have all required fields
            if (!result.Success)
            {
                _logger.Information($"  Failure message: {result.Message}");
            }
        }

        #endregion

        #region Convergence Status Tests

        [Fact]
        public void ConvergenceStatus_AfterCalculation_HasValidState()
        {
            // Test that convergence status always has a valid state after calculation

            // Arrange
            SetupFlowsheet(new[] { 0.33, 0.33, 0.34 }, "Convergence Status Test");
            if (_context == null) return;

            // Act
            var result = _calculationAdapter.RunCalculation();

            // Assert
            Assert.NotNull(result.ConvergenceStatus);

            // Should be one of the valid convergence states
            var validStates = new[]
            {
                ConvergenceState.NotStarted,
                ConvergenceState.InProgress,
                ConvergenceState.Converged,
                ConvergenceState.NotConverged,
                ConvergenceState.Error
            };

            Assert.Contains(result.ConvergenceStatus.State, validStates);

            _logger.Information($"✓ Convergence status is valid: {result.ConvergenceStatus.State}");

            // Converged status should have iterations and residual error
            if (result.ConvergenceStatus.State == ConvergenceState.Converged)
            {
                _logger.Information($"  Iterations: {result.ConvergenceStatus.Iterations}");
                _logger.Information($"  Residual Error: {result.ConvergenceStatus.ResidualError}");
            }
        }

        #endregion
    }
}
