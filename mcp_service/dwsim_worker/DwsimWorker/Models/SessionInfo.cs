// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using DwsimWorker.Engine;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents metadata for an active simulation session.
    /// This is an immutable record-like class capturing session details without exposing the context.
    /// </summary>
    public sealed class SessionInfo
    {
        /// <summary>
        /// Gets the unique identifier for the session.
        /// </summary>
        public Guid SessionId { get; }

        /// <summary>
        /// Gets the timestamp when the session was created.
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Gets the optional flowsheet name associated with the session.
        /// </summary>
        public string FlowsheetName { get; }

        /// <summary>
        /// Gets a value indicating whether the session has been initialized.
        /// </summary>
        public bool IsInitialized { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionInfo"/> class.
        /// </summary>
        /// <param name="sessionId">The unique identifier for the session.</param>
        /// <param name="createdAt">The timestamp when the session was created.</param>
        /// <param name="flowsheetName">The optional flowsheet name associated with the session.</param>
        /// <param name="isInitialized">Whether the session has been initialized.</param>
        public SessionInfo(Guid sessionId, DateTime createdAt, string flowsheetName, bool isInitialized)
        {
            SessionId = sessionId;
            CreatedAt = createdAt;
            FlowsheetName = flowsheetName;
            IsInitialized = isInitialized;
        }

        /// <summary>
        /// Creates a SessionInfo instance from a SessionEntry.
        /// </summary>
        /// <param name="entry">The session entry to convert.</param>
        /// <returns>A SessionInfo instance containing the session metadata.</returns>
        /// <exception cref="ArgumentNullException">Thrown when entry is null.</exception>
        internal static SessionInfo FromEntry(SessionManager.SessionEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            return new SessionInfo(entry.SessionId, entry.CreatedAt, entry.FlowsheetName, entry.IsInitialized);
        }
    }
}
