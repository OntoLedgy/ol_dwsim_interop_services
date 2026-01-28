using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using DwsimWorker.Engine;
using DwsimWorker.Observability;

namespace DwsimWorker
{
    /// <summary>
    /// Entry point for DWSIM engine worker process.
    /// Hosts DWSIM simulation engine and communicates with Python MCP server via Named Pipes.
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File("logs/dwsim-worker-.txt", rollingInterval: RollingInterval.Day)
                .Enrich.With<CorrelationEnricher>()
                .Enrich.WithProperty("Application", "DwsimWorker")
                .CreateLogger();

            // Configure OpenTelemetry tracing
            var tracingEnabled = Environment.GetEnvironmentVariable("DWSIM_TRACING_ENABLED");
            var tracingExporter = Environment.GetEnvironmentVariable("DWSIM_TRACING_EXPORTER") ?? "none";
            var tracingEndpoint = Environment.GetEnvironmentVariable("DWSIM_TRACING_ENDPOINT");
            var tracingSampleRate = Environment.GetEnvironmentVariable("DWSIM_TRACING_SAMPLE_RATE") ?? "1.0";

            TracingAdapter.Configure("DwsimWorker", tracingEndpoint ?? "http://localhost:4317");

            try
            {
                Log.Information("DWSIM Worker starting...");
                Log.Information(
                    "Observability configured: TracingEnabled={TracingEnabled}, Exporter={Exporter}, Endpoint={Endpoint}, SampleRate={SampleRate}",
                    tracingEnabled ?? "false",
                    tracingExporter,
                    tracingEndpoint ?? "(default)",
                    tracingSampleRate);

                // ===== ASSEMBLY LOADING =====
                Log.Information("Loading DWSIM assemblies...");

                // Create AssemblyLoader configuration with default settings
                var config = AssemblyLoaderConfig.Default();

                // Instantiate AssemblyLoader with logger and config
                var assemblyLoader = new AssemblyLoader(Log.Logger, config);

                // Load DWSIM assemblies
                var loadResult = assemblyLoader.LoadDwsimAssemblies();

                // Handle loading result
                if (!loadResult.Success)
                {
                    Log.Error("Failed to load DWSIM assemblies: {Message}", loadResult.Message);
                    if (loadResult.Error != null)
                    {
                        Log.Error(loadResult.Error, "Assembly loading error details");
                    }
                    Log.Information("Worker exiting with code {ExitCode}", loadResult.ExitCode);
                    return loadResult.ExitCode;
                }

                // Log successfully loaded assemblies
                Log.Information("Successfully loaded {Count} DWSIM assemblies", loadResult.LoadedAssemblies.Count);
                foreach (var assembly in loadResult.LoadedAssemblies)
                {
                    Log.Information("  - {Name} v{Version} from {Path}",
                        assembly.Name,
                        assembly.Version,
                        assembly.Path);
                }

                // TODO: Parse command line arguments
                // TODO: Load configuration
                // TODO: Initialize DWSIM engine host
                // TODO: Start Named Pipe server
                // TODO: Enter message processing loop

                Log.Information("DWSIM Worker initialized successfully");

                // Keep the process running
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                    Log.Information("Shutdown signal received");
                };

                await Task.Delay(Timeout.Infinite, cts.Token);

                return 0;
            }
            catch (OperationCanceledException)
            {
                Log.Information("Worker shutdown gracefully");
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Worker terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
