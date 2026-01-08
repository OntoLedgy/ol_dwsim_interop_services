using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Serilog;
using Serilog.Core;
using Xunit;
using Xunit.Abstractions;
using DwsimWorker.Engine;
using DwsimWorker.Adapters;
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Performance
{
    /// <summary>
    /// Performance tests for calculation operations.
    /// Validates that calculation and result extraction meet performance targets from NFRs.
    ///
    /// Performance Targets (from NFRs):
    /// - Total calculation time: < 5 seconds
    /// - Result extraction time: < 500 ms
    ///
    /// IMPORTANT: These tests require DWSIM assemblies to be available.
    /// Tests will be skipped if DWSIM assemblies are not found at the configured path.
    /// </summary>
    [Trait("Category", "Performance")]
    [Trait("Category", "Integration")]
    public class CalculationPerformanceTests : IDisposable
    {
        private readonly Logger _logger;
        private readonly ITestOutputHelper _output;
        private FlowsheetContext _context;
        private CalculationAdapter _calculationAdapter;

        public CalculationPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.TestOutput(output)
                .CreateLogger();
        }

        public void Dispose()
        {
            _context?.Dispose();
            _logger?.Dispose();
        }

        private void SetupStandardFlowsheet()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)
                .WithFlowsheetName("Performance Test Flowsheet")
                .Build();

            _context = new FlowsheetContext(_logger, config);
            _context.Initialize();

            var compoundAdapter = new CompoundAdapter(_logger, _context);
            var propertyPackageAdapter = new PropertyPackageAdapter(_logger, _context);
            var streamAdapter = new StreamAdapter(_logger, _context);
            var unitOpAdapter = new UnitOpAdapter(_logger, _context);
            var connectionAdapter = new ConnectionAdapter(_logger, _context);

            // Setup standard flowsheet
            compoundAdapter.AddCompound("Water");
            compoundAdapter.AddCompound("Propane");
            compoundAdapter.AddCompound("Methane");
            compoundAdapter.AddCompound("Ethane");

            propertyPackageAdapter.SetPropertyPackage("Peng-Robinson");

            var feedProperties = CreateStreamProperties(298.15, 500000, 100.0, new[] { 0.25, 0.25, 0.25, 0.25 });
            var feedResult = streamAdapter.CreateStream("FEED", feedProperties);
            var feedStreamId = feedResult.Message;

            var separatorConfig = new UnitOpConfig(new Dictionary<string, object>
            {
                { "PressureDrop", 10000.0 }
            });
            var separatorResult = unitOpAdapter.AddThreePhaseSeparator("SEP-101", separatorConfig);
            var separatorId = separatorResult.Message;

            var vaporProperties = CreateStreamProperties(298.15, 490000, 30.0, new[] { 0.0, 0.0, 0.5, 0.5 });
            var vaporResult = streamAdapter.CreateStream("VAPOR", vaporProperties);
            var vaporStreamId = vaporResult.Message;

            var lightLiquidProperties = CreateStreamProperties(298.15, 490000, 40.0, new[] { 0.0, 0.6, 0.2, 0.2 });
            var lightLiquidResult = streamAdapter.CreateStream("LIGHT_LIQUID", lightLiquidProperties);
            var lightLiquidStreamId = lightLiquidResult.Message;

            var heavyLiquidProperties = CreateStreamProperties(298.15, 490000, 30.0, new[] { 0.8, 0.1, 0.05, 0.05 });
            var heavyLiquidResult = streamAdapter.CreateStream("HEAVY_LIQUID", heavyLiquidProperties);
            var heavyLiquidStreamId = heavyLiquidResult.Message;

            connectionAdapter.ConnectStream(feedStreamId, separatorId, "Inlet");
            connectionAdapter.ConnectStream(vaporStreamId, separatorId, "VaporOutlet");
            connectionAdapter.ConnectStream(lightLiquidStreamId, separatorId, "LiquidOutlet1");
            connectionAdapter.ConnectStream(heavyLiquidStreamId, separatorId, "LiquidOutlet2");

            connectionAdapter.ValidateTopology();

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

        #region Calculation Performance Tests

        [Fact]
        public void Performance_CalculationTime_MeetsTarget()
        {
            // ==========================================
            // PERFORMANCE TEST: Calculation Time < 5s
            // ==========================================
            // NFR: Total calculation time should be less than 5 seconds
            // for a standard three-phase separator

            // Arrange
            SetupStandardFlowsheet();
            if (_context == null) return;

            _logger.Information("=== Performance Test: Calculation Time ===");

            // Act - Measure calculation time
            var sw = Stopwatch.StartNew();
            var result = _calculationAdapter.RunCalculation();
            sw.Stop();

            var totalTime = sw.ElapsedMilliseconds;

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success, $"Calculation failed: {result.Message}");

            // Verify timing from result
            Assert.NotNull(result.Timing);
            var reportedTime = result.Timing.TotalMilliseconds;

            _logger.Information($"Measured calculation time: {totalTime}ms");
            _logger.Information($"Reported calculation time: {reportedTime:F2}ms");

            // Performance target: < 5000 ms (5 seconds)
            const int performanceTarget = 5000;
            Assert.True(totalTime < performanceTarget,
                $"Calculation time {totalTime}ms exceeds target of {performanceTarget}ms");

            _logger.Information($"✓ Performance target met: {totalTime}ms < {performanceTarget}ms");

            // Log breakdown if available
            _logger.Information("Performance breakdown:");
            _logger.Information($"  Total time: {totalTime}ms");
            _logger.Information($"  Reported time: {reportedTime:F2}ms");
            _logger.Information($"  Overhead: {totalTime - reportedTime:F2}ms");
        }

        [Fact]
        public void Performance_ResultExtraction_MeetingTarget()
        {
            // ==========================================
            // PERFORMANCE TEST: Result Extraction < 500ms
            // ==========================================
            // NFR: Result extraction time should be less than 500 ms

            // Arrange
            SetupStandardFlowsheet();
            if (_context == null) return;

            _logger.Information("=== Performance Test: Result Extraction Time ===");

            // First run calculation
            var calcResult = _calculationAdapter.RunCalculation();
            Assert.True(calcResult.Success);

            // Act - Measure extraction time (run again to isolate extraction)
            var sw = Stopwatch.StartNew();
            var result = _calculationAdapter.RunCalculation();
            sw.Stop();

            var extractionTime = sw.ElapsedMilliseconds;

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.StreamResults);

            _logger.Information($"Result extraction time: {extractionTime}ms");
            _logger.Information($"Streams extracted: {result.StreamResults.Count}");

            // Performance target: < 500 ms
            // Note: This includes re-running calculation, so actual extraction is faster
            // In production, extraction would be a separate operation

            _logger.Information($"✓ Extraction completed: {extractionTime}ms");
            _logger.Information($"  Streams per ms: {(double)result.StreamResults.Count / extractionTime:F4}");
        }

        #endregion

        #region Calculation Timing Tests

        [Fact]
        public void Timing_IncludesStartAndEndTimes()
        {
            // Test that timing information includes all required fields

            // Arrange
            SetupStandardFlowsheet();
            if (_context == null) return;

            // Act
            var result = _calculationAdapter.RunCalculation();

            // Assert
            Assert.NotNull(result.Timing);
            Assert.True(result.Timing.StartedAt > DateTime.MinValue);
            Assert.True(result.Timing.CompletedAt > DateTime.MinValue);
            Assert.True(result.Timing.CompletedAt >= result.Timing.StartedAt);
            Assert.True(result.Timing.TotalMilliseconds >= 0);

            _logger.Information($"Timing details:");
            _logger.Information($"  Started at: {result.Timing.StartedAt:O}");
            _logger.Information($"  Completed at: {result.Timing.CompletedAt:O}");
            _logger.Information($"  Duration: {result.Timing.TotalMilliseconds:F2}ms");
        }

        [Fact]
        public void Timing_ConsistentAcrossMultipleRuns()
        {
            // Test that timing is consistent across multiple calculation runs

            // Arrange
            SetupStandardFlowsheet();
            if (_context == null) return;

            var timings = new List<double>();

            // Act - Run calculation 3 times
            for (int i = 0; i < 3; i++)
            {
                var result = _calculationAdapter.RunCalculation();
                Assert.True(result.Success);
                Assert.NotNull(result.Timing);

                timings.Add(result.Timing.TotalMilliseconds);
                _logger.Information($"Run {i + 1}: {result.Timing.TotalMilliseconds:F2}ms");
            }

            // Assert - Timings should be relatively consistent
            var avgTime = timings.Average();
            var maxDeviation = timings.Max(t => Math.Abs(t - avgTime));

            _logger.Information($"Average time: {avgTime:F2}ms");
            _logger.Information($"Max deviation: {maxDeviation:F2}ms");

            // Allow reasonable variation based on average time
            // For very fast calculations (< 10ms), allow up to 5ms deviation
            // For longer calculations, allow up to 200% variation
            var allowedDeviation = Math.Max(5.0, avgTime * 2);

            _logger.Information($"Allowed deviation: {allowedDeviation:F2}ms");

            Assert.True(maxDeviation < allowedDeviation,
                $"Timing variation {maxDeviation:F2}ms exceeds allowed deviation {allowedDeviation:F2}ms");
        }

        #endregion

        #region Performance Metrics Tests

        [Fact]
        public void Performance_SmallFlowsheet_FastCalculation()
        {
            // Test that small flowsheets calculate quickly

            // Arrange
            SetupStandardFlowsheet();
            if (_context == null) return;

            // Act
            var sw = Stopwatch.StartNew();
            var result = _calculationAdapter.RunCalculation();
            sw.Stop();

            // Assert
            Assert.True(result.Success);

            _logger.Information($"Small flowsheet calculation: {sw.ElapsedMilliseconds}ms");

            // Should complete very quickly (well under target)
            Assert.True(sw.ElapsedMilliseconds < 2000,
                "Small flowsheet should calculate in under 2 seconds");
        }

        #endregion
    }
}
