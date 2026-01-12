using System;
using System.Diagnostics;
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
    /// Performance tests for property set operations and complete workflow.
    /// Validates that operations meet NFR performance targets:
    /// - AddCompound: less than 100ms
    /// - SetPropertyPackage: less than 500ms
    /// - CreateStream: less than 200ms
    /// - SetProperty: less than 50ms
    /// - AddThreePhaseSeparator: less than 300ms
    /// - ConnectStream: less than 100ms
    /// - Full workflow (4 compounds, 4 streams, 1 separator): less than 2 seconds
    /// - Memory footprint: less than 50MB for configured flowsheet
    ///
    /// IMPORTANT: These tests require DWSIM assemblies to be available.
    /// Tests will fail if DWSIM assemblies are not found at the configured path.
    /// </summary>
    [Trait("Category", "Performance")]
    public class PropertySetPerformanceTests : IDisposable
    {
        private readonly Logger _logger;
        private readonly ITestOutputHelper _output;
        private FlowsheetContext _context;
        private CompoundAdapter _compoundAdapter;
        private PropertyPackageAdapter _propertyPackageAdapter;
        private StreamAdapter _streamAdapter;
        private UnitOpAdapter _unitOpAdapter;
        private ConnectionAdapter _connectionAdapter;

        public PropertySetPerformanceTests(ITestOutputHelper output)
        {
            _output = output;

            // Configure Serilog for performance tests
            _logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.TestOutput(output)
                .CreateLogger();

            // Validate DWSIM path is available for testing
            if (!TestConfiguration.ValidateDwsimPath())
            {
                throw new InvalidOperationException(
                    $"DWSIM assemblies not found for testing. {TestConfiguration.GetValidationErrorMessage()}");
            }
        }

        public void Dispose()
        {
            _context?.Dispose();
            _logger?.Dispose();
        }

        private void InitializeContext()
        {
            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)  // Skip validation for performance tests
                .WithFlowsheetName("Performance Test Flowsheet")
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

        private StreamProperties CreateTestStreamProperties(
            double temperature = 298.15,
            double pressure = 500000,
            double molarFlow = 100.0,
            double[] composition = null)
        {
            composition = composition ?? new[] { 0.4, 0.3, 0.2, 0.1 };

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

        #region Individual Operation Performance Tests

        [Fact]
        public void AddCompound_Latency_Under100ms()
        {
            // Arrange
            InitializeContext();
            var compounds = new[] { "Water", "Methane", "Ethane", "Propane", "Butane" };
            var stopwatch = new Stopwatch();

            // Act - Measure multiple iterations
            for (int i = 0; i < compounds.Length; i++)
            {
                stopwatch.Start();
                var result = _compoundAdapter.AddCompound(compounds[i]);
                stopwatch.Stop();

                Assert.True(result.Success, $"AddCompound failed: {result.Message}");
            }

            // Calculate average
            var avgMs = stopwatch.Elapsed.TotalMilliseconds / compounds.Length;

            _output.WriteLine($"AddCompound average latency: {avgMs:F2}ms (target: < 100ms, iterations: {compounds.Length})");

            // Assert
            Assert.True(avgMs < 100, $"AddCompound latency was {avgMs:F2}ms, expected < 100ms");

            _logger.Information("✓ Performance test passed: AddCompound average latency = {Latency:F2}ms (target: < 100ms)",
                avgMs);
        }

        [Fact]
        public void SetPropertyPackage_Latency_Under500ms()
        {
            // Arrange
            InitializeContext();
            _compoundAdapter.AddCompound("Water");
            _compoundAdapter.AddCompound("Ethanol");

            var stopwatch = new Stopwatch();

            // Act - Measure single operation (property package can only be set once effectively)
            stopwatch.Start();
            var result = _propertyPackageAdapter.SetPropertyPackage("Peng-Robinson");
            stopwatch.Stop();

            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            _output.WriteLine($"SetPropertyPackage latency: {elapsedMs:F2}ms (target: < 500ms)");

            // Assert
            Assert.True(result.Success, $"SetPropertyPackage failed: {result.Message}");
            Assert.True(elapsedMs < 500, $"SetPropertyPackage latency was {elapsedMs:F2}ms, expected < 500ms");

            _logger.Information("✓ Performance test passed: SetPropertyPackage latency = {Latency:F2}ms (target: < 500ms)",
                elapsedMs);
        }

        [Fact]
        public void CreateStream_Latency_Under200ms()
        {
            // Arrange
            InitializeContext();
            _compoundAdapter.AddCompound("Methane");
            _compoundAdapter.AddCompound("Ethane");
            _compoundAdapter.AddCompound("Propane");
            _compoundAdapter.AddCompound("Water");
            _propertyPackageAdapter.SetPropertyPackage("Peng-Robinson");

            const int iterations = 5;
            var stopwatch = new Stopwatch();
            var properties = CreateTestStreamProperties();

            // Act - Measure multiple stream creations
            for (int i = 0; i < iterations; i++)
            {
                stopwatch.Start();
                var result = _streamAdapter.CreateStream($"STREAM{i}", properties);
                stopwatch.Stop();

                Assert.True(result.Success, $"CreateStream failed: {result.Message}");
            }

            // Calculate average
            var avgMs = stopwatch.Elapsed.TotalMilliseconds / iterations;

            _output.WriteLine($"CreateStream average latency: {avgMs:F2}ms (target: < 200ms, iterations: {iterations})");

            // Assert
            Assert.True(avgMs < 200, $"CreateStream latency was {avgMs:F2}ms, expected < 200ms");

            _logger.Information("✓ Performance test passed: CreateStream average latency = {Latency:F2}ms (target: < 200ms)",
                avgMs);
        }

        [Fact]
        public void SetProperty_SingleProperty_Latency_Under50ms()
        {
            // Arrange
            InitializeContext();
            _compoundAdapter.AddCompound("Water");
            _compoundAdapter.AddCompound("Ethanol");
            _propertyPackageAdapter.SetPropertyPackage("NRTL");

            var properties = CreateTestStreamProperties();
            var streamResult = _streamAdapter.CreateStream("TEST_STREAM", properties);
            Assert.True(streamResult.Success);
            var streamId = streamResult.ObjectId;

            const int iterations = 10;
            var stopwatch = new Stopwatch();

            // Act - Measure multiple property sets
            for (int i = 0; i < iterations; i++)
            {
                // Alternate between two temperatures
                var newTemp = 300.15 + (i % 2);

                stopwatch.Start();
                var result = _streamAdapter.SetProperty(streamId, "Temperature", newTemp);
                stopwatch.Stop();

                Assert.True(result.Success, $"SetProperty failed: {result.Message}");
            }

            // Calculate average
            var avgMs = stopwatch.Elapsed.TotalMilliseconds / iterations;

            _output.WriteLine($"SetProperty average latency: {avgMs:F2}ms (target: < 50ms, iterations: {iterations})");

            // Assert
            Assert.True(avgMs < 50, $"SetProperty latency was {avgMs:F2}ms, expected < 50ms");

            _logger.Information("✓ Performance test passed: SetProperty average latency = {Latency:F2}ms (target: < 50ms)",
                avgMs);
        }

        [Fact]
        public void AddThreePhaseSeparator_Latency_Under300ms()
        {
            // Arrange
            InitializeContext();
            _compoundAdapter.AddCompound("Water");
            _compoundAdapter.AddCompound("Ethanol");
            _propertyPackageAdapter.SetPropertyPackage("Peng-Robinson");

            const int iterations = 5;
            var stopwatch = new Stopwatch();

            var config = new UnitOpConfig(new System.Collections.Generic.Dictionary<string, object>
            {
                { "PressureDrop", 5000.0 }
            });

            // Act - Measure multiple separator additions
            for (int i = 0; i < iterations; i++)
            {
                stopwatch.Start();
                var result = _unitOpAdapter.AddThreePhaseSeparator($"SEP{i}", config);
                stopwatch.Stop();

                Assert.True(result.Success, $"AddThreePhaseSeparator failed: {result.Message}");
            }

            // Calculate average
            var avgMs = stopwatch.Elapsed.TotalMilliseconds / iterations;

            _output.WriteLine($"AddThreePhaseSeparator average latency: {avgMs:F2}ms (target: < 300ms, iterations: {iterations})");

            // Assert
            Assert.True(avgMs < 300, $"AddThreePhaseSeparator latency was {avgMs:F2}ms, expected < 300ms");

            _logger.Information("✓ Performance test passed: AddThreePhaseSeparator average latency = {Latency:F2}ms (target: < 300ms)",
                avgMs);
        }

        [Fact]
        public void ConnectStream_Latency_Under100ms()
        {
            // Arrange
            InitializeContext();
            _compoundAdapter.AddCompound("Water");
            _compoundAdapter.AddCompound("Ethanol");
            _propertyPackageAdapter.SetPropertyPackage("Peng-Robinson");

            var properties = CreateTestStreamProperties();

            // Create separator
            var separatorResult = _unitOpAdapter.AddThreePhaseSeparator("SEP1", null);
            Assert.True(separatorResult.Success);
            var separatorId = separatorResult.ObjectId;

            // Create multiple streams
            var stream1 = _streamAdapter.CreateStream("INLET", properties);
            var stream2 = _streamAdapter.CreateStream("VAPOR", properties);
            var stream3 = _streamAdapter.CreateStream("LIQUID1", properties);
            var stream4 = _streamAdapter.CreateStream("LIQUID2", properties);

            Assert.True(stream1.Success && stream2.Success && stream3.Success && stream4.Success);

            var streamIds = new[] { stream1.ObjectId, stream2.ObjectId, stream3.ObjectId, stream4.ObjectId };
            var ports = new[] { "Inlet", "VaporOutlet", "LiquidOutlet1", "LiquidOutlet2" };

            var stopwatch = new Stopwatch();

            // Act - Measure multiple stream connections
            for (int i = 0; i < streamIds.Length; i++)
            {
                stopwatch.Start();
                var result = _connectionAdapter.ConnectStream(streamIds[i], separatorId, ports[i]);
                stopwatch.Stop();

                Assert.True(result.Success, $"ConnectStream failed: {result.Message}");
            }

            // Calculate average
            var avgMs = stopwatch.Elapsed.TotalMilliseconds / streamIds.Length;

            _output.WriteLine($"ConnectStream average latency: {avgMs:F2}ms (target: < 100ms, connections: {streamIds.Length})");

            // Assert
            Assert.True(avgMs < 100, $"ConnectStream latency was {avgMs:F2}ms, expected < 100ms");

            _logger.Information("✓ Performance test passed: ConnectStream average latency = {Latency:F2}ms (target: < 100ms)",
                avgMs);
        }

        #endregion

        #region Full Workflow Performance Test

        [Fact]
        public void FullWorkflow_4Compounds4Streams1Separator_Under2Seconds()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();

            // Act - Complete workflow
            InitializeContext();

            // Step 1: Add 4 compounds
            _compoundAdapter.AddCompound("Methane");
            _compoundAdapter.AddCompound("Ethane");
            _compoundAdapter.AddCompound("Propane");
            _compoundAdapter.AddCompound("Water");

            // Step 2: Set property package
            _propertyPackageAdapter.SetPropertyPackage("Peng-Robinson");

            // Step 3: Create 4 streams
            var properties = CreateTestStreamProperties();
            var inlet = _streamAdapter.CreateStream("INLET", properties);
            var vapor = _streamAdapter.CreateStream("VAPOR", properties);
            var liquid1 = _streamAdapter.CreateStream("LIQUID1", properties);
            var liquid2 = _streamAdapter.CreateStream("LIQUID2", properties);

            Assert.True(inlet.Success && vapor.Success && liquid1.Success && liquid2.Success);

            // Step 4: Add three-phase separator
            var separatorConfig = new UnitOpConfig(new System.Collections.Generic.Dictionary<string, object>
            {
                { "PressureDrop", 5000.0 }
            });
            var separator = _unitOpAdapter.AddThreePhaseSeparator("SEP1", separatorConfig);
            Assert.True(separator.Success);

            // Step 5: Connect all streams
            _connectionAdapter.ConnectStream(inlet.ObjectId, separator.ObjectId, "Inlet");
            _connectionAdapter.ConnectStream(vapor.ObjectId, separator.ObjectId, "VaporOutlet");
            _connectionAdapter.ConnectStream(liquid1.ObjectId, separator.ObjectId, "LiquidOutlet1");
            _connectionAdapter.ConnectStream(liquid2.ObjectId, separator.ObjectId, "LiquidOutlet2");

            // Step 6: Validate topology
            var validation = _connectionAdapter.ValidateTopology();
            Assert.True(validation.Success);

            stopwatch.Stop();

            // Assert
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

            _output.WriteLine($"Full workflow completion time: {elapsedSeconds:F3}s ({elapsedMs:F0}ms) (target: < 2.0s)");
            _output.WriteLine("Workflow steps:");
            _output.WriteLine("  - 4 compounds added");
            _output.WriteLine("  - 1 property package set");
            _output.WriteLine("  - 4 streams created");
            _output.WriteLine("  - 1 three-phase separator added");
            _output.WriteLine("  - 4 stream connections made");
            _output.WriteLine("  - Topology validated");

            Assert.True(elapsedSeconds < 2.0, $"Full workflow took {elapsedSeconds:F3}s, expected < 2.0s");

            _logger.Information("✓ Performance test passed: Full workflow time = {Time:F3}s (target: < 2.0s)",
                elapsedSeconds);
        }

        #endregion

        #region Memory Footprint Test

        [Fact]
        public void MemoryFootprint_ConfiguredFlowsheet_Under50MB()
        {
            // Arrange - Force garbage collection before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memoryBefore = GC.GetTotalMemory(true) / (1024.0 * 1024.0);

            // Act - Create fully configured flowsheet
            InitializeContext();

            _compoundAdapter.AddCompound("Methane");
            _compoundAdapter.AddCompound("Ethane");
            _compoundAdapter.AddCompound("Propane");
            _compoundAdapter.AddCompound("Water");

            _propertyPackageAdapter.SetPropertyPackage("Peng-Robinson");

            var properties = CreateTestStreamProperties();
            var inlet = _streamAdapter.CreateStream("INLET", properties);
            var vapor = _streamAdapter.CreateStream("VAPOR", properties);
            var liquid1 = _streamAdapter.CreateStream("LIQUID1", properties);
            var liquid2 = _streamAdapter.CreateStream("LIQUID2", properties);

            var separator = _unitOpAdapter.AddThreePhaseSeparator("SEP1", null);

            _connectionAdapter.ConnectStream(inlet.ObjectId, separator.ObjectId, "Inlet");
            _connectionAdapter.ConnectStream(vapor.ObjectId, separator.ObjectId, "VaporOutlet");
            _connectionAdapter.ConnectStream(liquid1.ObjectId, separator.ObjectId, "LiquidOutlet1");
            _connectionAdapter.ConnectStream(liquid2.ObjectId, separator.ObjectId, "LiquidOutlet2");

            // Force garbage collection after configuration
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memoryAfter = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
            var memoryDelta = memoryAfter - memoryBefore;

            // Assert
            _output.WriteLine($"Memory before configuration: {memoryBefore:F2} MB");
            _output.WriteLine($"Memory after configuration:  {memoryAfter:F2} MB");
            _output.WriteLine($"Memory delta:                {memoryDelta:F2} MB (target: < 50 MB)");

            // Note: Memory footprint target is for the flowsheet configuration, not including DWSIM assemblies
            // Allow more tolerance in CI environments
            Assert.True(memoryDelta < 50, $"Memory footprint delta was {memoryDelta:F2}MB, expected < 50MB");

            _logger.Information("✓ Performance test passed: Memory footprint delta = {Memory:F2}MB (target: < 50MB)",
                memoryDelta);
        }

        #endregion

        #region Performance Baseline Test

        [Fact]
        public void PerformanceBaseline_EstablishMetrics()
        {
            // This test establishes performance baselines for future comparison
            // Useful for detecting performance regressions

            // Arrange
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memoryBefore = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
            var stopwatch = Stopwatch.StartNew();

            // Act - Complete workflow with timing for each phase
            var sw1 = Stopwatch.StartNew();
            InitializeContext();
            sw1.Stop();

            var sw2 = Stopwatch.StartNew();
            _compoundAdapter.AddCompound("Methane");
            _compoundAdapter.AddCompound("Ethane");
            _compoundAdapter.AddCompound("Propane");
            _compoundAdapter.AddCompound("Water");
            sw2.Stop();

            var sw3 = Stopwatch.StartNew();
            _propertyPackageAdapter.SetPropertyPackage("Peng-Robinson");
            sw3.Stop();

            var sw4 = Stopwatch.StartNew();
            var properties = CreateTestStreamProperties();
            var inlet = _streamAdapter.CreateStream("INLET", properties);
            var vapor = _streamAdapter.CreateStream("VAPOR", properties);
            var liquid1 = _streamAdapter.CreateStream("LIQUID1", properties);
            var liquid2 = _streamAdapter.CreateStream("LIQUID2", properties);
            sw4.Stop();

            var sw5 = Stopwatch.StartNew();
            var separator = _unitOpAdapter.AddThreePhaseSeparator("SEP1", null);
            sw5.Stop();

            var sw6 = Stopwatch.StartNew();
            _connectionAdapter.ConnectStream(inlet.ObjectId, separator.ObjectId, "Inlet");
            _connectionAdapter.ConnectStream(vapor.ObjectId, separator.ObjectId, "VaporOutlet");
            _connectionAdapter.ConnectStream(liquid1.ObjectId, separator.ObjectId, "LiquidOutlet1");
            _connectionAdapter.ConnectStream(liquid2.ObjectId, separator.ObjectId, "LiquidOutlet2");
            _connectionAdapter.ValidateTopology();
            sw6.Stop();

            stopwatch.Stop();

            // Measure memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memoryAfter = GC.GetTotalMemory(true) / (1024.0 * 1024.0);

            // Report metrics
            var metrics = new
            {
                TotalTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                InitializeMs = sw1.Elapsed.TotalMilliseconds,
                AddCompoundsMs = sw2.Elapsed.TotalMilliseconds,
                SetPropertyPackageMs = sw3.Elapsed.TotalMilliseconds,
                CreateStreamsMs = sw4.Elapsed.TotalMilliseconds,
                AddSeparatorMs = sw5.Elapsed.TotalMilliseconds,
                ConnectAndValidateMs = sw6.Elapsed.TotalMilliseconds,
                MemoryBeforeMB = memoryBefore,
                MemoryAfterMB = memoryAfter,
                MemoryDeltaMB = memoryAfter - memoryBefore,
                TestTimestamp = DateTime.UtcNow
            };

            // Output metrics in structured format
            _output.WriteLine("=== PERFORMANCE BASELINE METRICS ===");
            _output.WriteLine($"Test Timestamp:              {metrics.TestTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
            _output.WriteLine($"Total Time:                  {metrics.TotalTimeMs:F0}ms");
            _output.WriteLine($"  - Initialize Context:      {metrics.InitializeMs:F0}ms");
            _output.WriteLine($"  - Add 4 Compounds:         {metrics.AddCompoundsMs:F0}ms");
            _output.WriteLine($"  - Set Property Package:    {metrics.SetPropertyPackageMs:F0}ms");
            _output.WriteLine($"  - Create 4 Streams:        {metrics.CreateStreamsMs:F0}ms");
            _output.WriteLine($"  - Add Separator:           {metrics.AddSeparatorMs:F0}ms");
            _output.WriteLine($"  - Connect & Validate:      {metrics.ConnectAndValidateMs:F0}ms");
            _output.WriteLine($"Memory Before:               {metrics.MemoryBeforeMB:F2}MB");
            _output.WriteLine($"Memory After:                {metrics.MemoryAfterMB:F2}MB");
            _output.WriteLine($"Memory Delta:                {metrics.MemoryDeltaMB:F2}MB");
            _output.WriteLine("====================================");

            _logger.Information(
                "Performance baseline: Total={Total:F0}ms, Init={Init:F0}ms, Compounds={Comp:F0}ms, PropPkg={PP:F0}ms, Streams={Str:F0}ms, Sep={Sep:F0}ms, Connect={Conn:F0}ms, Memory={Mem:F2}MB",
                metrics.TotalTimeMs, metrics.InitializeMs, metrics.AddCompoundsMs, metrics.SetPropertyPackageMs,
                metrics.CreateStreamsMs, metrics.AddSeparatorMs, metrics.ConnectAndValidateMs, metrics.MemoryDeltaMB);
        }

        #endregion
    }
}
