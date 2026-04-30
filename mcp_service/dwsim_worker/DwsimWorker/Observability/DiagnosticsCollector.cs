// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DwsimWorker.Engine;
using DwsimWorker.Models;

namespace DwsimWorker.Observability
{
    /// <summary>
    /// Collects diagnostics for active sessions and flowsheets, plus recent error tracking.
    /// </summary>
    public static class DiagnosticsCollector
    {
        private const int MaxErrorHistory = 5;
        private static readonly ConcurrentDictionary<Guid, Queue<ErrorRecordDto>> ErrorHistory
            = new ConcurrentDictionary<Guid, Queue<ErrorRecordDto>>();
        private static readonly ConcurrentDictionary<Guid, object> SessionLocks
            = new ConcurrentDictionary<Guid, object>();

        public static SessionDiagnosticsDto GetSessionDiagnostics(SessionManager sessionManager, Guid sessionId)
        {
            if (sessionManager == null || sessionId == Guid.Empty)
            {
                return null;
            }

            var sessionInfo = TryGetSessionInfo(sessionManager, sessionId);
            if (sessionInfo == null)
            {
                return null;
            }

            var result = new SessionDiagnosticsDto
            {
                SessionId = sessionId,
                CreatedAtUtc = sessionInfo.CreatedAt,
                FlowsheetName = sessionInfo.FlowsheetName,
                IsInitialized = sessionInfo.IsInitialized,
                CapturedAtUtc = DateTime.UtcNow
            };

            try
            {
                var contextResult = sessionManager.GetSession(sessionId);
                if (!contextResult.Success || contextResult.Data == null)
                {
                    PopulateMemoryMetrics(result);
                    return result;
                }

                PopulateObjectCounts(result, contextResult.Data);
            }
            catch (ObjectDisposedException)
            {
                PopulateMemoryMetrics(result);
                return result;
            }
            catch (InvalidOperationException)
            {
                PopulateMemoryMetrics(result);
                return result;
            }

            PopulateMemoryMetrics(result);
            return result;
        }

        public static SessionDiagnosticsDto GetSessionDiagnostics(SessionManager sessionManager, string sessionId)
        {
            if (!Guid.TryParse(sessionId, out var parsed))
            {
                return null;
            }

            return GetSessionDiagnostics(sessionManager, parsed);
        }

        public static FlowsheetDiagnosticsDto GetFlowsheetDiagnostics(SessionManager sessionManager, Guid sessionId)
        {
            if (sessionManager == null || sessionId == Guid.Empty)
            {
                return null;
            }

            var sessionInfo = TryGetSessionInfo(sessionManager, sessionId);
            if (sessionInfo == null)
            {
                return null;
            }

            var result = new FlowsheetDiagnosticsDto
            {
                SessionId = sessionId,
                FlowsheetName = sessionInfo.FlowsheetName,
                IsInitialized = sessionInfo.IsInitialized,
                Streams = new List<string>(),
                Units = new List<string>(),
                Compounds = new List<string>()
            };

            try
            {
                var contextResult = sessionManager.GetSession(sessionId);
                if (!contextResult.Success || contextResult.Data == null)
                {
                    return result;
                }

                PopulateFlowsheetDetails(result, contextResult.Data);
            }
            catch (ObjectDisposedException)
            {
                return result;
            }
            catch (InvalidOperationException)
            {
                return result;
            }

            return result;
        }

        public static FlowsheetDiagnosticsDto GetFlowsheetDiagnostics(SessionManager sessionManager, string sessionId)
        {
            if (!Guid.TryParse(sessionId, out var parsed))
            {
                return null;
            }

            return GetFlowsheetDiagnostics(sessionManager, parsed);
        }

        public static void RecordError(Guid sessionId, string error, string context)
        {
            if (sessionId == Guid.Empty || string.IsNullOrWhiteSpace(error))
            {
                return;
            }

            var entry = new ErrorRecordDto
            {
                SessionId = sessionId,
                ErrorMessage = error,
                Context = context,
                TimestampUtc = DateTime.UtcNow
            };

            var sessionLock = SessionLocks.GetOrAdd(sessionId, _ => new object());
            lock (sessionLock)
            {
                var queue = ErrorHistory.GetOrAdd(sessionId, _ => new Queue<ErrorRecordDto>());
                queue.Enqueue(entry);
                while (queue.Count > MaxErrorHistory)
                {
                    queue.Dequeue();
                }
            }
        }

        public static void RecordError(string sessionId, string error, string context)
        {
            if (!Guid.TryParse(sessionId, out var parsed))
            {
                return;
            }

            RecordError(parsed, error, context);
        }

