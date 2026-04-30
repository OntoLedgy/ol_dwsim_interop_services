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

namespace DwsimWorker.Exceptions
{
    /// <summary>
    /// Exception thrown when a requested compound is not found in the DWSIM compound database.
    /// </summary>
    public sealed class CompoundNotFoundException : DwsimException
    {
        /// <summary>
        /// Gets the name of the compound that was not found.
        /// </summary>
        public string CompoundName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompoundNotFoundException"/> class.
        /// </summary>
        /// <param name="compoundName">The name of the compound that was not found.</param>
        public CompoundNotFoundException(string compoundName)
            : base($"Compound '{compoundName}' not found in DWSIM database. Please check the compound name spelling or use a supported compound.")
        {
            CompoundName = compoundName ?? throw new ArgumentNullException(nameof(compoundName));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompoundNotFoundException"/> class with an inner exception.
        /// </summary>
        /// <param name="compoundName">The name of the compound that was not found.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public CompoundNotFoundException(string compoundName, Exception innerException)
            : base($"Compound '{compoundName}' not found in DWSIM database. Please check the compound name spelling or use a supported compound.", innerException)
        {
            CompoundName = compoundName ?? throw new ArgumentNullException(nameof(compoundName));
        }

        /// <summary>
        /// Returns a string representation of the exception including compound name.
        /// </summary>
        /// <returns>A string containing the exception details.</returns>
        public override string ToString()
        {
            return $"{base.ToString()}\nCompoundName: {CompoundName}";
        }
    }
}
