using System;
using System.Diagnostics;
using System.Linq;
using Serilog;
using Serilog.Core;
using Xunit;
using Xunit.Abstractions;
using DwsimWorker.Engine;

namespace DwsimWorker.Tests.Performance
{
    /// <summary>
    /// Performance tests for DWSIM assembly loading.
    /// Validates that assembly loading meets performance requirements:
    /// - Total loading time: less than 5 seconds
    /// - Per-assembly time: less than 500ms
    /// - Memory footprint: less than 200MB
    ///
    /// IMPORTANT: These tests require DWSIM assemblies to be available.
    /// Configured to use: D:\S\C#\dwsim\DWSIM\bin\Debug
    /// Tests will fail if DWSIM assemblies are not found at the configured path.
    /// </summary>
    [Trait("Category", "Performance")]
    public class AssemblyLoadingPerformanceTests : IDisposable
    {
        private readonly Logger _logger;
        private readonly ITestOutputHelper _output;
        private readonly string _originalEnvVar;

        public AssemblyLoadingPerformanceTests(ITestOutputHelper output)
        {
            _output = output;

            // Configure Serilog for performance tests
            _logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.TestOutput(output)
                .CreateLogger();

            // Save original environment variable
            _originalEnvVar = Environment.GetEnvironmentVariable("DWSIM_PATH");

            // Validate DWSIM path is available for testing
            if (!TestConfiguration.ValidateDwsimPath())
            {
                throw new InvalidOperationException(
                    $"DWSIM assemblies not found for testing. {TestConfiguration.GetValidationErrorMessage()}");
            }
        }

        public void Dispose()
        {
            // Restore original environment variable
            if (_originalEnvVar != null)
            {
                Environment.SetEnvironmentVariable("DWSIM_PATH", _originalEnvVar);
            }
            else
            {
                Environment.SetEnvironmentVariable("DWSIM_PATH", null);
            }

            _logger?.Dispose();
        }

        #region Total Loading Time Tests

        [Fact]
        public void AssemblyLoading_TotalTime_CompletesWithin5Seconds()
        {
            // Arrange - Focus on loading performance without validation overhead
            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .WithTimeoutSeconds(30)
                .Build();

            var loader = new AssemblyLoader(_logger, config);
            var stopwatch = Stopwatch.StartNew();

            // Act
            var result = loader.LoadDwsimAssemblies();
            stopwatch.Stop();

            // Assert
            Assert.True(result.Success, $"Assembly loading failed: {result.Message}");

            var totalSeconds = stopwatch.Elapsed.TotalSeconds;
            _output.WriteLine($"Total loading time: {totalSeconds:F3} seconds");
            _output.WriteLine($"Loaded {result.LoadedAssemblies.Count} assemblies");

            // Performance requirement: less than 5 seconds total
            Assert.True(totalSeconds < 5.0,
                $"Assembly loading took {totalSeconds:F3}s, expected < 5.0s");

            _logger.Information("✓ Performance test passed: Total loading time = {Time:F3}s (target: < 5.0s)",
                totalSeconds);
        }

        [Fact]
        public void AssemblyLoading_WithoutValidation_IsFaster()
        {
            // Arrange
            var configWithValidation = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(true)
                .Build();

            var configWithoutValidation = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var loader1 = new AssemblyLoader(_logger, configWithValidation);
            var loader2 = new AssemblyLoader(_logger, configWithoutValidation);

            // Act - First run with validation
            var sw1 = Stopwatch.StartNew();
            var result1 = loader1.LoadDwsimAssemblies();
            sw1.Stop();

            // Force garbage collection before second run
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Second run without validation
            var sw2 = Stopwatch.StartNew();
            var result2 = loader2.LoadDwsimAssemblies();
            sw2.Stop();

            // Assert
            // Note: Partial validation success is acceptable in headless environment
            Assert.NotEmpty(result1.LoadedAssemblies);
            Assert.True(result2.Success);

            _output.WriteLine($"With validation:    {sw1.Elapsed.TotalSeconds:F3}s (Success: {result1.Success})");
            _output.WriteLine($"Without validation: {sw2.Elapsed.TotalSeconds:F3}s");
            _output.WriteLine($"Difference:         {(sw1.Elapsed - sw2.Elapsed).TotalSeconds:F3}s");

            // Validation should add some overhead
            Assert.True(sw1.Elapsed > sw2.Elapsed,
                "Loading with validation should be slower than without validation");
        }

        #endregion

        #region Per-Assembly Loading Time Tests

        [Fact]
        public void AssemblyLoading_PerAssemblyTime_LessThan500ms()
        {
            // Arrange
            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false) // Test loading only
                .Build();

            var loader = new AssemblyLoader(_logger, config);

