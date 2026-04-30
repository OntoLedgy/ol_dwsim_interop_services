// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents the convergence status of a solver calculation with detailed diagnostics.
    /// </summary>
    public sealed class ConvergenceStatus
    {
        /// <summary>
        /// Gets the convergence state.
        /// </summary>
        public ConvergenceState State { get; }

        /// <summary>
        /// Gets the status or error message associated with convergence.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the number of iterations performed during calculation.
        /// Null if not available or not started.
        /// </summary>
        public int? Iterations { get; }

        /// <summary>
        /// Gets the final residual error from the solver.
        /// Null if not available or not started.
        /// </summary>
        public double? ResidualError { get; }

        /// <summary>
        /// Gets a dictionary of convergence status for individual unit operations.
        /// Key is the unit operation ID, value indicates whether that unit converged.
        /// </summary>
        public IReadOnlyDictionary<string, bool> UnitConvergence { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvergenceStatus"/> class.
        /// </summary>
        /// <param name="state">The convergence state.</param>
        /// <param name="message">Optional status or error message.</param>
        /// <param name="iterations">Optional number of iterations performed.</param>
        /// <param name="residualError">Optional final residual error.</param>
        /// <param name="unitConvergence">Optional per-unit convergence status.</param>
        public ConvergenceStatus(
            ConvergenceState state,
            string message = null,
            int? iterations = null,
            double? residualError = null,
            IReadOnlyDictionary<string, bool> unitConvergence = null)
        {
            State = state;
            Message = message ?? string.Empty;
            Iterations = iterations;
            ResidualError = residualError;
            UnitConvergence = unitConvergence ?? new Dictionary<string, bool>();
        }

        /// <summary>
        /// Creates a ConvergenceStatus indicating calculation has not started.
        /// </summary>
        /// <returns>A new ConvergenceStatus with NotStarted state.</returns>
        public static ConvergenceStatus NotStarted()
        {
            return new ConvergenceStatus(ConvergenceState.NotStarted, "Calculation not started");
        }

        /// <summary>
        /// Creates a ConvergenceStatus indicating calculation is in progress.
        /// </summary>
        /// <param name="message">Optional progress message.</param>
        /// <returns>A new ConvergenceStatus with InProgress state.</returns>
        public static ConvergenceStatus InProgress(string message = null)
        {
            return new ConvergenceStatus(ConvergenceState.InProgress, message ?? "Calculation in progress");
        }

        /// <summary>
        /// Creates a ConvergenceStatus indicating successful convergence.
        /// </summary>
        /// <param name="iterations">Number of iterations performed.</param>
        /// <param name="residualError">Final residual error.</param>
        /// <param name="unitConvergence">Optional per-unit convergence status.</param>
        /// <returns>A new ConvergenceStatus with Converged state.</returns>
        public static ConvergenceStatus Converged(
            int iterations,
            double residualError,
            IReadOnlyDictionary<string, bool> unitConvergence = null)
        {
            return new ConvergenceStatus(
                ConvergenceState.Converged,
                "Calculation converged successfully",
                iterations,
                residualError,
                unitConvergence);
        }

        /// <summary>
        /// Creates a ConvergenceStatus indicating calculation did not converge.
        /// </summary>
        /// <param name="message">Reason for non-convergence.</param>
        /// <param name="iterations">Number of iterations attempted.</param>
        /// <param name="residualError">Final residual error.</param>
        /// <returns>A new ConvergenceStatus with NotConverged state.</returns>
        public static ConvergenceStatus NotConverged(
            string message,
            int iterations,
            double residualError)
        {
            return new ConvergenceStatus(
                ConvergenceState.NotConverged,
                message ?? "Calculation did not converge",
                iterations,
                residualError);
        }

        /// <summary>
        /// Creates a ConvergenceStatus indicating an error occurred.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A new ConvergenceStatus with Error state.</returns>
        public static ConvergenceStatus Error(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                throw new ArgumentException("Error message cannot be null or empty.", nameof(errorMessage));

            return new ConvergenceStatus(ConvergenceState.Error, errorMessage);
        }

        /// <summary>
        /// Returns a string representation of the convergence status.
        /// </summary>
        /// <returns>A string describing the convergence status.</returns>
        public override string ToString()
        {
            var details = $"{State}";
            if (Iterations.HasValue)
                details += $", Iterations: {Iterations.Value}";
            if (ResidualError.HasValue)
                details += $", Residual: {ResidualError.Value:E3}";
            if (!string.IsNullOrEmpty(Message))
                details += $", Message: {Message}";
            return details;
        }
    }
}
