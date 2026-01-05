using System;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents a measurement of a physical quantity using a specific unit.
    /// Acts as a bridge between PhysicalQuantities and UnitsOfMeasure.
    /// </summary>
    /// <remarks>
    /// A Measurements combines:
    /// - What is being measured (PhysicalQuantities - e.g., Temperature, Pressure)
    /// - The numeric value
    /// - The unit of measure (UnitsOfMeasure - e.g., K, bar)
    ///
    /// Validation ensures the unit is compatible with the quantity type and
    /// the value is within the valid range for that unit.
    /// </remarks>
    public sealed class Measurements
    {
        /// <summary>
        /// Gets the physical quantity being measured.
        /// </summary>
        public PhysicalQuantities Quantity { get; }

        /// <summary>
        /// Gets the numeric value of the measurement.
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Gets the unit of measure for this measurement.
        /// </summary>
        public UnitsOfMeasure Unit { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Measurements"/> class.
        /// </summary>
        /// <param name="quantity">The physical quantity being measured.</param>
        /// <param name="value">The numeric value.</param>
        /// <param name="unit">The unit of measure.</param>
        /// <exception cref="ArgumentNullException">Thrown when quantity or unit is null.</exception>
        /// <exception cref="ArgumentException">Thrown when unit type doesn't match quantity type or value is out of range.</exception>
        public Measurements(PhysicalQuantities quantity, double value, UnitsOfMeasure unit)
        {
            Quantity = quantity ?? throw new ArgumentNullException(nameof(quantity));
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));

            // Validate that the unit's quantity type matches the provided quantity
            if (!unit.QuantityType.IsInstanceOfType(quantity))
            {
                throw new ArgumentException(
                    $"Unit type mismatch: Unit '{unit.UnitName}' is for {unit.QuantityType.Name}, " +
                    $"but quantity provided is {quantity.GetType().Name}.",
                    nameof(unit));
            }

            // Validate that the value is within the valid range for this unit
            if (!unit.IsValueValid(value, out string errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(value));
            }

            Value = value;
        }

        /// <summary>
        /// Returns a string representation of the measurement.
        /// </summary>
        /// <returns>A string in the format "value unit".</returns>
        public override string ToString()
        {
            return $"{Value} {Unit.UnitName}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>True if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is Measurements other)
            {
                return Math.Abs(Value - other.Value) < 1e-10 &&
                       Quantity.GetType() == other.Quantity.GetType() &&
                       Unit.Equals(other.Unit);
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
                hash = hash * 23 + Value.GetHashCode();
                hash = hash * 23 + (Quantity?.GetType().GetHashCode() ?? 0);
                hash = hash * 23 + (Unit?.GetHashCode() ?? 0);
                return hash;
            }
        }

        // TODO: Later integration with DWSIM:
        // - Add ConvertTo(UnitsOfMeasure targetUnit) method
        // - Add ToBaseUnits() method
        // These will use DWSIM's unit conversion system
    }
}