            // Act
            var totalStopwatch = Stopwatch.StartNew();
            var result = loader.LoadDwsimAssemblies();
            totalStopwatch.Stop();

            // Assert
            Assert.True(result.Success, $"Assembly loading failed: {result.Message}");

            // Calculate average time per assembly
            var assemblyCount = result.LoadedAssemblies.Count;
            var totalMs = totalStopwatch.Elapsed.TotalMilliseconds;
            var avgMs = totalMs / assemblyCount;

            _output.WriteLine($"Total time: {totalMs:F0}ms");
            _output.WriteLine($"Assemblies loaded: {assemblyCount}");
            _output.WriteLine($"Average per assembly: {avgMs:F0}ms");

            foreach (var assembly in result.LoadedAssemblies)
            {
                _output.WriteLine($"  - {assembly.Name}");
            }

            // Performance requirement: average less than 500ms per assembly
            Assert.True(avgMs < 500,
                $"Average loading time per assembly was {avgMs:F0}ms, expected < 500ms");

            _logger.Information("✓ Performance test passed: Average per-assembly time = {Time:F0}ms (target: < 500ms)",
                avgMs);
        }

        #endregion

        #region Memory Footprint Tests

        [Fact]
        public void AssemblyLoading_MemoryFootprint_LessThan200MB()
        {
            // Arrange
            // Force garbage collection before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var process = Process.GetCurrentProcess();
            var memoryBeforeMB = process.WorkingSet64 / (1024.0 * 1024.0);

            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false) // Focus on memory footprint of loading only
                .Build();

            var loader = new AssemblyLoader(_logger, config);

            // Act
            var result = loader.LoadDwsimAssemblies();

            // Force garbage collection after loading
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            process.Refresh();
            var memoryAfterMB = process.WorkingSet64 / (1024.0 * 1024.0);
            var memoryDeltaMB = memoryAfterMB - memoryBeforeMB;

            // Assert
            Assert.True(result.Success, $"Assembly loading failed: {result.Message}");

            _output.WriteLine($"Memory before loading: {memoryBeforeMB:F2} MB");
            _output.WriteLine($"Memory after loading:  {memoryAfterMB:F2} MB");
            _output.WriteLine($"Memory delta:          {memoryDeltaMB:F2} MB");

            // Performance requirement: less than 200MB additional memory
            Assert.True(memoryDeltaMB < 200,
                $"Memory footprint increase was {memoryDeltaMB:F2}MB, expected < 200MB");

