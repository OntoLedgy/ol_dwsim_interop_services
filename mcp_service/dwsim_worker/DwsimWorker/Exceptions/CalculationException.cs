// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace DwsimWorker.Exceptions
{
    /// <summary>
    /// Exception thrown when a calculation operation fails.
    /// This is the base exception for all calculation-related errors.
    /// </summary>
    public class CalculationException : DwsimException
    {
        /// <summary>
        /// Gets the ID of the unit operation that caused the exception, if applicable.
        /// </summary>
        public string UnitOperationId { get; }

        /// <summary>
        /// Gets the ID of the stream that caused the exception, if applicable.
        /// </summary>
        public string StreamId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationException"/> class.
        /// </summary>
        public CalculationException()
            : base("A calculation error occurred.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public CalculationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public CalculationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationException"/> class with a specified error message
        /// and optional unit operation and stream identifiers.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="unitOperationId">The ID of the unit operation that caused the exception.</param>
        /// <param name="streamId">The ID of the stream that caused the exception.</param>
        public CalculationException(string message, string unitOperationId = null, string streamId = null)
            : base(message)
        {
            UnitOperationId = unitOperationId;
            StreamId = streamId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationException"/> class with a specified error message,
        /// inner exception, and optional unit operation and stream identifiers.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        /// <param name="unitOperationId">The ID of the unit operation that caused the exception.</param>
        /// <param name="streamId">The ID of the stream that caused the exception.</param>
        public CalculationException(string message, Exception innerException, string unitOperationId = null, string streamId = null)
            : base(message, innerException)
        {
            UnitOperationId = unitOperationId;
            StreamId = streamId;
        }

        /// <summary>
        /// Returns a string representation of the exception.
        /// </summary>
        /// <returns>A string containing exception details.</returns>
        public override string ToString()
        {
            var details = base.ToString();

            if (!string.IsNullOrWhiteSpace(UnitOperationId))
                details += $"\nUnit Operation ID: {UnitOperationId}";

            if (!string.IsNullOrWhiteSpace(StreamId))
                details += $"\nStream ID: {StreamId}";

            return details;
        }
    }
}
