// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace DwsimWorker.Exceptions
{
    /// <summary>
    /// Exception thrown when a calculation operation exceeds the specified timeout duration.
    /// </summary>
    public class CalculationTimeoutException : CalculationException
    {
        /// <summary>
        /// Gets the timeout duration that was exceeded.
        /// </summary>
        public TimeSpan Timeout { get; }

        /// <summary>
        /// Gets the elapsed time when the timeout occurred.
        /// </summary>
        public TimeSpan ElapsedTime { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationTimeoutException"/> class.
        /// </summary>
        public CalculationTimeoutException()
            : base("Calculation operation timed out.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationTimeoutException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public CalculationTimeoutException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationTimeoutException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public CalculationTimeoutException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationTimeoutException"/> class with timeout details.
        /// </summary>
        /// <param name="timeout">The timeout duration that was exceeded.</param>
        /// <param name="elapsedTime">The elapsed time when the timeout occurred.</param>
        /// <param name="unitOperationId">The ID of the unit operation that timed out.</param>
        public CalculationTimeoutException(TimeSpan timeout, TimeSpan elapsedTime, string unitOperationId = null)
            : base(
                $"Calculation timed out after {elapsedTime.TotalSeconds:F2} seconds (timeout: {timeout.TotalSeconds:F2} seconds).",
                unitOperationId,
                null)
        {
            Timeout = timeout;
            ElapsedTime = elapsedTime;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationTimeoutException"/> class with a custom message and timeout details.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="timeout">The timeout duration that was exceeded.</param>
        /// <param name="elapsedTime">The elapsed time when the timeout occurred.</param>
        /// <param name="unitOperationId">The ID of the unit operation that timed out.</param>
        public CalculationTimeoutException(string message, TimeSpan timeout, TimeSpan elapsedTime, string unitOperationId = null)
            : base(message, unitOperationId, null)
        {
            Timeout = timeout;
            ElapsedTime = elapsedTime;
        }

        /// <summary>
        /// Returns a string representation of the exception.
        /// </summary>
        /// <returns>A string containing exception details.</returns>
        public override string ToString()
        {
            var details = base.ToString();

            if (Timeout != default(TimeSpan))
                details += $"\nTimeout: {Timeout.TotalSeconds:F2} seconds";

            if (ElapsedTime != default(TimeSpan))
                details += $"\nElapsed Time: {ElapsedTime.TotalSeconds:F2} seconds";

            return details;
        }
    }
}