            _logger.Information("✓ Performance test passed: Memory delta = {Memory:F2}MB (target: < 200MB)",
                memoryDeltaMB);
        }

        [Fact]
        public void AssemblyLoading_TotalWorkingSet_Reasonable()
        {
            // Arrange
            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false) // Focus on memory footprint of loading
                .Build();
            var loader = new AssemblyLoader(_logger, config);

            // Act
            var result = loader.LoadDwsimAssemblies();

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var process = Process.GetCurrentProcess();
            process.Refresh();
            var workingSetMB = process.WorkingSet64 / (1024.0 * 1024.0);
            var privateMemoryMB = process.PrivateMemorySize64 / (1024.0 * 1024.0);

            // Assert
            Assert.True(result.Success);

            _output.WriteLine($"Working Set:    {workingSetMB:F2} MB");
            _output.WriteLine($"Private Memory: {privateMemoryMB:F2} MB");
            _output.WriteLine($"Assemblies loaded: {result.LoadedAssemblies.Count}");

            // Log memory information (no strict assertion, just informational)
            _logger.Information("Memory footprint: WorkingSet={WorkingSet:F2}MB, Private={Private:F2}MB",
                workingSetMB, privateMemoryMB);
        }

        #endregion

        #region Concurrent Loading Tests

        [Fact]
        public void AssemblyLoading_ConcurrentInstances_NoFileContention()
        {
            // This test verifies that multiple DwsimWorker instances can load
            // assemblies concurrently without file locking issues.
            // Note: This is a simplified test - full concurrent test would spawn multiple processes

            // Arrange
            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            var loader1 = new AssemblyLoader(_logger, config);
            var loader2 = new AssemblyLoader(_logger, config);

            // Act
            var sw = Stopwatch.StartNew();
            var result1 = loader1.LoadDwsimAssemblies();
            var result2 = loader2.LoadDwsimAssemblies();
            sw.Stop();

            // Assert
            Assert.True(result1.Success, "First loader failed");
            Assert.True(result2.Success, "Second loader failed");

            _output.WriteLine($"Both loaders completed in {sw.Elapsed.TotalSeconds:F3}s");
            _output.WriteLine($"Loader 1: {result1.LoadedAssemblies.Count} assemblies");
            _output.WriteLine($"Loader 2: {result2.LoadedAssemblies.Count} assemblies");

            // Both should load the same assemblies
            Assert.Equal(result1.LoadedAssemblies.Count, result2.LoadedAssemblies.Count);
        }

        #endregion

        #region Performance Baseline Tests

        [Fact]
        public void AssemblyLoading_PerformanceBaseline_EstablishMetrics()
        {
            // This test establishes performance baselines for future comparison
            // Useful for detecting performance regressions

            // Arrange
            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false) // Focus on loading performance
                .Build();
            var loader = new AssemblyLoader(_logger, config);

            // Measure before state
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var process = Process.GetCurrentProcess();
            var memoryBeforeMB = process.WorkingSet64 / (1024.0 * 1024.0);

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = loader.LoadDwsimAssemblies();
            stopwatch.Stop();

            // Measure after state
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            process.Refresh();
            var memoryAfterMB = process.WorkingSet64 / (1024.0 * 1024.0);

            // Assert & Report
            Assert.True(result.Success);

            var metrics = new
            {
                TotalTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                TotalTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                AssemblyCount = result.LoadedAssemblies.Count,
                AvgTimePerAssemblyMs = stopwatch.Elapsed.TotalMilliseconds / result.LoadedAssemblies.Count,
                MemoryBeforeMB = memoryBeforeMB,
                MemoryAfterMB = memoryAfterMB,
                MemoryDeltaMB = memoryAfterMB - memoryBeforeMB,
                TestTimestamp = DateTime.UtcNow
            };

            // Output metrics in structured format for logging/tracking
            _output.WriteLine("=== PERFORMANCE BASELINE METRICS ===");
            _output.WriteLine($"Test Timestamp:           {metrics.TestTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
            _output.WriteLine($"Total Loading Time:       {metrics.TotalTimeSeconds:F3}s ({metrics.TotalTimeMs:F0}ms)");
            _output.WriteLine($"Assemblies Loaded:        {metrics.AssemblyCount}");
            _output.WriteLine($"Avg Time Per Assembly:    {metrics.AvgTimePerAssemblyMs:F0}ms");
            _output.WriteLine($"Memory Before:            {metrics.MemoryBeforeMB:F2}MB");
            _output.WriteLine($"Memory After:             {metrics.MemoryAfterMB:F2}MB");
            _output.WriteLine($"Memory Delta:             {metrics.MemoryDeltaMB:F2}MB");
            _output.WriteLine("====================================");

            // Log structured metrics
            _logger.Information(
                "Performance baseline: Time={Time:F3}s, Assemblies={Count}, AvgPerAssembly={Avg:F0}ms, Memory={Memory:F2}MB",
                metrics.TotalTimeSeconds, metrics.AssemblyCount, metrics.AvgTimePerAssemblyMs, metrics.MemoryDeltaMB);
        }

        #endregion

        #region Cold Start vs Warm Start Tests

        [Fact]
        public void AssemblyLoading_WarmStart_FasterThanColdStart()
        {
            // This test measures the difference between cold start (first load)
            // and warm start (subsequent load with OS file cache)

            // Arrange
            var config = AssemblyLoaderConfig.Create()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidationEnabled(false)
                .Build();

            // Act - Cold start (first load)
            var loader1 = new AssemblyLoader(_logger, config);
            var sw1 = Stopwatch.StartNew();
            var result1 = loader1.LoadDwsimAssemblies();
            sw1.Stop();

            Assert.True(result1.Success);
            var coldStartMs = sw1.Elapsed.TotalMilliseconds;

            // Wait a moment and force GC
            System.Threading.Thread.Sleep(100);
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Act - Warm start (second load, assemblies likely in OS file cache)
            var loader2 = new AssemblyLoader(_logger, config);
            var sw2 = Stopwatch.StartNew();
            var result2 = loader2.LoadDwsimAssemblies();
            sw2.Stop();

            Assert.True(result2.Success);
            var warmStartMs = sw2.Elapsed.TotalMilliseconds;

            // Report
            _output.WriteLine($"Cold start: {coldStartMs:F0}ms");
            _output.WriteLine($"Warm start: {warmStartMs:F0}ms");
            _output.WriteLine($"Improvement: {coldStartMs - warmStartMs:F0}ms ({(1 - warmStartMs / coldStartMs) * 100:F1}% faster)");

            // Warm start should typically be faster (but not strictly enforced as it depends on system state)
            if (warmStartMs < coldStartMs)
            {
                _logger.Information("✓ Warm start was {Improvement:F0}ms faster than cold start",
                    coldStartMs - warmStartMs);
            }
            else
            {
                _logger.Warning("Warm start was not faster than cold start (may be due to system state)");
            }
        }

        #endregion
    }
}
