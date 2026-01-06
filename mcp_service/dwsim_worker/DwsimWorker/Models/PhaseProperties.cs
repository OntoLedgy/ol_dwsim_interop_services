using System;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents calculated properties for a specific phase in a material stream.
    /// This is an immutable model following the existing pattern for domain models.
    /// </summary>
    public sealed class PhaseProperties
    {
        /// <summary>
        /// Gets the name of the phase (e.g., "Vapor", "Liquid1", "Liquid2", "Overall").
        /// </summary>
        public string PhaseName { get; }

        /// <summary>
        /// Gets the molar flow rate of the phase in mol/sec.
        /// </summary>
        public double MolarFlowMolPerSec { get; }

        /// <summary>
        /// Gets the mass flow rate of the phase in kg/sec.
        /// </summary>
        public double MassFlowKgPerSec { get; }

        /// <summary>
        /// Gets the phase fraction (0 to 1).
        /// </summary>
        public double PhaseFraction { get; }

        /// <summary>
        /// Gets the composition of the phase as mole fractions.
        /// </summary>
        public Composition Composition { get; }

        /// <summary>
        /// Gets the density of the phase in kg/m³ (optional).
        /// </summary>
        public double? DensityKgPerM3 { get; }

        /// <summary>
        /// Gets the dynamic viscosity of the phase in Pa·s (optional).
        /// </summary>
        public double? ViscosityPaS { get; }

        /// <summary>
        /// Gets the molecular weight of the phase in kg/kmol (optional).
        /// </summary>
        public double? MolecularWeightKgPerKmol { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PhaseProperties"/> class with required properties.
        /// </summary>
        /// <param name="phaseName">The name of the phase.</param>
        /// <param name="molarFlowMolPerSec">The molar flow rate in mol/sec.</param>
        /// <param name="massFlowKgPerSec">The mass flow rate in kg/sec.</param>
        /// <param name="phaseFraction">The phase fraction (0 to 1).</param>
        /// <param name="composition">The composition of the phase.</param>
        /// <exception cref="ArgumentNullException">Thrown when phaseName or composition is null.</exception>
        /// <exception cref="ArgumentException">Thrown when phaseFraction is out of range [0, 1] or flow rates are negative.</exception>
        public PhaseProperties(
            string phaseName,
            double molarFlowMolPerSec,
            double massFlowKgPerSec,
            double phaseFraction,
            Composition composition)
            : this(phaseName, molarFlowMolPerSec, massFlowKgPerSec, phaseFraction, composition, null, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PhaseProperties"/> class with all properties.
        /// </summary>
        /// <param name="phaseName">The name of the phase.</param>
        /// <param name="molarFlowMolPerSec">The molar flow rate in mol/sec.</param>
        /// <param name="massFlowKgPerSec">The mass flow rate in kg/sec.</param>
        /// <param name="phaseFraction">The phase fraction (0 to 1).</param>
        /// <param name="composition">The composition of the phase.</param>
        /// <param name="densityKgPerM3">The density in kg/m³ (optional).</param>
        /// <param name="viscosityPaS">The dynamic viscosity in Pa·s (optional).</param>
        /// <param name="molecularWeightKgPerKmol">The molecular weight in kg/kmol (optional).</param>
        /// <exception cref="ArgumentNullException">Thrown when phaseName or composition is null.</exception>
        /// <exception cref="ArgumentException">Thrown when values are out of valid ranges.</exception>
        public PhaseProperties(
            string phaseName,
            double molarFlowMolPerSec,
            double massFlowKgPerSec,
            double phaseFraction,
            Composition composition,
            double? densityKgPerM3,
            double? viscosityPaS,
            double? molecularWeightKgPerKmol)
        {
            if (string.IsNullOrWhiteSpace(phaseName))
                throw new ArgumentNullException(nameof(phaseName), "Phase name cannot be null or empty.");

            if (composition == null)
                throw new ArgumentNullException(nameof(composition), "Composition cannot be null.");

            if (molarFlowMolPerSec < 0)
                throw new ArgumentException("Molar flow rate cannot be negative.", nameof(molarFlowMolPerSec));

            if (massFlowKgPerSec < 0)
                throw new ArgumentException("Mass flow rate cannot be negative.", nameof(massFlowKgPerSec));

            if (phaseFraction < 0 || phaseFraction > 1)
                throw new ArgumentException("Phase fraction must be between 0 and 1.", nameof(phaseFraction));

            if (densityKgPerM3.HasValue && densityKgPerM3.Value < 0)
                throw new ArgumentException("Density cannot be negative.", nameof(densityKgPerM3));

            if (viscosityPaS.HasValue && viscosityPaS.Value < 0)
                throw new ArgumentException("Viscosity cannot be negative.", nameof(viscosityPaS));

            if (molecularWeightKgPerKmol.HasValue && molecularWeightKgPerKmol.Value <= 0)
                throw new ArgumentException("Molecular weight must be positive.", nameof(molecularWeightKgPerKmol));

            PhaseName = phaseName;
            MolarFlowMolPerSec = molarFlowMolPerSec;
            MassFlowKgPerSec = massFlowKgPerSec;
            PhaseFraction = phaseFraction;
            Composition = composition;
            DensityKgPerM3 = densityKgPerM3;
            ViscosityPaS = viscosityPaS;
            MolecularWeightKgPerKmol = molecularWeightKgPerKmol;
        }

        /// <summary>
        /// Returns a string representation of the phase properties.
        /// </summary>
        /// <returns>A string containing the phase details.</returns>
        public override string ToString()
        {
            var optional = "";
            if (DensityKgPerM3.HasValue || ViscosityPaS.HasValue || MolecularWeightKgPerKmol.HasValue)
            {
                optional = $" [ρ={DensityKgPerM3?.ToString("F2") ?? "N/A"} kg/m³, " +
                          $"μ={ViscosityPaS?.ToString("E3") ?? "N/A"} Pa·s, " +
                          $"MW={MolecularWeightKgPerKmol?.ToString("F2") ?? "N/A"} kg/kmol]";
            }

            return $"Phase '{PhaseName}': Molar={MolarFlowMolPerSec:F4} mol/s, " +
                   $"Mass={MassFlowKgPerSec:F4} kg/s, Fraction={PhaseFraction:F4}{optional}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current PhaseProperties.
        /// </summary>
        /// <param name="obj">The object to compare with the current PhaseProperties.</param>
        /// <returns>True if the specified object is equal to the current PhaseProperties; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is PhaseProperties other)
            {
                return PhaseName == other.PhaseName &&
                       MolarFlowMolPerSec == other.MolarFlowMolPerSec &&
                       MassFlowKgPerSec == other.MassFlowKgPerSec &&
                       PhaseFraction == other.PhaseFraction &&
                       DensityKgPerM3 == other.DensityKgPerM3 &&
                       ViscosityPaS == other.ViscosityPaS &&
                       MolecularWeightKgPerKmol == other.MolecularWeightKgPerKmol;
                // Note: Composition equality not checked here for simplicity
            }

            return false;
        }

        /// <summary>
        /// Returns a hash code for the current PhaseProperties.
        /// </summary>
        /// <returns>A hash code for the current PhaseProperties.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (PhaseName?.GetHashCode() ?? 0);
                hash = hash * 23 + MolarFlowMolPerSec.GetHashCode();
                hash = hash * 23 + MassFlowKgPerSec.GetHashCode();
                hash = hash * 23 + PhaseFraction.GetHashCode();
                hash = hash * 23 + (DensityKgPerM3?.GetHashCode() ?? 0);
                hash = hash * 23 + (ViscosityPaS?.GetHashCode() ?? 0);
                hash = hash * 23 + (MolecularWeightKgPerKmol?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
