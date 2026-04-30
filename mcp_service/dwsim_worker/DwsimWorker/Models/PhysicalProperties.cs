// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents a named physical property with its measurement.
    /// Physical properties belong to objects (streams, units) and have meaningful names.
    /// </summary>
    /// <remarks>
    /// A PhysicalProperties represents a specific property of an object, such as:
    /// - "InletPressure" - the pressure at the inlet of a unit
    /// - "OutletTemperature" - the temperature at the outlet
    /// - "WallThickness" - the thickness of a vessel wall
    ///
    /// The property combines a descriptive name with a measurement (value, quantity, unit).
    /// </remarks>
    public sealed class PhysicalProperties
    {
        /// <summary>
        /// Gets the name of this physical property (e.g., "InletPressure", "OutletTemperature").
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the measurement for this property.
        /// </summary>
        public Measurements Measurement { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PhysicalProperties"/> class.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="measurement">The measurement value.</param>
        /// <exception cref="ArgumentNullException">Thrown when name or measurement is null.</exception>
        /// <exception cref="ArgumentException">Thrown when name is empty or whitespace.</exception>
        public PhysicalProperties(string name, Measurements measurement)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Property name cannot be empty or whitespace.", nameof(name));

            Name = name.Trim();
            Measurement = measurement ?? throw new ArgumentNullException(nameof(measurement));
        }

        /// <summary>
        /// Gets the numeric value of this property's measurement.
        /// </summary>
        public double Value => Measurement.Value;

        /// <summary>
        /// Gets the unit of this property's measurement.
        /// </summary>
        public UnitsOfMeasure Unit => Measurement.Unit;

        /// <summary>
        /// Gets the physical quantity type of this property.
        /// </summary>
        public PhysicalQuantities Quantity => Measurement.Quantity;

        /// <summary>
        /// Returns a string representation of the physical property.
        /// </summary>
        /// <returns>A string in the format "name: value unit".</returns>
        public override string ToString()
        {
            return $"{Name}: {Measurement}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>True if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is PhysicalProperties other)
            {
                return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
                       Measurement.Equals(other.Measurement);
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
                hash = hash * 23 + (Name?.ToLowerInvariant().GetHashCode() ?? 0);
                hash = hash * 23 + (Measurement?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
