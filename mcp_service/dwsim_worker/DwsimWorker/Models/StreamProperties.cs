using System;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents all thermodynamic properties of a material stream.
    /// Properties are stored with their measurements (quantity, value, unit) and validated.
    /// </summary>
    /// <remarks>
    /// This class stores physical properties of a stream as provided by the user.
    /// Unit conversion and full validation are delegated to DWSIM's systems.
    ///
    /// Example usage:
    /// <code>
    /// var tempUnit = new UnitsOfMeasure("K", typeof(Temperature), new Ranges(0, double.PositiveInfinity));
    /// var tempMeasurement = new Measurements(new Temperature(), 298.15, tempUnit);
    /// var tempProperty = new PhysicalProperties("Temperature", tempMeasurement);
    /// </code>
    /// </remarks>
    public sealed class StreamProperties
    {
        /// <summary>
        /// Gets the temperature property.
        /// </summary>
        public PhysicalProperties Temperature { get; }

        /// <summary>
        /// Gets the pressure property.
        /// </summary>
        public PhysicalProperties Pressure { get; }

        /// <summary>
        /// Gets the molar flow rate property.
        /// </summary>
        public PhysicalProperties MolarFlow { get; }

        /// <summary>
        /// Gets the composition (mole fractions for each compound).
        /// </summary>
        public Composition Composition { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamProperties"/> class.
        /// </summary>
        /// <param name="temperature">The temperature physical property.</param>
        /// <param name="pressure">The pressure physical property.</param>
        /// <param name="molarFlow">The molar flow rate physical property.</param>
        /// <param name="composition">The composition (mole fractions).</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        public StreamProperties(
            PhysicalProperties temperature,
            PhysicalProperties pressure,
            PhysicalProperties molarFlow,
            Composition composition)
        {
            Temperature = temperature ?? throw new ArgumentNullException(nameof(temperature));
            Pressure = pressure ?? throw new ArgumentNullException(nameof(pressure));
            MolarFlow = molarFlow ?? throw new ArgumentNullException(nameof(molarFlow));
            Composition = composition ?? throw new ArgumentNullException(nameof(composition));
        }

        /// <summary>
        /// Validates the stream properties.
        /// </summary>
        /// <param name="errorMessage">If validation fails, contains a description of the error.</param>
        /// <returns>True if all values are valid; otherwise, false.</returns>
        /// <remarks>
        /// For now, performs basic validation:
        /// - All properties must be non-null
        /// - Composition must be valid (sum to 1.0)
        ///
        /// Full validation including unit-specific constraints will be delegated to DWSIM
        /// when the stream is created in the flowsheet.
        /// </remarks>
        public bool IsValid(out string errorMessage)
        {
            if (Temperature == null)
            {
                errorMessage = "Temperature cannot be null";
                return false;
            }

            if (Pressure == null)
            {
                errorMessage = "Pressure cannot be null";
                return false;
            }

            if (MolarFlow == null)
            {
                errorMessage = "Molar flow cannot be null";
                return false;
            }

            if (Composition == null)
            {
                errorMessage = "Composition cannot be null";
                return false;
            }

            if (!Composition.IsValid())
            {
                errorMessage = "Composition validation failed. Mole fractions must sum to 1.0 ± 1e-6.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
