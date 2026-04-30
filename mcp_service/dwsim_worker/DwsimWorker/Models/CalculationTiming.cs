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
    /// Represents timing information for a calculation operation.
    /// This is an immutable record-like class capturing performance metrics.
    /// </summary>
    public sealed class CalculationTiming
    {
        /// <summary>
        /// Gets the total elapsed time for the calculation.
        /// </summary>
        public TimeSpan TotalTime { get; }

        /// <summary>
        /// Gets the timestamp when the calculation started.
        /// </summary>
        public DateTime StartedAt { get; }

        /// <summary>
        /// Gets the timestamp when the calculation completed.
        /// </summary>
        public DateTime CompletedAt { get; }

        /// <summary>
        /// Gets the time spent on initialization (optional breakdown).
        /// </summary>
        public TimeSpan? InitializationTime { get; }

        /// <summary>
        /// Gets the time spent on solver execution (optional breakdown).
        /// </summary>
        public TimeSpan? SolverTime { get; }

        /// <summary>
        /// Gets the time spent on result extraction (optional breakdown).
        /// </summary>
        public TimeSpan? ResultExtractionTime { get; }

        /// <summary>
        /// Gets the total elapsed time in milliseconds (convenience property).
        /// </summary>
        public double TotalMilliseconds => TotalTime.TotalMilliseconds;

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationTiming"/> class.
        /// </summary>
        /// <param name="totalTime">The total elapsed time for the calculation.</param>
        /// <param name="startedAt">The timestamp when the calculation started.</param>
        /// <param name="completedAt">The timestamp when the calculation completed.</param>
        /// <exception cref="ArgumentException">Thrown when totalTime is negative or completedAt is before startedAt.</exception>
        public CalculationTiming(TimeSpan totalTime, DateTime startedAt, DateTime completedAt)
            : this(totalTime, startedAt, completedAt, null, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalculationTiming"/> class with optional breakdown.
        /// </summary>
        /// <param name="totalTime">The total elapsed time for the calculation.</param>
        /// <param name="startedAt">The timestamp when the calculation started.</param>
        /// <param name="completedAt">The timestamp when the calculation completed.</param>
        /// <param name="initializationTime">The time spent on initialization (optional).</param>
        /// <param name="solverTime">The time spent on solver execution (optional).</param>
        /// <param name="resultExtractionTime">The time spent on result extraction (optional).</param>
        /// <exception cref="ArgumentException">Thrown when totalTime is negative, completedAt is before startedAt, or any optional time is negative.</exception>
        public CalculationTiming(
            TimeSpan totalTime,
            DateTime startedAt,
            DateTime completedAt,
            TimeSpan? initializationTime,
            TimeSpan? solverTime,
            TimeSpan? resultExtractionTime)
        {
            if (totalTime < TimeSpan.Zero)
                throw new ArgumentException("Total time cannot be negative.", nameof(totalTime));

            if (completedAt < startedAt)
                throw new ArgumentException("Completed time cannot be before started time.", nameof(completedAt));

            if (initializationTime.HasValue && initializationTime.Value < TimeSpan.Zero)
                throw new ArgumentException("Initialization time cannot be negative.", nameof(initializationTime));

            if (solverTime.HasValue && solverTime.Value < TimeSpan.Zero)
                throw new ArgumentException("Solver time cannot be negative.", nameof(solverTime));

            if (resultExtractionTime.HasValue && resultExtractionTime.Value < TimeSpan.Zero)
                throw new ArgumentException("Result extraction time cannot be negative.", nameof(resultExtractionTime));

            TotalTime = totalTime;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            InitializationTime = initializationTime;
            SolverTime = solverTime;
            ResultExtractionTime = resultExtractionTime;
        }

        /// <summary>
        /// Creates a CalculationTiming instance by calculating the total time from start and end timestamps.
        /// </summary>
        /// <param name="startedAt">The timestamp when the calculation started.</param>
        /// <param name="completedAt">The timestamp when the calculation completed.</param>
        /// <returns>A new CalculationTiming instance.</returns>
        public static CalculationTiming FromTimestamps(DateTime startedAt, DateTime completedAt)
        {
            var totalTime = completedAt - startedAt;
            return new CalculationTiming(totalTime, startedAt, completedAt);
        }

        /// <summary>
        /// Returns a string representation of the calculation timing.
        /// </summary>
        /// <returns>A string containing the timing details.</returns>
        public override string ToString()
        {
            var breakdown = "";
            if (InitializationTime.HasValue || SolverTime.HasValue || ResultExtractionTime.HasValue)
            {
                breakdown = $" (Init: {InitializationTime?.TotalMilliseconds ?? 0:F0}ms, " +
                           $"Solver: {SolverTime?.TotalMilliseconds ?? 0:F0}ms, " +
                           $"Extract: {ResultExtractionTime?.TotalMilliseconds ?? 0:F0}ms)";
            }

            return $"Calculation: {TotalMilliseconds:F0}ms " +
                   $"({StartedAt:yyyy-MM-dd HH:mm:ss} -> {CompletedAt:yyyy-MM-dd HH:mm:ss}){breakdown}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current CalculationTiming.
        /// </summary>
        /// <param name="obj">The object to compare with the current CalculationTiming.</param>
        /// <returns>True if the specified object is equal to the current CalculationTiming; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is CalculationTiming other)
            {
                return TotalTime == other.TotalTime &&
                       StartedAt == other.StartedAt &&
                       CompletedAt == other.CompletedAt &&
                       InitializationTime == other.InitializationTime &&
                       SolverTime == other.SolverTime &&
                       ResultExtractionTime == other.ResultExtractionTime;
            }

            return false;
        }

        /// <summary>
        /// Returns a hash code for the current CalculationTiming.
        /// </summary>
        /// <returns>A hash code for the current CalculationTiming.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + TotalTime.GetHashCode();
                hash = hash * 23 + StartedAt.GetHashCode();
                hash = hash * 23 + CompletedAt.GetHashCode();
                hash = hash * 23 + (InitializationTime?.GetHashCode() ?? 0);
                hash = hash * 23 + (SolverTime?.GetHashCode() ?? 0);
                hash = hash * 23 + (ResultExtractionTime?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
