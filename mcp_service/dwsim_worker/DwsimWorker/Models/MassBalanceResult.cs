using System;
using System.Collections.Generic;
using System.Linq;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents the overall mass balance validation result for a calculation.
    /// This is an immutable model following the result pattern.
    /// </summary>
    public sealed class MassBalanceResult
    {
        /// <summary>
        /// Gets a value indicating whether the overall mass balance is valid.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the total inlet molar flow rate (mol/sec).
        /// </summary>
        public double InletMolarFlow { get; }

        /// <summary>
        /// Gets the total outlet molar flow rate (mol/sec).
        /// </summary>
        public double OutletMolarFlow { get; }

        /// <summary>
        /// Gets the absolute error in molar flow (mol/sec).
        /// Calculated as: |Outlet - Inlet|
        /// </summary>
        public double AbsoluteError { get; }

        /// <summary>
        /// Gets the relative error as a percentage.
        /// Calculated as: |Outlet - Inlet| / Inlet * 100
        /// </summary>
        public double RelativeErrorPercent { get; }

        /// <summary>
        /// Gets the tolerance percentage used for validation.
        /// </summary>
        public double TolerancePercent { get; }

        /// <summary>
        /// Gets the per-component mass balance results.
        /// </summary>
        public IReadOnlyList<ComponentMassBalance> ComponentBalances { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MassBalanceResult"/> class.
        /// Private constructor - use factory methods instead.
        /// </summary>
        /// <param name="isValid">Whether the overall mass balance is valid.</param>
        /// <param name="inletMolarFlow">The total inlet molar flow rate (mol/sec).</param>
        /// <param name="outletMolarFlow">The total outlet molar flow rate (mol/sec).</param>
        /// <param name="absoluteError">The absolute error in molar flow (mol/sec).</param>
        /// <param name="relativeErrorPercent">The relative error as a percentage.</param>
        /// <param name="tolerancePercent">The tolerance percentage used for validation.</param>
        /// <param name="componentBalances">The per-component mass balance results.</param>
        private MassBalanceResult(
            bool isValid,
            double inletMolarFlow,
            double outletMolarFlow,
            double absoluteError,
            double relativeErrorPercent,
            double tolerancePercent,
            IReadOnlyList<ComponentMassBalance> componentBalances)
        {
            IsValid = isValid;
            InletMolarFlow = inletMolarFlow;
            OutletMolarFlow = outletMolarFlow;
            AbsoluteError = absoluteError;
            RelativeErrorPercent = relativeErrorPercent;
            TolerancePercent = tolerancePercent;
            ComponentBalances = componentBalances ?? new List<ComponentMassBalance>();
        }

        /// <summary>
        /// Creates a MassBalanceResult with automatic validation based on flows and tolerance.
        /// </summary>
        /// <param name="inletMolarFlow">The total inlet molar flow rate (mol/sec).</param>
        /// <param name="outletMolarFlow">The total outlet molar flow rate (mol/sec).</param>
        /// <param name="tolerancePercent">The tolerance for validation (default 1%).</param>
        /// <param name="componentBalances">Optional per-component mass balance results.</param>
        /// <returns>A new MassBalanceResult instance.</returns>
        /// <exception cref="ArgumentException">Thrown when flow values are negative.</exception>
        public static MassBalanceResult Create(
            double inletMolarFlow,
            double outletMolarFlow,
            double tolerancePercent = 1.0,
            IReadOnlyList<ComponentMassBalance> componentBalances = null)
        {
            if (inletMolarFlow < 0)
                throw new ArgumentException("Inlet molar flow cannot be negative.", nameof(inletMolarFlow));

            if (outletMolarFlow < 0)
                throw new ArgumentException("Outlet molar flow cannot be negative.", nameof(outletMolarFlow));

            if (tolerancePercent < 0)
                throw new ArgumentException("Tolerance cannot be negative.", nameof(tolerancePercent));

            // Calculate errors
            double absoluteError = Math.Abs(outletMolarFlow - inletMolarFlow);
            double relativeErrorPercent = 0.0;

            if (inletMolarFlow > 0)
            {
                relativeErrorPercent = (absoluteError / inletMolarFlow) * 100.0;
            }
            else if (outletMolarFlow > 0)
            {
                // If inlet is zero but outlet is not, this is an error
                relativeErrorPercent = double.PositiveInfinity;
            }

            // Validate: overall and all components must be valid
            bool isValid = relativeErrorPercent <= tolerancePercent;

            if (componentBalances != null && componentBalances.Any())
            {
                // Overall is valid only if all components are valid
                isValid = isValid && componentBalances.All(c => c.IsValid);
            }

            return new MassBalanceResult(
                isValid,
                inletMolarFlow,
                outletMolarFlow,
                absoluteError,
                relativeErrorPercent,
                tolerancePercent,
                componentBalances);
        }

        /// <summary>
        /// Creates a valid MassBalanceResult (for testing or explicit creation).
        /// </summary>
        /// <param name="inletMolarFlow">The total inlet molar flow rate (mol/sec).</param>
        /// <param name="outletMolarFlow">The total outlet molar flow rate (mol/sec).</param>
        /// <param name="tolerancePercent">The tolerance percentage.</param>
        /// <param name="componentBalances">Optional per-component mass balance results.</param>
        /// <returns>A new MassBalanceResult instance marked as valid.</returns>
        public static MassBalanceResult Valid(
            double inletMolarFlow,
            double outletMolarFlow,
            double tolerancePercent = 1.0,
            IReadOnlyList<ComponentMassBalance> componentBalances = null)
        {
            double absoluteError = Math.Abs(outletMolarFlow - inletMolarFlow);
            double relativeErrorPercent = 0.0;

            if (inletMolarFlow > 0)
            {
                relativeErrorPercent = (absoluteError / inletMolarFlow) * 100.0;
            }

            return new MassBalanceResult(
                isValid: true,
                inletMolarFlow,
                outletMolarFlow,
                absoluteError,
                relativeErrorPercent,
                tolerancePercent,
                componentBalances);
        }

        /// <summary>
        /// Creates an invalid MassBalanceResult (for testing or explicit creation).
        /// </summary>
        /// <param name="inletMolarFlow">The total inlet molar flow rate (mol/sec).</param>
        /// <param name="outletMolarFlow">The total outlet molar flow rate (mol/sec).</param>
        /// <param name="tolerancePercent">The tolerance percentage.</param>
        /// <param name="componentBalances">Optional per-component mass balance results.</param>
        /// <returns>A new MassBalanceResult instance marked as invalid.</returns>
        public static MassBalanceResult Invalid(
            double inletMolarFlow,
            double outletMolarFlow,
            double tolerancePercent = 1.0,
            IReadOnlyList<ComponentMassBalance> componentBalances = null)
        {
            double absoluteError = Math.Abs(outletMolarFlow - inletMolarFlow);
            double relativeErrorPercent = 0.0;

            if (inletMolarFlow > 0)
            {
                relativeErrorPercent = (absoluteError / inletMolarFlow) * 100.0;
            }

            return new MassBalanceResult(
                isValid: false,
                inletMolarFlow,
                outletMolarFlow,
                absoluteError,
                relativeErrorPercent,
                tolerancePercent,
                componentBalances);
        }

        /// <summary>
        /// Gets the number of components in the mass balance.
        /// </summary>
        public int ComponentCount => ComponentBalances?.Count ?? 0;

        /// <summary>
        /// Gets the number of valid component balances.
        /// </summary>
        public int ValidComponentCount => ComponentBalances?.Count(c => c.IsValid) ?? 0;

        /// <summary>
        /// Gets the number of invalid component balances.
        /// </summary>
        public int InvalidComponentCount => ComponentBalances?.Count(c => !c.IsValid) ?? 0;

        /// <summary>
        /// Returns a string representation of the mass balance result.
        /// </summary>
        /// <returns>A string containing the mass balance details.</returns>
        public override string ToString()
        {
            var status = IsValid ? "✓ Valid" : "✗ Invalid";
            var componentInfo = ComponentCount > 0
                ? $", Components: {ValidComponentCount}/{ComponentCount} valid"
                : "";

            return $"{status} Mass Balance: " +
                   $"Inlet={InletMolarFlow:F4} mol/s, Outlet={OutletMolarFlow:F4} mol/s, " +
                   $"Error={RelativeErrorPercent:F3}% (Tolerance={TolerancePercent:F3}%)" +
                   componentInfo;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current MassBalanceResult.
        /// </summary>
        /// <param name="obj">The object to compare with the current MassBalanceResult.</param>
        /// <returns>True if the specified object is equal to the current MassBalanceResult; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is MassBalanceResult other)
            {
                return IsValid == other.IsValid &&
                       InletMolarFlow == other.InletMolarFlow &&
                       OutletMolarFlow == other.OutletMolarFlow &&
                       TolerancePercent == other.TolerancePercent &&
                       ComponentCount == other.ComponentCount;
            }

            return false;
        }

        /// <summary>
        /// Returns a hash code for the current MassBalanceResult.
        /// </summary>
        /// <returns>A hash code for the current MassBalanceResult.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + IsValid.GetHashCode();
                hash = hash * 23 + InletMolarFlow.GetHashCode();
                hash = hash * 23 + OutletMolarFlow.GetHashCode();
                hash = hash * 23 + TolerancePercent.GetHashCode();
                hash = hash * 23 + ComponentCount.GetHashCode();
                return hash;
            }
        }
    }
}
