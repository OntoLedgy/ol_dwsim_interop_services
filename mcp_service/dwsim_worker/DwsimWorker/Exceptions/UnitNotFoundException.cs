using System;

namespace DwsimWorker.Exceptions
{
    /// <summary>
    /// Exception thrown when a requested unit operation is not found in the flowsheet.
    /// </summary>
    public sealed class UnitNotFoundException : DwsimException
    {
        /// <summary>
        /// Gets the ID of the unit operation that was not found.
        /// </summary>
        public string UnitId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitNotFoundException"/> class.
        /// </summary>
        /// <param name="unitId">The ID of the unit operation that was not found.</param>
        public UnitNotFoundException(string unitId)
            : base($"Unit operation '{unitId}' not found in flowsheet. The unit may not have been created or may have been removed.")
        {
            UnitId = unitId ?? throw new ArgumentNullException(nameof(unitId));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitNotFoundException"/> class with an inner exception.
        /// </summary>
        /// <param name="unitId">The ID of the unit operation that was not found.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public UnitNotFoundException(string unitId, Exception innerException)
            : base($"Unit operation '{unitId}' not found in flowsheet. The unit may not have been created or may have been removed.", innerException)
        {
            UnitId = unitId ?? throw new ArgumentNullException(nameof(unitId));
        }

        /// <summary>
        /// Returns a string representation of the exception including unit ID.
        /// </summary>
        /// <returns>A string containing the exception details.</returns>
        public override string ToString()
        {
            return $"{base.ToString()}\nUnitId: {UnitId}";
        }
    }
}
