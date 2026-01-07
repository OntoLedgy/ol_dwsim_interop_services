using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Xunit;
using DwsimWorker.Engine;
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Integration
{
    /// <summary>
    /// Integration tests for concurrent session operations.
    /// </summary>
    [Trait("Category", "Integration")]
    public class SessionConcurrencyTests : IDisposable
    {
        private readonly Logger _logger;
        private readonly SessionManager _sessionManager;

        public SessionConcurrencyTests()
        {
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            if (!TestConfiguration.ValidateDwsimPath())
            {
                throw new InvalidOperationException(
                    $"DWSIM assemblies not found for testing. {TestConfiguration.GetValidationErrorMessage()}");
            }

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)
                .Build();

            _sessionManager = new SessionManager(_logger, config);
        }

        public void Dispose()
        {
            _sessionManager?.Dispose();
            _logger?.Dispose();
        }

        private static Task[] RunConcurrentActions(int count, Action action)
        {
            var tasks = new List<Task>(count);
            for (int i = 0; i < count; i++)
            {
                tasks.Add(Task.Run(action));
            }

            return tasks.ToArray();
        }

        [Fact]
        public async Task ConcurrentCreation_From5Threads_AllSucceed()
        {
            var results = new ConcurrentBag<OperationResult<Guid>>();

            var tasks = RunConcurrentActions(5, () =>
            {
                results.Add(_sessionManager.CreateSession());
            });

            await Task.WhenAll(tasks);

            Assert.Equal(5, results.Count);
            Assert.All(results, result => Assert.True(result.Success));
        }

        [Fact]
        public async Task ConcurrentCreation_10Sessions_AllHaveUniqueIds()
        {
            var results = new ConcurrentBag<OperationResult<Guid>>();

            var tasks = RunConcurrentActions(10, () =>
            {
                results.Add(_sessionManager.CreateSession());
            });

            await Task.WhenAll(tasks);

            var ids = results.Select(r => r.Data).ToList();
            Assert.Equal(10, ids.Count);
            Assert.Equal(10, ids.Distinct().Count());
        }

        [Fact]
        public async Task ConcurrentCreation_VerifyAllSessionsFunctional()
        {
            var results = new ConcurrentBag<OperationResult<Guid>>();

            var tasks = RunConcurrentActions(5, () =>
            {
                results.Add(_sessionManager.CreateSession());
            });

            await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                Assert.True(result.Success);
                var getResult = _sessionManager.GetSession(result.Data);
                Assert.True(getResult.Success);
                Assert.NotNull(getResult.Data);
            }
        }

        [Fact]
        public void SessionIsolation_DifferentCompounds_NoContamination()
        {
            var first = _sessionManager.CreateSession(flowsheetName: "Isolation-A");
            var second = _sessionManager.CreateSession(flowsheetName: "Isolation-B");

            Assert.True(first.Success);
            Assert.True(second.Success);

            var firstContext = _sessionManager.GetSession(first.Data).Data;
            var secondContext = _sessionManager.GetSession(second.Data).Data;

            firstContext.AddCompound("Water");
            secondContext.AddCompound("Methane");

            var firstCompounds = firstContext.GetCompounds();
            var secondCompounds = secondContext.GetCompounds();

            Assert.Contains("Water", firstCompounds);
            Assert.DoesNotContain("Methane", firstCompounds);
            Assert.Contains("Methane", secondCompounds);
            Assert.DoesNotContain("Water", secondCompounds);
        }

        [Fact]
        public void SessionIsolation_DifferentCalculations_IndependentResults()
        {
            var first = _sessionManager.CreateSession(flowsheetName: "Calc-A");
            var second = _sessionManager.CreateSession(flowsheetName: "Calc-B");

            Assert.True(first.Success);
            Assert.True(second.Success);

            var firstContext = _sessionManager.GetSession(first.Data).Data;
            var secondContext = _sessionManager.GetSession(second.Data).Data;

            firstContext.AddCompound("Water");
            firstContext.AddCompound("Ethanol");
            secondContext.AddCompound("Methane");

            var firstCompounds = firstContext.GetCompounds();
            var secondCompounds = secondContext.GetCompounds();

            Assert.Equal(2, firstCompounds.Count);
            Assert.Single(secondCompounds);
            Assert.Contains("Ethanol", firstCompounds);
            Assert.Contains("Methane", secondCompounds);
        }

        [Fact]
        public void SessionIsolation_OneSessionException_OthersContinue()
        {
            var first = _sessionManager.CreateSession(flowsheetName: "Error-A");
            var second = _sessionManager.CreateSession(flowsheetName: "Error-B");

            Assert.True(first.Success);
            Assert.True(second.Success);

            var firstContext = _sessionManager.GetSession(first.Data).Data;
            var secondContext = _sessionManager.GetSession(second.Data).Data;

            Assert.Throws<ArgumentNullException>(() => firstContext.AddCompound(null));

            secondContext.AddCompound("Propane");
            var secondCompounds = secondContext.GetCompounds();

            Assert.Contains("Propane", secondCompounds);
        }

        [Fact]
        public void StressTest_Create100Sessions_AllDispose()
        {
            var sessionIds = new List<Guid>(100);

            for (int i = 0; i < 100; i++)
            {
                var result = _sessionManager.CreateSession(flowsheetName: $"Stress-{i}");
                Assert.True(result.Success);
                sessionIds.Add(result.Data);
            }

            _sessionManager.CloseAllSessions();

            var sessions = _sessionManager.GetAllSessions();
            Assert.Empty(sessions);
        }

        [Fact]
        public void StressTest_VerifyMemoryCleanup()
        {
            var before = GC.GetTotalMemory(true);

            for (int i = 0; i < 50; i++)
            {
                var result = _sessionManager.CreateSession(flowsheetName: $"Memory-{i}");
                Assert.True(result.Success);
            }

            _sessionManager.CloseAllSessions();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var after = GC.GetTotalMemory(true);
            var delta = after - before;

            Assert.True(delta < 100 * 1024 * 1024, $"Memory delta too high: {delta} bytes");
        }

        [Fact]
        public async Task ConcurrentMixedOperations_CreateUseClose()
        {
            const int total = 30;
            var sessionIds = new ConcurrentQueue<Guid>();
            var exceptions = new ConcurrentBag<Exception>();
            int created = 0;

            var createTask = Task.Run(() =>
            {
                for (int i = 0; i < total; i++)
                {
                    try
                    {
                        var result = _sessionManager.CreateSession(flowsheetName: $"Mixed-{i}");
                        if (result.Success)
                        {
                            sessionIds.Enqueue(result.Data);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                    finally
                    {
                        Interlocked.Increment(ref created);
                    }
                }
            });

            var useTask = Task.Run(() =>
            {
                while (Volatile.Read(ref created) < total || !sessionIds.IsEmpty)
                {
                    if (sessionIds.TryPeek(out Guid sessionId))
                    {
                        try
                        {
                            _sessionManager.GetSession(sessionId);
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    }
                    else
                    {
                        Thread.Yield();
                    }
                }
            });

            var closeTask = Task.Run(() =>
            {
                while (Volatile.Read(ref created) < total || !sessionIds.IsEmpty)
                {
                    if (sessionIds.TryDequeue(out Guid sessionId))
                    {
                        try
                        {
                            _sessionManager.CloseSession(sessionId);
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    }
                    else
                    {
                        Thread.Yield();
                    }
                }
            });

            await Task.WhenAll(createTask, useTask, closeTask);

            Assert.Empty(exceptions);
        }
    }
}
