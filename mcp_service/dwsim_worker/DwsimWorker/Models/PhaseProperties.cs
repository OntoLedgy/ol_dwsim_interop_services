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
        /// Gets the specific enthalpy of the phase in kJ/kg (optional).
        /// </summary>
        public double? EnthalpyKJPerKg { get; }

        /// <summary>
        /// Gets the molar enthalpy of the phase in kJ/kmol (optional).
        /// </summary>
        public double? MolarEnthalpyKJPerKmol { get; }

        /// <summary>
        /// Gets the specific entropy of the phase in kJ/(kg*K) (optional).
        /// </summary>
        public double? EntropyKJPerKgK { get; }

        /// <summary>
        /// Gets the molar entropy of the phase in kJ/(kmol*K) (optional).
        /// </summary>
        public double? MolarEntropyKJPerKmolK { get; }

        /// <summary>
        /// Gets the volumetric flow of the phase in m3/s (optional).
        /// </summary>
        public double? VolumetricFlowM3PerSec { get; }

        /// <summary>
        /// Gets the mass fraction of the phase (0 to 1, optional).
        /// </summary>
        public double? MassFraction { get; }

        /// <summary>
        /// Gets the volumetric fraction of the phase (0 to 1, optional).
        /// </summary>
        public double? VolumetricFraction { get; }

        /// <summary>
        /// Gets the Gibbs free energy of the phase (optional).
        /// </summary>
        public double? GibbsFreeEnergy { get; }

        /// <summary>
        /// Gets the Helmholtz energy of the phase (optional).
        /// </summary>
        public double? HelmholtzEnergy { get; }

        /// <summary>
        /// Gets the internal energy of the phase (optional).
        /// </summary>
        public double? InternalEnergy { get; }

        /// <summary>
        /// Gets the K-value of the phase (optional).
        /// </summary>
        public double? KValue { get; }

        /// <summary>
        /// Gets the fugacity of the phase (optional).
        /// </summary>
        public double? Fugacity { get; }

        /// <summary>
        /// Gets the activity coefficient of the phase (optional).
        /// </summary>
        public double? ActivityCoefficient { get; }

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
            double? molecularWeightKgPerKmol,
            double? enthalpyKJPerKg = null,
            double? molarEnthalpyKJPerKmol = null,
            double? entropyKJPerKgK = null,
            double? molarEntropyKJPerKmolK = null,
            double? volumetricFlowM3PerSec = null,
            double? massFraction = null,
            double? volumetricFraction = null,
            double? gibbsFreeEnergy = null,
            double? helmholtzEnergy = null,
            double? internalEnergy = null,
            double? kValue = null,
            double? fugacity = null,
            double? activityCoefficient = null)
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

            // Allow zero molecular weight only when flow is also zero (empty stream/phase)
            if (molecularWeightKgPerKmol.HasValue && molecularWeightKgPerKmol.Value < 0)
                throw new ArgumentException("Molecular weight cannot be negative.", nameof(molecularWeightKgPerKmol));

            if (molecularWeightKgPerKmol.HasValue && molecularWeightKgPerKmol.Value == 0 && molarFlowMolPerSec > 0)
                throw new ArgumentException("Molecular weight cannot be zero when flow is non-zero.", nameof(molecularWeightKgPerKmol));

            if (volumetricFlowM3PerSec.HasValue && volumetricFlowM3PerSec.Value < 0)
                throw new ArgumentException("Volumetric flow cannot be negative.", nameof(volumetricFlowM3PerSec));

            if (massFraction.HasValue && (massFraction.Value < 0 || massFraction.Value > 1))
                throw new ArgumentException("Mass fraction must be between 0 and 1.", nameof(massFraction));

            if (volumetricFraction.HasValue && (volumetricFraction.Value < 0 || volumetricFraction.Value > 1))
                throw new ArgumentException("Volumetric fraction must be between 0 and 1.", nameof(volumetricFraction));

            PhaseName = phaseName;
            MolarFlowMolPerSec = molarFlowMolPerSec;
            MassFlowKgPerSec = massFlowKgPerSec;
            PhaseFraction = phaseFraction;
            Composition = composition;
            DensityKgPerM3 = densityKgPerM3;
            ViscosityPaS = viscosityPaS;
            MolecularWeightKgPerKmol = molecularWeightKgPerKmol;
            EnthalpyKJPerKg = enthalpyKJPerKg;
            MolarEnthalpyKJPerKmol = molarEnthalpyKJPerKmol;
            EntropyKJPerKgK = entropyKJPerKgK;
            MolarEntropyKJPerKmolK = molarEntropyKJPerKmolK;
            VolumetricFlowM3PerSec = volumetricFlowM3PerSec;
            MassFraction = massFraction;
            VolumetricFraction = volumetricFraction;
            GibbsFreeEnergy = gibbsFreeEnergy;
            HelmholtzEnergy = helmholtzEnergy;
            InternalEnergy = internalEnergy;
            KValue = kValue;
            Fugacity = fugacity;
            ActivityCoefficient = activityCoefficient;
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
                       MolecularWeightKgPerKmol == other.MolecularWeightKgPerKmol &&
                       EnthalpyKJPerKg == other.EnthalpyKJPerKg &&
                       MolarEnthalpyKJPerKmol == other.MolarEnthalpyKJPerKmol &&
                       EntropyKJPerKgK == other.EntropyKJPerKgK &&
                       MolarEntropyKJPerKmolK == other.MolarEntropyKJPerKmolK &&
                       VolumetricFlowM3PerSec == other.VolumetricFlowM3PerSec &&
                       MassFraction == other.MassFraction &&
                       VolumetricFraction == other.VolumetricFraction &&
                       GibbsFreeEnergy == other.GibbsFreeEnergy &&
                       HelmholtzEnergy == other.HelmholtzEnergy &&
                       InternalEnergy == other.InternalEnergy &&
                       KValue == other.KValue &&
                       Fugacity == other.Fugacity &&
                       ActivityCoefficient == other.ActivityCoefficient;
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
                hash = hash * 23 + (EnthalpyKJPerKg?.GetHashCode() ?? 0);
                hash = hash * 23 + (MolarEnthalpyKJPerKmol?.GetHashCode() ?? 0);
                hash = hash * 23 + (EntropyKJPerKgK?.GetHashCode() ?? 0);
                hash = hash * 23 + (MolarEntropyKJPerKmolK?.GetHashCode() ?? 0);
                hash = hash * 23 + (VolumetricFlowM3PerSec?.GetHashCode() ?? 0);
                hash = hash * 23 + (MassFraction?.GetHashCode() ?? 0);
                hash = hash * 23 + (VolumetricFraction?.GetHashCode() ?? 0);
                hash = hash * 23 + (GibbsFreeEnergy?.GetHashCode() ?? 0);
                hash = hash * 23 + (HelmholtzEnergy?.GetHashCode() ?? 0);
                hash = hash * 23 + (InternalEnergy?.GetHashCode() ?? 0);
                hash = hash * 23 + (KValue?.GetHashCode() ?? 0);
                hash = hash * 23 + (Fugacity?.GetHashCode() ?? 0);
                hash = hash * 23 + (ActivityCoefficient?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
