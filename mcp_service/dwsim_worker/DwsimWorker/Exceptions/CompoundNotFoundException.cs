// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
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
