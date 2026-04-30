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
    /// Represents a valid range for a physical quantity measurement.
    /// </summary>
    public struct Ranges
    {
        /// <summary>
        /// Gets the minimum valid value (inclusive).
        /// </summary>
        public double Min { get; }

        /// <summary>
        /// Gets the maximum valid value (inclusive).
        /// </summary>
        public double Max { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Ranges"/> struct.
        /// </summary>
        /// <param name="min">The minimum valid value.</param>
        /// <param name="max">The maximum valid value.</param>
        /// <exception cref="ArgumentException">Thrown when min is greater than max.</exception>
        public Ranges(double min, double max)
        {
            if (min > max)
                throw new ArgumentException($"Minimum value ({min}) cannot be greater than maximum value ({max}).");

            Min = min;
            Max = max;
        }

        /// <summary>
        /// Checks if a value is within this range.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True if the value is within range; otherwise, false.</returns>
        public bool Contains(double value)
        {
            return value >= Min && value <= Max;
        }

        /// <summary>
        /// Returns a string representation of the range.
        /// </summary>
        /// <returns>A string in the format "[min, max]".</returns>
        public override string ToString()
        {
            return $"[{Min}, {Max}]";
        }
    }
}
