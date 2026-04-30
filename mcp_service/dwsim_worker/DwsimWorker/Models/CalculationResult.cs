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

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents the complete result of a calculation operation.
    /// This is an immutable model aggregating all calculation outputs following the result pattern.
    /// </summary>
    public sealed class CalculationResult
    {
        /// <summary>
        /// Gets a value indicating whether the calculation succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the convergence status of the solver.
        /// </summary>
        public ConvergenceStatus ConvergenceStatus { get; }

        /// <summary>
        /// Gets a message describing the result.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the timing information for the calculation.
        /// </summary>
        public CalculationTiming Timing { get; }

        /// <summary>
        /// Gets the calculated stream results.
        /// </summary>
        public IReadOnlyList<StreamResult> StreamResults { get; }

        /// <summary>
        /// Gets the mass balance validation result.
        /// </summary>
        public MassBalanceResult MassBalance { get; }

        /// <summary>
        /// Gets the solver diagnostic messages.
        /// </summary>
        public IReadOnlyList<SolverMessage> Messages { get; }

        /// <summary>
        /// Gets the exception that caused the failure, if any.
        /// Null if the calculation succeeded or failed without an exception.
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationResult"/> class.
        /// Private constructor - use static factory methods instead.
        /// </summary>
        private CalculationResult(
            bool success,
            ConvergenceStatus convergenceStatus,
            string message,
            CalculationTiming timing,
            IReadOnlyList<StreamResult> streamResults,
            MassBalanceResult massBalance,
            IReadOnlyList<SolverMessage> messages,
            Exception error)
        {
            Success = success;
            ConvergenceStatus = convergenceStatus;
            Message = message ?? string.Empty;
            Timing = timing;
            StreamResults = streamResults ?? new List<StreamResult>();
            MassBalance = massBalance;
            Messages = messages ?? new List<SolverMessage>();
            Error = error;
        }

        /// <summary>
        /// Creates a successful CalculationResult.
        /// </summary>
        /// <param name="convergenceStatus">The convergence status.</param>
        /// <param name="timing">The timing information.</param>
        /// <param name="streamResults">The calculated stream results.</param>
        /// <param name="massBalance">The mass balance validation result.</param>
        /// <param name="messages">Optional solver diagnostic messages.</param>
        /// <param name="message">Optional success message.</param>
        /// <returns>A CalculationResult indicating success.</returns>
        public static CalculationResult SuccessResult(
            ConvergenceStatus convergenceStatus,
            CalculationTiming timing,
            IReadOnlyList<StreamResult> streamResults,
            MassBalanceResult massBalance,
            IReadOnlyList<SolverMessage> messages = null,
            string message = null)
        {
            if (convergenceStatus == null)
                throw new ArgumentNullException(nameof(convergenceStatus));

            if (timing == null)
                throw new ArgumentNullException(nameof(timing));

            return new CalculationResult(
                success: true,
                convergenceStatus: convergenceStatus,
                message: message ?? "Calculation completed successfully.",
                timing: timing,
                streamResults: streamResults,
                massBalance: massBalance,
                messages: messages,
                error: null);
        }

        /// <summary>
        /// Creates a failed CalculationResult.
        /// </summary>
        /// <param name="message">The error message describing the failure.</param>
        /// <param name="error">The exception that caused the failure.</param>
        /// <param name="convergenceStatus">Optional convergence status (defaults to Error state).</param>
        /// <param name="timing">Optional timing information.</param>
        /// <param name="messages">Optional solver diagnostic messages.</param>
        /// <returns>A CalculationResult indicating failure.</returns>
        public static CalculationResult FailureResult(
            string message,
            Exception error = null,
            ConvergenceStatus convergenceStatus = null,
            CalculationTiming timing = null,
            IReadOnlyList<SolverMessage> messages = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                message = "Calculation failed.";

            // Default to Error convergence state if not provided
            if (convergenceStatus == null)
            {
                convergenceStatus = ConvergenceStatus.Error(message);
            }

            return new CalculationResult(
                success: false,
                convergenceStatus: convergenceStatus,
                message: message,
                timing: timing,
                streamResults: null,
                massBalance: null,
                messages: messages,
                error: error);
        }

        /// <summary>
        /// Creates a CalculationResult for a not converged scenario.
        /// </summary>
        /// <param name="convergenceStatus">The convergence status indicating non-convergence.</param>
        /// <param name="timing">The timing information.</param>
        /// <param name="messages">Optional solver diagnostic messages.</param>
        /// <param name="message">Optional message.</param>
        /// <returns>A CalculationResult indicating non-convergence.</returns>
        public static CalculationResult NotConvergedResult(
            ConvergenceStatus convergenceStatus,
            CalculationTiming timing,
            IReadOnlyList<SolverMessage> messages = null,
            string message = null)
        {
            if (convergenceStatus == null)
                throw new ArgumentNullException(nameof(convergenceStatus));

            if (timing == null)
                throw new ArgumentNullException(nameof(timing));

            return new CalculationResult(
                success: false,
                convergenceStatus: convergenceStatus,
                message: message ?? "Calculation did not converge.",
                timing: timing,
                streamResults: null,
                massBalance: null,
                messages: messages,
                error: null);
        }

        /// <summary>
        /// Gets the number of stream results.
        /// </summary>
        public int StreamCount => StreamResults?.Count ?? 0;

        /// <summary>
        /// Gets the number of diagnostic messages.
        /// </summary>
        public int MessageCount => Messages?.Count ?? 0;

        /// <summary>
        /// Gets the number of error-level messages.
        /// </summary>
        public int ErrorMessageCount => Messages?.Count(m => m.Level == SolverMessageLevel.Error) ?? 0;

        /// <summary>
        /// Gets the number of warning-level messages.
        /// </summary>
        public int WarningMessageCount => Messages?.Count(m => m.Level == SolverMessageLevel.Warning) ?? 0;

        /// <summary>
        /// Checks if the result has errors (either failed or has error messages).
        /// </summary>
        public bool HasErrors => !Success || ErrorMessageCount > 0;

        /// <summary>
        /// Checks if the result has warnings.
        /// </summary>
        public bool HasWarnings => WarningMessageCount > 0;

        /// <summary>
        /// Gets a specific stream result by ID.
        /// </summary>
        /// <param name="streamId">The ID of the stream.</param>
        /// <returns>The StreamResult if found; otherwise, null.</returns>
        public StreamResult GetStream(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId) || StreamResults == null)
                return null;

            return StreamResults.FirstOrDefault(s => s.StreamId == streamId);
        }

        /// <summary>
        /// Returns a string representation of the calculation result.
        /// </summary>
        /// <returns>A string containing the result summary.</returns>
        public override string ToString()
        {
            var status = Success ? "✓ Success" : "✗ Failed";
            var convergence = ConvergenceStatus != null ? $", {ConvergenceStatus.State}" : "";
            var time = Timing != null ? $", {Timing.TotalMilliseconds:F0}ms" : "";
            var streams = StreamCount > 0 ? $", {StreamCount} stream(s)" : "";
            var massBalanceStatus = MassBalance != null ? $", Mass Balance: {(MassBalance.IsValid ? "Valid" : "Invalid")}" : "";
            var messagesSummary = MessageCount > 0 ? $", {MessageCount} message(s) ({ErrorMessageCount} errors, {WarningMessageCount} warnings)" : "";

            return $"{status} Calculation{convergence}{time}{streams}{massBalanceStatus}{messagesSummary}: {Message}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current CalculationResult.
        /// </summary>
        /// <param name="obj">The object to compare with the current CalculationResult.</param>
        /// <returns>True if the specified object is equal to the current CalculationResult; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is CalculationResult other)
            {
                return Success == other.Success &&
                       Message == other.Message &&
                       StreamCount == other.StreamCount &&
                       MessageCount == other.MessageCount;
            }

            return false;
        }

        /// <summary>
        /// Returns a hash code for the current CalculationResult.
        /// </summary>
        /// <returns>A hash code for the current CalculationResult.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Success.GetHashCode();
                hash = hash * 23 + (Message?.GetHashCode() ?? 0);
                hash = hash * 23 + StreamCount.GetHashCode();
                hash = hash * 23 + MessageCount.GetHashCode();
                return hash;
            }
        }
    }
}
