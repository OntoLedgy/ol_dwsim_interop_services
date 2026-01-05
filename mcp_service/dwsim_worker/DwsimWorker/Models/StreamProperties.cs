using System;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents all thermodynamic properties of a material stream.
    /// Properties use SI units: Temperature (K), Pressure (Pa), Molar Flow (mol/s).
    /// </summary>
    public sealed class StreamProperties
    {
        /// <summary>
        /// Gets the temperature in Kelvin.
        /// </summary>
        public double TemperatureK { get; }

        /// <summary>
        /// Gets the pressure in Pascal.
        /// </summary>
        public double PressurePa { get; }

        /// <summary>
        /// Gets the molar flow rate in mol/s.
        /// </summary>
        public double MolarFlowMolPerSec { get; }

        /// <summary>
        /// Gets the composition (mole fractions for each compound).
        /// </summary>
        public Composition Composition { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamProperties"/> class.
        /// </summary>
        /// <param name="temperatureK">The temperature in Kelvin. Must be greater than 0.</param>
        /// <param name="pressurePa">The pressure in Pascal. Must be greater than 0.</param>
        /// <param name="molarFlowMolPerSec">The molar flow rate in mol/s. Must be greater than or equal to 0.</param>
        /// <param name="composition">The composition (mole fractions).</param>
        /// <exception cref="ArgumentException">Thrown when any property value is out of valid range.</exception>
        /// <exception cref="ArgumentNullException">Thrown when composition is null.</exception>
        public StreamProperties(
            double temperatureK,
            double pressurePa,
            double molarFlowMolPerSec,
            Composition composition)
        {
            Composition = composition ?? throw new ArgumentNullException(nameof(composition));

            if (!IsValid(temperatureK, pressurePa, molarFlowMolPerSec, out string errorMessage))
            {
                throw new ArgumentException(errorMessage);
            }

            TemperatureK = temperatureK;
            PressurePa = pressurePa;
            MolarFlowMolPerSec = molarFlowMolPerSec;
        }

        /// <summary>
        /// Validates stream property values against physical constraints.
        /// </summary>
        /// <param name="temperatureK">The temperature to validate.</param>
        /// <param name="pressurePa">The pressure to validate.</param>
        /// <param name="molarFlowMolPerSec">The molar flow to validate.</param>
        /// <param name="errorMessage">If validation fails, contains a description of the error.</param>
        /// <returns>True if all values are valid; otherwise, false.</returns>
        public static bool IsValid(
            double temperatureK,
            double pressurePa,
            double molarFlowMolPerSec,
            out string errorMessage)
        {
            const double MinTemperatureK = 0.0;
            const double MaxTemperatureK = 10000.0;
            const double MinPressurePa = 0.0;
            const double MaxPressurePa = 1e9; // 10000 bar
            const double MinMolarFlowMolPerSec = 0.0;

            if (temperatureK <= MinTemperatureK)
            {
                errorMessage = $"Temperature must be > {MinTemperatureK} K. Provided: {temperatureK} K.";
                return false;
            }

            if (temperatureK > MaxTemperatureK)
            {
                errorMessage = $"Temperature must be <= {MaxTemperatureK} K. Provided: {temperatureK} K.";
                return false;
            }

            if (pressurePa <= MinPressurePa)
            {
                errorMessage = $"Pressure must be > {MinPressurePa} Pa. Provided: {pressurePa} Pa.";
                return false;
            }

            if (pressurePa > MaxPressurePa)
            {
                errorMessage = $"Pressure must be <= {MaxPressurePa} Pa. Provided: {pressurePa} Pa.";
                return false;
            }

            if (molarFlowMolPerSec < MinMolarFlowMolPerSec)
            {
                errorMessage = $"Molar flow must be >= {MinMolarFlowMolPerSec} mol/s. Provided: {molarFlowMolPerSec} mol/s.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Validates the current instance properties.
        /// </summary>
        /// <param name="errorMessage">If validation fails, contains a description of the error.</param>
        /// <returns>True if all values are valid; otherwise, false.</returns>
        public bool IsValid(out string errorMessage)
        {
            if (!IsValid(TemperatureK, PressurePa, MolarFlowMolPerSec, out errorMessage))
            {
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
