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
using DwsimWorker.Models;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Manages DWSIM simulation sessions with lifecycle control and isolation.
    /// </summary>
    /// <remarks>
    /// Thread-safe for registry operations (create/retrieve/close) via internal locking.
    /// FlowsheetContext instances are not thread-safe; callers must serialize operations per session.
    /// Dispose is idempotent and will close all active sessions.
    /// </remarks>
    public sealed class SessionManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly FlowsheetContextConfig _defaultConfig;
        private readonly Dictionary<Guid, SessionEntry> _sessions;
        private readonly object _lock;
        private bool _disposed;

        internal sealed class SessionEntry
        {
            public Guid SessionId { get; }
            public FlowsheetContext Context { get; }
            public DateTime CreatedAt { get; }
            public string FlowsheetName { get; }
            public bool IsInitialized => Context?.IsInitialized ?? false;

            public SessionEntry(Guid sessionId, FlowsheetContext context, DateTime createdAt, string flowsheetName)
            {
                SessionId = sessionId;
                Context = context;
                CreatedAt = createdAt;
                FlowsheetName = flowsheetName;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionManager"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for session management logging.</param>
        /// <param name="defaultConfig">The default configuration for session flowsheets.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger or defaultConfig is null.</exception>
        public SessionManager(ILogger logger, FlowsheetContextConfig defaultConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _defaultConfig = defaultConfig ?? throw new ArgumentNullException(nameof(defaultConfig));
            _sessions = new Dictionary<Guid, SessionEntry>();
            _lock = new object();
            _disposed = false;

            _logger.Information("SessionManager created with default flowsheet name: {FlowsheetName}", _defaultConfig.FlowsheetName);
        }

        /// <summary>
        /// Creates a new session with optional flowsheet name and configuration override.
        /// </summary>
        /// <param name="flowsheetName">Optional flowsheet name for identification and logging.</param>
        /// <param name="config">Optional configuration override for this session.</param>
        /// <returns>An OperationResult containing the session ID on success.</returns>
        /// <remarks>
        /// Returns a failure result if initialization fails or if the manager has been disposed.
        /// </remarks>
        public OperationResult<Guid> CreateSession(string flowsheetName = null, FlowsheetContextConfig config = null)
        {
            if (_disposed)
                return OperationResult<Guid>.FailureResult("SessionManager is disposed.");

            var sessionId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;
            var resolvedConfig = ResolveConfig(flowsheetName, config);
            FlowsheetContext context = null;

            try
            {
                context = new FlowsheetContext(_logger, resolvedConfig);
                context.Initialize();

                var entry = new SessionEntry(sessionId, context, createdAt, resolvedConfig.FlowsheetName);

                lock (_lock)
                {
                    _sessions.Add(sessionId, entry);
                }

                _logger.Information("Session created: {SessionId} {FlowsheetName}", sessionId, resolvedConfig.FlowsheetName);
                return OperationResult<Guid>.SuccessResult(sessionId, "Session created successfully.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create session {SessionId}", sessionId);

                if (context != null)
                {
                    try
                    {
                        context.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        _logger.Error(disposeEx, "Failed to dispose failed session context {SessionId}", sessionId);
                    }
                }

                return OperationResult<Guid>.FailureResult($"Failed to initialize session: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Retrieves an existing session by its ID.
        /// </summary>
        /// <param name="sessionId">The session ID to retrieve.</param>
        /// <returns>An OperationResult containing the FlowsheetContext on success.</returns>
        /// <remarks>
        /// Returns a failure result if the session is not found or if the manager has been disposed.
        /// </remarks>
        public OperationResult<FlowsheetContext> GetSession(Guid sessionId)
        {
            if (_disposed)
                return OperationResult<FlowsheetContext>.FailureResult("SessionManager is disposed.");

            _logger.Debug("Retrieving session: {SessionId}", sessionId);

            lock (_lock)
            {
                if (_sessions.TryGetValue(sessionId, out SessionEntry entry))
                {
                    return OperationResult<FlowsheetContext>.SuccessResult(entry.Context, "Session retrieved successfully.");
                }
            }

            _logger.Warning("Session not found: {SessionId}", sessionId);
            return OperationResult<FlowsheetContext>.FailureResult($"Session {sessionId} not found.");
        }

        /// <summary>
        /// Checks whether a session exists in the registry.
        /// </summary>
        /// <param name="sessionId">The session ID to check.</param>
        /// <returns>True if the session exists; otherwise, false.</returns>
        public bool SessionExists(Guid sessionId)
        {
            if (_disposed)
                return false;

            lock (_lock)
            {
                return _sessions.ContainsKey(sessionId);
            }
        }

        /// <summary>
        /// Gets a snapshot of all active sessions.
        /// </summary>
        /// <returns>A read-only list of SessionInfo objects.</returns>
        /// <remarks>
        /// Returns an empty list if the manager has been disposed.
        /// </remarks>
        public IReadOnlyList<SessionInfo> GetAllSessions()
        {
            if (_disposed)
                return new List<SessionInfo>().AsReadOnly();

            List<SessionEntry> entries;
            lock (_lock)
            {
                entries = _sessions.Values.ToList();
            }

            var sessions = entries.Select(SessionInfo.FromEntry).ToList().AsReadOnly();
            return sessions;
        }

        /// <summary>
        /// Closes and disposes a session by its ID.
        /// </summary>
        /// <param name="sessionId">The session ID to close.</param>
        /// <returns>An OperationResult indicating success or failure.</returns>
        /// <remarks>
        /// Closing an already-closed session returns a successful result with a false data value.
        /// Returns a failure result if the manager has been disposed.
        /// </remarks>
        public OperationResult<bool> CloseSession(Guid sessionId)
        {
            if (_disposed)
                return OperationResult<bool>.FailureResult("SessionManager is disposed.");

            SessionEntry entry;
            lock (_lock)
            {
                if (!_sessions.TryGetValue(sessionId, out entry))
                {
                    _logger.Warning("Session already closed or not found: {SessionId}", sessionId);
                    return OperationResult<bool>.SuccessResult(false, "Session already closed.");
                }

                _sessions.Remove(sessionId);
            }

            try
            {
                entry.Context.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error disposing session context {SessionId}", sessionId);
            }

            _logger.Information("Session closed: {SessionId}", sessionId);
            return OperationResult<bool>.SuccessResult(true, "Session closed successfully.");
        }

        /// <summary>
        /// Saves a session flowsheet case to disk.
        /// </summary>
        /// <param name="sessionId">The session ID to save.</param>
        /// <param name="filePath">Destination file path.</param>
        /// <returns>An OperationResult indicating success or failure.</returns>
        public OperationResult<bool> SaveCase(Guid sessionId, string filePath)
        {
            if (_disposed)
                return OperationResult<bool>.FailureResult("SessionManager is disposed.");

            if (string.IsNullOrWhiteSpace(filePath))
                return OperationResult<bool>.FailureResult("File path cannot be null or empty.");

            var sessionResult = GetSession(sessionId);
            if (!sessionResult.Success || sessionResult.Data == null)
                return OperationResult<bool>.FailureResult(sessionResult.Message ?? "Session not found.");

            try
            {
                sessionResult.Data.SaveCase(filePath);
                _logger.Information("Session case saved: {SessionId} {FilePath}", sessionId, filePath);
                return OperationResult<bool>.SuccessResult(true, "Case saved successfully.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save case for session {SessionId}", sessionId);
                return OperationResult<bool>.FailureResult($"Failed to save case: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads a flowsheet case from disk into a session.
        /// </summary>
        /// <param name="sessionId">The session ID to load into.</param>
        /// <param name="filePath">Source file path.</param>
        /// <returns>An OperationResult indicating success or failure.</returns>
        public OperationResult<bool> LoadCase(Guid sessionId, string filePath)
        {
            if (_disposed)
                return OperationResult<bool>.FailureResult("SessionManager is disposed.");

            if (string.IsNullOrWhiteSpace(filePath))
                return OperationResult<bool>.FailureResult("File path cannot be null or empty.");

            var sessionResult = GetSession(sessionId);
            if (!sessionResult.Success || sessionResult.Data == null)
                return OperationResult<bool>.FailureResult(sessionResult.Message ?? "Session not found.");

            try
            {
                sessionResult.Data.LoadCase(filePath);
                _logger.Information("Session case loaded: {SessionId} {FilePath}", sessionId, filePath);
                return OperationResult<bool>.SuccessResult(true, "Case loaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load case for session {SessionId}", sessionId);
                return OperationResult<bool>.FailureResult($"Failed to load case: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Closes all active sessions.
        /// </summary>
        /// <remarks>
        /// Errors disposing individual sessions are logged and do not stop cleanup of others.
        /// </remarks>
        public void CloseAllSessions()
        {
            if (_disposed)
                return;

            List<Guid> sessionIds;
            lock (_lock)
            {
                sessionIds = _sessions.Keys.ToList();
            }

            foreach (var sessionId in sessionIds)
            {
                try
                {
                    CloseSession(sessionId);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error closing session {SessionId}", sessionId);
                }
            }
        }

        /// <summary>
        /// Disposes the session manager and releases all resources.
        /// </summary>
        /// <remarks>
        /// Dispose is idempotent and closes all active sessions.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
                return;

            _logger.Information("Disposing SessionManager");

            try
            {
                CloseAllSessions();
            }
            finally
            {
                _disposed = true;
            }
        }

        private FlowsheetContextConfig ResolveConfig(string flowsheetName, FlowsheetContextConfig config)
        {
            var baseConfig = config ?? _defaultConfig;
            if (string.IsNullOrWhiteSpace(flowsheetName))
                return baseConfig;

            return new FlowsheetContextConfigBuilder()
                .WithAssemblyConfig(baseConfig.AssemblyConfig)
                .WithValidation(baseConfig.ValidateAfterInit)
                .WithFlowsheetName(flowsheetName)
                .Build();
        }
    }
}
