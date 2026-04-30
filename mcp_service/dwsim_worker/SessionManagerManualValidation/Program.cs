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
using System.Collections.Generic;
using System.Linq;
using Serilog;
using DwsimWorker.Engine;
using DwsimWorker.Utilities;

namespace SessionManagerManualValidation
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                var dwsimPath = ResolveDwsimPath(args);
                if (string.IsNullOrWhiteSpace(dwsimPath))
                {
                    return 1;
                }

                var config = new FlowsheetContextConfigBuilder()
                    .WithAssemblyPath(dwsimPath)
                    .WithValidation(false)
                    .WithFlowsheetName("Manual Validation")
                    .Build();

                var sessionManager = new SessionManager(Log.Logger, config);
                var beforeMemory = GC.GetTotalMemory(true);

                var sessionIds = CreateSessions(sessionManager, 5);
                ValidateIsolation(sessionManager, sessionIds);

                foreach (var sessionId in sessionIds)
                {
                    var closeResult = sessionManager.CloseSession(sessionId);
                    Log.Information("Closed session {SessionId}: {Success} ({Message})",
                        sessionId, closeResult.Success, closeResult.Message);
                }

                var remaining = sessionManager.GetAllSessions();
                Log.Information("Remaining sessions after close: {Count}", remaining.Count);

                sessionManager.Dispose();

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var afterMemory = GC.GetTotalMemory(true);
                Log.Information("Memory delta after cleanup: {Bytes} bytes", afterMemory - beforeMemory);

                Log.Information("Manual validation completed.");
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Manual validation failed.");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static string ResolveDwsimPath(string[] args)
        {
            if (args.Length > 0)
            {
                var provided = args[0];
                if (PathResolver.ValidatePath(provided))
                {
                    Log.Information("Using provided DWSIM path: {Path}", provided);
                    return provided;
                }

                Log.Error("Provided DWSIM path is invalid: {Path}", provided);
                return null;
            }

            try
            {
                return PathResolver.ResolveDwsimPath();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to resolve DWSIM path. Set DWSIM_PATH or pass path as the first argument.");
                return null;
            }
        }

        private static List<Guid> CreateSessions(SessionManager sessionManager, int count)
        {
            var sessionIds = new List<Guid>(count);

            for (int i = 0; i < count; i++)
            {
                var result = sessionManager.CreateSession(flowsheetName: $"Manual-{i + 1}");
                if (!result.Success)
                {
                    throw new InvalidOperationException($"Failed to create session {i + 1}: {result.Message}");
                }

                Log.Information("Created session {Index}: {SessionId}", i + 1, result.Data);
                sessionIds.Add(result.Data);
            }

            return sessionIds;
        }

        private static void ValidateIsolation(SessionManager sessionManager, IReadOnlyList<Guid> sessionIds)
        {
            var compounds = new[] { "Water", "Methane", "Ethanol", "Propane", "Nitrogen" };

            for (int i = 0; i < sessionIds.Count; i++)
            {
                var sessionId = sessionIds[i];
                var contextResult = sessionManager.GetSession(sessionId);
                if (!contextResult.Success)
                {
                    throw new InvalidOperationException($"Failed to retrieve session {sessionId}: {contextResult.Message}");
                }

                var compound = compounds[i % compounds.Length];
                contextResult.Data.AddCompound(compound);
                Log.Information("Session {SessionId} added compound {Compound}", sessionId, compound);
            }

            for (int i = 0; i < sessionIds.Count; i++)
            {
                var sessionId = sessionIds[i];
                var contextResult = sessionManager.GetSession(sessionId);
                var compound = compounds[i % compounds.Length];
                var sessionCompounds = contextResult.Data.GetCompounds();

                if (!sessionCompounds.Contains(compound))
                {
                    throw new InvalidOperationException($"Isolation check failed for {sessionId}: missing {compound}");
                }

                var unexpected = compounds.Where(c => c != compound).Any(c => sessionCompounds.Contains(c));
                if (unexpected)
                {
                    throw new InvalidOperationException($"Isolation check failed for {sessionId}: unexpected compound found.");
                }

                Log.Information("Session {SessionId} isolation verified with compound {Compound}.", sessionId, compound);
            }
        }
    }
}