        public static IReadOnlyList<ErrorRecordDto> GetRecentErrors(Guid sessionId, int count = MaxErrorHistory)
        {
            if (sessionId == Guid.Empty || count <= 0)
            {
                return Array.Empty<ErrorRecordDto>();
            }

            if (!ErrorHistory.TryGetValue(sessionId, out var queue))
            {
                return Array.Empty<ErrorRecordDto>();
            }

            var sessionLock = SessionLocks.GetOrAdd(sessionId, _ => new object());
            lock (sessionLock)
            {
                if (queue.Count == 0)
                {
                    return Array.Empty<ErrorRecordDto>();
                }

                var items = queue.ToList();
                if (items.Count > count)
                {
                    items = items.Skip(items.Count - count).ToList();
                }

                items.Reverse();
                return items;
            }
        }

        public static IReadOnlyList<ErrorRecordDto> GetRecentErrors(string sessionId, int count = MaxErrorHistory)
        {
            if (!Guid.TryParse(sessionId, out var parsed))
            {
                return Array.Empty<ErrorRecordDto>();
            }

            return GetRecentErrors(parsed, count);
        }

        private static SessionInfo TryGetSessionInfo(SessionManager sessionManager, Guid sessionId)
        {
            var sessions = sessionManager.GetAllSessions();
            if (sessions == null || sessions.Count == 0)
            {
                return null;
            }

            return sessions.FirstOrDefault(info => info.SessionId == sessionId);
        }

        private static void PopulateObjectCounts(SessionDiagnosticsDto result, FlowsheetContext context)
        {
            if (result == null || context == null)
            {
                return;
            }

            if (!context.IsInitialized)
            {
                return;
            }

            var streamCount = SafeCount(context.GetStreamIds);
            var unitCount = SafeCount(context.GetUnitIds);
            var compoundCount = SafeCount(context.GetCompounds);
            var connectionCount = SafeCount(context.GetConnections);

            result.StreamCount = streamCount;
            result.UnitCount = unitCount;
            result.CompoundCount = compoundCount;
            result.ConnectionCount = connectionCount;
            result.ObjectCount = streamCount + unitCount + compoundCount + connectionCount;
        }

        private static void PopulateFlowsheetDetails(FlowsheetDiagnosticsDto result, FlowsheetContext context)
        {
            if (result == null || context == null)
            {
                return;
            }

            if (!context.IsInitialized)
            {
                return;
            }

            result.Streams = SafeList(context.GetStreamIds);
            result.Units = SafeList(context.GetUnitIds);
            result.Compounds = SafeList(context.GetCompounds);
            result.ConnectionCount = SafeCount(context.GetConnections);
            result.StreamCount = result.Streams.Count;
            result.UnitCount = result.Units.Count;
            result.CompoundCount = result.Compounds.Count;
            result.ObjectCount = result.StreamCount + result.UnitCount + result.CompoundCount + result.ConnectionCount;
            result.PropertyPackageName = SafeValue(context.GetPropertyPackageName);
        }

        private static void PopulateMemoryMetrics(SessionDiagnosticsDto result)
        {
            if (result == null)
            {
                return;
            }

            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    result.WorkingSetBytes = process.WorkingSet64;
                    result.PrivateMemoryBytes = process.PrivateMemorySize64;
                }
            }
            catch
            {
                result.WorkingSetBytes = 0;
                result.PrivateMemoryBytes = 0;
            }
        }

        private static int SafeCount<T>(Func<IReadOnlyCollection<T>> getter)
        {
            try
            {
                return getter()?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private static int SafeCount<T>(Func<IReadOnlyList<T>> getter)
        {
            try
            {
                return getter()?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private static List<string> SafeList(Func<IReadOnlyCollection<string>> getter)
        {
            try
            {
                return getter()?.ToList() ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static List<string> SafeList(Func<IReadOnlyList<string>> getter)
        {
            try
            {
                return getter()?.ToList() ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static string SafeValue(Func<string> getter)
        {
            try
            {
                return getter();
            }
            catch
            {
                return null;
            }
        }
    }

    public sealed class SessionDiagnosticsDto
    {
        public Guid SessionId { get; set; }
        public DateTime? CreatedAtUtc { get; set; }
        public string FlowsheetName { get; set; }
        public bool IsInitialized { get; set; }
        public int StreamCount { get; set; }
        public int UnitCount { get; set; }
        public int CompoundCount { get; set; }
        public int ConnectionCount { get; set; }
        public int ObjectCount { get; set; }
        public long WorkingSetBytes { get; set; }
        public long PrivateMemoryBytes { get; set; }
        public DateTime CapturedAtUtc { get; set; }
    }

    public sealed class FlowsheetDiagnosticsDto
    {
        public Guid SessionId { get; set; }
        public string FlowsheetName { get; set; }
        public bool IsInitialized { get; set; }
        public int StreamCount { get; set; }
        public int UnitCount { get; set; }
        public int CompoundCount { get; set; }
        public int ConnectionCount { get; set; }
        public int ObjectCount { get; set; }
        public string PropertyPackageName { get; set; }
        public List<string> Streams { get; set; }
        public List<string> Units { get; set; }
        public List<string> Compounds { get; set; }
    }

    public sealed class ErrorRecordDto
    {
        public Guid SessionId { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string ErrorMessage { get; set; }
        public string Context { get; set; }
    }
}
