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

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents a diagnostic message from the solver during calculation.
    /// This is an immutable record-like class capturing solver diagnostic information.
    /// </summary>
    public sealed class SolverMessage
    {
        /// <summary>
        /// Gets the severity level of the message.
        /// </summary>
        public SolverMessageLevel Level { get; }

        /// <summary>
        /// Gets the message text.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the source of the message (e.g., solver name, unit operation).
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets the timestamp when the message was generated.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SolverMessage"/> class.
        /// </summary>
        /// <param name="level">The severity level of the message.</param>
        /// <param name="message">The message text.</param>
        /// <param name="source">The source of the message.</param>
        /// <exception cref="ArgumentNullException">Thrown when message or source is null or whitespace.</exception>
        public SolverMessage(SolverMessageLevel level, string message, string source)
            : this(level, message, source, DateTime.UtcNow)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SolverMessage"/> class with a specific timestamp.
        /// </summary>
        /// <param name="level">The severity level of the message.</param>
        /// <param name="message">The message text.</param>
        /// <param name="source">The source of the message.</param>
        /// <param name="timestamp">The timestamp when the message was generated.</param>
        /// <exception cref="ArgumentNullException">Thrown when message or source is null or whitespace.</exception>
        public SolverMessage(SolverMessageLevel level, string message, string source, DateTime timestamp)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentNullException(nameof(message), "Message cannot be null or empty.");

            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentNullException(nameof(source), "Source cannot be null or empty.");

            Level = level;
            Message = message;
            Source = source;
            Timestamp = timestamp;
        }

        /// <summary>
        /// Returns a string representation of the solver message.
        /// </summary>
        /// <returns>A string containing the message details.</returns>
        public override string ToString()
        {
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss} UTC] [{Level}] {Source}: {Message}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current SolverMessage.
        /// </summary>
        /// <param name="obj">The object to compare with the current SolverMessage.</param>
        /// <returns>True if the specified object is equal to the current SolverMessage; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is SolverMessage other)
            {
                return Level == other.Level &&
                       Message == other.Message &&
                       Source == other.Source &&
                       Timestamp == other.Timestamp;
            }

            return false;
        }

        /// <summary>
        /// Returns a hash code for the current SolverMessage.
        /// </summary>
        /// <returns>A hash code for the current SolverMessage.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Level.GetHashCode();
                hash = hash * 23 + (Message?.GetHashCode() ?? 0);
                hash = hash * 23 + (Source?.GetHashCode() ?? 0);
                hash = hash * 23 + Timestamp.GetHashCode();
                return hash;
            }
        }
    }
}
