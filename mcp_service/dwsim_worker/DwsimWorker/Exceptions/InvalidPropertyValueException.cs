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
    /// Exception thrown when an invalid property value is provided (e.g., negative temperature, invalid composition).
    /// Provides detailed information about the property, the invalid value, and the valid range.
    /// </summary>
    public sealed class InvalidPropertyValueException : DwsimException
    {
        /// <summary>
        /// Gets the name of the property that received an invalid value.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Gets the invalid value that was provided.
        /// </summary>
        public object ProvidedValue { get; }

        /// <summary>
        /// Gets the description of the valid range for this property.
        /// </summary>
        public string ValidRange { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidPropertyValueException"/> class.
        /// </summary>
        /// <param name="propertyName">The name of the property that received an invalid value.</param>
        /// <param name="providedValue">The invalid value that was provided.</param>
        /// <param name="validRange">The description of the valid range for this property.</param>
        public InvalidPropertyValueException(string propertyName, object providedValue, string validRange)
            : base($"Invalid value for '{propertyName}': {providedValue}. Valid range: {validRange}")
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            ProvidedValue = providedValue;
            ValidRange = validRange ?? throw new ArgumentNullException(nameof(validRange));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidPropertyValueException"/> class with an inner exception.
        /// </summary>
        /// <param name="propertyName">The name of the property that received an invalid value.</param>
        /// <param name="providedValue">The invalid value that was provided.</param>
        /// <param name="validRange">The description of the valid range for this property.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public InvalidPropertyValueException(string propertyName, object providedValue, string validRange, Exception innerException)
            : base($"Invalid value for '{propertyName}': {providedValue}. Valid range: {validRange}", innerException)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            ProvidedValue = providedValue;
            ValidRange = validRange ?? throw new ArgumentNullException(nameof(validRange));
        }

        /// <summary>
        /// Returns a string representation of the exception including property context and valid range.
        /// </summary>
        /// <returns>A string containing the exception details.</returns>
        public override string ToString()
        {
            return $"{base.ToString()}\nPropertyName: {PropertyName}\nProvidedValue: {ProvidedValue}\nValidRange: {ValidRange}";
        }
    }
}
