// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace DwsimWorker.Exceptions
{
    /// <summary>
    /// Exception thrown when a requested material stream is not found in the flowsheet.
    /// </summary>
    public sealed class StreamNotFoundException : DwsimException
    {
        /// <summary>
        /// Gets the ID of the stream that was not found.
        /// </summary>
        public string StreamId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamNotFoundException"/> class.
        /// </summary>
        /// <param name="streamId">The ID of the stream that was not found.</param>
        public StreamNotFoundException(string streamId)
            : base($"Stream '{streamId}' not found in flowsheet. The stream may not have been created or may have been removed.")
        {
            StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamNotFoundException"/> class with an inner exception.
        /// </summary>
        /// <param name="streamId">The ID of the stream that was not found.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public StreamNotFoundException(string streamId, Exception innerException)
            : base($"Stream '{streamId}' not found in flowsheet. The stream may not have been created or may have been removed.", innerException)
        {
            StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
        }

        /// <summary>
        /// Returns a string representation of the exception including stream ID.
        /// </summary>
        /// <returns>A string containing the exception details.</returns>
        public override string ToString()
        {
            return $"{base.ToString()}\nStreamId: {StreamId}";
        }
    }
}
