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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Xunit;
using Xunit.Abstractions;
using DwsimWorker.Engine;

namespace DwsimWorker.Tests.Performance
{
    /// <summary>
    /// Performance tests for SessionManager operations.
    /// </summary>
    [Trait("Category", "Performance")]
    [Trait("Category", "Integration")]
    public class SessionManagerPerformanceTests : IDisposable
    {
        private readonly Logger _logger;
        private readonly ITestOutputHelper _output;
        private SessionManager _sessionManager;

        public SessionManagerPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.TestOutput(output)
                .CreateLogger();
        }

        public void Dispose()
        {
            _sessionManager?.Dispose();
            _logger?.Dispose();
        }

        private void EnsureSessionManager()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                return;
            }

            if (_sessionManager != null)
            {
                return;
            }

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)
                .WithFlowsheetName("Performance Session")
                .Build();

            _sessionManager = new SessionManager(_logger, config);
        }

        [Fact]
        public void Performance_SessionCreation_Under2000ms()
        {
            EnsureSessionManager();
            if (_sessionManager == null) return;

            var sw = Stopwatch.StartNew();
            var result = _sessionManager.CreateSession();
            sw.Stop();

            Assert.True(result.Success);
            Assert.True(sw.ElapsedMilliseconds < 2000,
                $"Session creation took {sw.ElapsedMilliseconds}ms, expected < 2000ms");
        }

        [Fact]
        public void Performance_SessionRetrieval_Under1ms()
        {
            EnsureSessionManager();
            if (_sessionManager == null) return;

            var createResult = _sessionManager.CreateSession();
            Assert.True(createResult.Success);

            var sw = Stopwatch.StartNew();
            var getResult = _sessionManager.GetSession(createResult.Data);
            sw.Stop();

            Assert.True(getResult.Success);
            Assert.True(sw.ElapsedMilliseconds < 1,
                $"Session retrieval took {sw.ElapsedMilliseconds}ms, expected < 1ms");
        }

        [Fact]
        public void Performance_SessionDisposal_Under100ms()
        {
            EnsureSessionManager();
            if (_sessionManager == null) return;

            var createResult = _sessionManager.CreateSession();
            Assert.True(createResult.Success);

            var sw = Stopwatch.StartNew();
            var closeResult = _sessionManager.CloseSession(createResult.Data);
            sw.Stop();

            Assert.True(closeResult.Success);
            Assert.True(sw.ElapsedMilliseconds < 100,
                $"Session disposal took {sw.ElapsedMilliseconds}ms, expected < 100ms");
        }

        [Fact]
        public async Task Performance_20ConcurrentSessions_AllFunctional()
        {
            EnsureSessionManager();
            if (_sessionManager == null) return;

            var tasks = Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() => _sessionManager.CreateSession()))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            Assert.All(results, result => Assert.True(result.Success));

            foreach (var result in results)
            {
                var getResult = _sessionManager.GetSession(result.Data);
                Assert.True(getResult.Success);
            }
        }
    }
}
