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
    /// Represents the composition of a material stream as mole fractions.
    /// Mole fractions must sum to 1.0 ± tolerance, and all values must be between 0 and 1.
    /// </summary>
    public sealed class Composition
    {
        /// <summary>
        /// Default tolerance for mole fraction sum validation.
        /// </summary>
        public const double DefaultTolerance = 1e-6;

        /// <summary>
        /// Gets the mole fractions for each compound in the same order as compounds were added to the flowsheet.
        /// </summary>
        public IReadOnlyList<double> MoleFractions { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Composition"/> class.
        /// </summary>
        /// <param name="moleFractions">The mole fractions for each compound. Must sum to 1.0 ± tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when moleFractions is null.</exception>
        /// <exception cref="ArgumentException">Thrown when mole fractions are invalid (negative, > 1, or sum != 1.0).</exception>
        public Composition(IReadOnlyList<double> moleFractions)
        {
            if (moleFractions == null)
                throw new ArgumentNullException(nameof(moleFractions));

            if (moleFractions.Count == 0)
                throw new ArgumentException("Mole fractions list cannot be empty.", nameof(moleFractions));

            // Validate each mole fraction is between 0 and 1
            for (int i = 0; i < moleFractions.Count; i++)
            {
                if (moleFractions[i] < 0.0 || moleFractions[i] > 1.0)
                {
                    throw new ArgumentException(
                        $"Mole fraction at index {i} is out of valid range [0, 1]: {moleFractions[i]}",
                        nameof(moleFractions));
                }
            }

            // Validate sum to 1.0
            if (!SumsToOne(moleFractions, DefaultTolerance))
            {
                double sum = moleFractions.Sum();
                throw new ArgumentException(
                    $"Mole fractions must sum to 1.0 ± {DefaultTolerance}. Provided sum: {sum}",
                    nameof(moleFractions));
            }

            MoleFractions = new List<double>(moleFractions);
        }

        /// <summary>
        /// Validates that the composition is valid.
        /// </summary>
        /// <returns>True if the composition is valid; otherwise, false.</returns>
        public bool IsValid()
        {
            return IsValid(MoleFractions.Count);
        }

        /// <summary>
        /// Validates that the composition has the expected number of compounds and all mole fractions are valid.
        /// </summary>
        /// <param name="expectedCompoundCount">The expected number of compounds.</param>
        /// <returns>True if the composition is valid; otherwise, false.</returns>
        public bool IsValid(int expectedCompoundCount)
        {
            if (MoleFractions.Count != expectedCompoundCount)
            {
                return false;
            }

            return SumsToOne(MoleFractions, DefaultTolerance);
        }

        /// <summary>
        /// Validates that a list of mole fractions sums to 1.0 within the specified tolerance.
        /// </summary>
        /// <param name="moleFractions">The mole fractions to validate.</param>
        /// <param name="tolerance">The acceptable tolerance for the sum. Default is 1e-6.</param>
        /// <returns>True if the sum is within tolerance of 1.0; otherwise, false.</returns>
        public static bool SumsToOne(IReadOnlyList<double> moleFractions, double tolerance = DefaultTolerance)
        {
            if (moleFractions == null || moleFractions.Count == 0)
                return false;

            double sum = moleFractions.Sum();
            return Math.Abs(sum - 1.0) <= tolerance;
        }

        /// <summary>
        /// Returns a string representation of the composition.
        /// </summary>
        /// <returns>A string containing the mole fractions.</returns>
        public override string ToString()
        {
            return $"Composition: [{string.Join(", ", MoleFractions.Select(f => f.ToString("F6")))}]";
        }
    }
}
