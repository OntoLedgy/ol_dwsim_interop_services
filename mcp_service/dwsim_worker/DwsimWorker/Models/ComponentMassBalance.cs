// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents the mass balance for a single component in a calculation.
    /// This is an immutable model following the result pattern.
    /// </summary>
    public sealed class ComponentMassBalance
    {
        /// <summary>
        /// Gets the name of the compound.
        /// </summary>
        public string CompoundName { get; }

        /// <summary>
        /// Gets the total inlet moles for this component (mol/sec).
        /// </summary>
        public double InletMoles { get; }

        /// <summary>
        /// Gets the total outlet moles for this component (mol/sec).
        /// </summary>
        public double OutletMoles { get; }

        /// <summary>
        /// Gets the relative error as a percentage.
        /// Calculated as: |Outlet - Inlet| / Inlet * 100
        /// </summary>
        public double RelativeErrorPercent { get; }

        /// <summary>
        /// Gets a value indicating whether the mass balance for this component is valid.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentMassBalance"/> class.
        /// Private constructor - use factory methods instead.
        /// </summary>
        /// <param name="compoundName">The name of the compound.</param>
        /// <param name="inletMoles">The total inlet moles (mol/sec).</param>
        /// <param name="outletMoles">The total outlet moles (mol/sec).</param>
        /// <param name="relativeErrorPercent">The relative error as a percentage.</param>
        /// <param name="isValid">Whether the mass balance is valid.</param>
        private ComponentMassBalance(
            string compoundName,
            double inletMoles,
            double outletMoles,
            double relativeErrorPercent,
            bool isValid)
        {
            CompoundName = compoundName;
            InletMoles = inletMoles;
            OutletMoles = outletMoles;
            RelativeErrorPercent = relativeErrorPercent;
            IsValid = isValid;
        }

        /// <summary>
        /// Creates a ComponentMassBalance with automatic validity calculation based on tolerance.
        /// </summary>
        /// <param name="compoundName">The name of the compound.</param>
        /// <param name="inletMoles">The total inlet moles (mol/sec).</param>
        /// <param name="outletMoles">The total outlet moles (mol/sec).</param>
        /// <param name="tolerancePercent">The tolerance for validation (default 1%).</param>
        /// <returns>A new ComponentMassBalance instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when compoundName is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when flow values are negative.</exception>
        public static ComponentMassBalance Create(
            string compoundName,
            double inletMoles,
            double outletMoles,
            double tolerancePercent = 1.0)
        {
            if (string.IsNullOrWhiteSpace(compoundName))
                throw new ArgumentNullException(nameof(compoundName), "Compound name cannot be null or empty.");

            if (inletMoles < 0)
                throw new ArgumentException("Inlet moles cannot be negative.", nameof(inletMoles));

            if (outletMoles < 0)
                throw new ArgumentException("Outlet moles cannot be negative.", nameof(outletMoles));

            if (tolerancePercent < 0)
                throw new ArgumentException("Tolerance cannot be negative.", nameof(tolerancePercent));

            // Calculate relative error
            double relativeErrorPercent = 0.0;
            if (inletMoles > 0)
            {
                relativeErrorPercent = Math.Abs(outletMoles - inletMoles) / inletMoles * 100.0;
            }
            else if (outletMoles > 0)
            {
                // If inlet is zero but outlet is not, this is an error
                relativeErrorPercent = double.PositiveInfinity;
            }

            // Validate against tolerance
            bool isValid = relativeErrorPercent <= tolerancePercent;

            return new ComponentMassBalance(
                compoundName,
                inletMoles,
                outletMoles,
                relativeErrorPercent,
                isValid);
        }

        /// <summary>
        /// Creates a valid ComponentMassBalance (for testing or explicit creation).
        /// </summary>
        /// <param name="compoundName">The name of the compound.</param>
        /// <param name="inletMoles">The total inlet moles (mol/sec).</param>
        /// <param name="outletMoles">The total outlet moles (mol/sec).</param>
        /// <returns>A new ComponentMassBalance instance marked as valid.</returns>
        public static ComponentMassBalance Valid(
            string compoundName,
            double inletMoles,
            double outletMoles)
        {
            double relativeErrorPercent = 0.0;
            if (inletMoles > 0)
            {
                relativeErrorPercent = Math.Abs(outletMoles - inletMoles) / inletMoles * 100.0;
            }

            return new ComponentMassBalance(
                compoundName,
                inletMoles,
                outletMoles,
                relativeErrorPercent,
                isValid: true);
        }

        /// <summary>
        /// Creates an invalid ComponentMassBalance (for testing or explicit creation).
        /// </summary>
        /// <param name="compoundName">The name of the compound.</param>
        /// <param name="inletMoles">The total inlet moles (mol/sec).</param>
        /// <param name="outletMoles">The total outlet moles (mol/sec).</param>
        /// <returns>A new ComponentMassBalance instance marked as invalid.</returns>
        public static ComponentMassBalance Invalid(
            string compoundName,
            double inletMoles,
            double outletMoles)
        {
            double relativeErrorPercent = 0.0;
            if (inletMoles > 0)
            {
                relativeErrorPercent = Math.Abs(outletMoles - inletMoles) / inletMoles * 100.0;
            }

            return new ComponentMassBalance(
                compoundName,
                inletMoles,
                outletMoles,
                relativeErrorPercent,
                isValid: false);
        }

        /// <summary>
        /// Gets the absolute error (mol/sec).
        /// </summary>
        public double AbsoluteError => Math.Abs(OutletMoles - InletMoles);

        /// <summary>
        /// Returns a string representation of the component mass balance.
        /// </summary>
        /// <returns>A string containing the mass balance details.</returns>
        public override string ToString()
        {
            var status = IsValid ? "✓ Valid" : "✗ Invalid";
            return $"{status} Mass Balance for '{CompoundName}': " +
                   $"Inlet={InletMoles:F6} mol/s, Outlet={OutletMoles:F6} mol/s, " +
                   $"Error={RelativeErrorPercent:F3}%";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current ComponentMassBalance.
        /// </summary>
        /// <param name="obj">The object to compare with the current ComponentMassBalance.</param>
        /// <returns>True if the specified object is equal to the current ComponentMassBalance; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is ComponentMassBalance other)
            {
                return CompoundName == other.CompoundName &&
                       InletMoles == other.InletMoles &&
                       OutletMoles == other.OutletMoles &&
                       IsValid == other.IsValid;
            }

            return false;
        }

        /// <summary>
        /// Returns a hash code for the current ComponentMassBalance.
        /// </summary>
        /// <returns>A hash code for the current ComponentMassBalance.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (CompoundName?.GetHashCode() ?? 0);
                hash = hash * 23 + InletMoles.GetHashCode();
                hash = hash * 23 + OutletMoles.GetHashCode();
                hash = hash * 23 + IsValid.GetHashCode();
                return hash;
            }
        }
    }
}
