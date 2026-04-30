// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents a unit of measure for a specific physical quantity.
    /// Units are tied to quantity types and have valid ranges.
    /// </summary>
    /// <remarks>
    /// This class stores unit information and will integrate with DWSIM's unit conversion
    /// system for converting between units.
    ///
    /// Common units:
    /// - Temperature: K (0 to ∞), C (-273.15 to ∞), F (-459.67 to ∞), R (0 to ∞)
    /// - Pressure: Pa, bar, psi, atm, kPa, MPa, kgf/cm2 (all 0 to ∞)
    /// - MolarFlowRate: mol/s, kmol/h, lbmol/h (all 0 to ∞)
    /// - MassFlowRate: kg/s, kg/h, lb/h, ton/h (all 0 to ∞)
    /// - VolumetricFlowRate: m3/s, m3/h, L/s, ft3/s (all 0 to ∞)
    /// - Distance: m, cm, mm, ft, in (all 0 to ∞)
    /// </remarks>
    public sealed class UnitsOfMeasure
    {
        /// <summary>
        /// Gets the unit name (e.g., "K", "bar", "mol/s").
        /// </summary>
        public string UnitName { get; }

        /// <summary>
        /// Gets the type of physical quantity this unit measures.
        /// </summary>
        public Type QuantityType { get; }

        /// <summary>
        /// Gets the valid range for values in this unit.
        /// </summary>
        public Ranges ValidRange { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitsOfMeasure"/> class.
        /// </summary>
        /// <param name="unitName">The unit name (e.g., "K", "bar", "mol/s").</param>
        /// <param name="quantityType">The type of physical quantity (e.g., typeof(Temperature)).</param>
        /// <param name="validRange">The valid range for measurements in this unit.</param>
        /// <exception cref="ArgumentNullException">Thrown when unitName or quantityType is null.</exception>
        /// <exception cref="ArgumentException">Thrown when unitName is empty or whitespace, or quantityType is not a PhysicalQuantities subclass.</exception>
        public UnitsOfMeasure(string unitName, Type quantityType, Ranges validRange)
        {
            if (unitName == null)
                throw new ArgumentNullException(nameof(unitName));

            if (string.IsNullOrWhiteSpace(unitName))
                throw new ArgumentException("Unit name cannot be empty or whitespace.", nameof(unitName));

            if (quantityType == null)
                throw new ArgumentNullException(nameof(quantityType));

            if (!typeof(PhysicalQuantities).IsAssignableFrom(quantityType))
                throw new ArgumentException(
                    $"Quantity type must be a subclass of PhysicalQuantities. Provided: {quantityType.Name}",
                    nameof(quantityType));

            UnitName = unitName.Trim();
            QuantityType = quantityType;
            ValidRange = validRange;
        }

        /// <summary>
        /// Validates that a value is within the valid range for this unit.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <returns>True if the value is within range; otherwise, false.</returns>
        public bool IsValueValid(double value)
        {
            return ValidRange.Contains(value);
        }

        /// <summary>
        /// Validates that a value is within the valid range, returning an error message if invalid.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="errorMessage">The error message if validation fails.</param>
        /// <returns>True if valid; otherwise, false with error message.</returns>
        public bool IsValueValid(double value, out string errorMessage)
        {
            if (ValidRange.Contains(value))
            {
                errorMessage = null;
                return true;
            }

            errorMessage = $"Value {value} {UnitName} is outside valid range {ValidRange} for unit {UnitName}.";
            return false;
        }

        /// <summary>
        /// Returns a string representation of the unit.
        /// </summary>
        /// <returns>The unit name.</returns>
        public override string ToString()
        {
            return UnitName;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>True if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is UnitsOfMeasure other)
            {
                return string.Equals(UnitName, other.UnitName, StringComparison.OrdinalIgnoreCase) &&
                       QuantityType == other.QuantityType;
            }
            return false;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (UnitName?.ToLowerInvariant().GetHashCode() ?? 0);
                hash = hash * 23 + (QuantityType?.GetHashCode() ?? 0);
                return hash;
            }
        }

        // TODO: Later integration with DWSIM unit system:
        // - Add ConvertTo(UnitsOfMeasure targetUnit, double value) method
        // - Add ConvertToBaseUnit(double value) method
        // - Add static GetAvailableUnits(Type quantityType) method
        // These will delegate to DWSIM's unit conversion system
    }
}
