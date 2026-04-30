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
using System.Collections.Generic;
using System.Linq;
using DwsimWorker.Models;

namespace DwsimWorker.Utilities
{
    /// <summary>
    /// Utility class for validating mass balances in process calculations.
    /// Compares inlet and outlet molar flows to ensure conservation of mass.
    /// </summary>
    public static class MassBalanceValidator
    {
        /// <summary>
        /// Default tolerance percentage for mass balance validation (1%).
        /// </summary>
        public const double DefaultTolerancePercent = 1.0;

        /// <summary>
        /// Validates the overall mass balance by comparing total inlet and outlet molar flows.
        /// </summary>
        /// <param name="inletStreams">Collection of inlet stream results.</param>
        /// <param name="outletStreams">Collection of outlet stream results.</param>
        /// <param name="tolerancePercent">The tolerance percentage for validation (default 1%).</param>
        /// <returns>A MassBalanceResult indicating whether the balance is valid.</returns>
        /// <exception cref="ArgumentNullException">Thrown when inlet or outlet streams are null.</exception>
        /// <exception cref="ArgumentException">Thrown when tolerance is negative.</exception>
        public static MassBalanceResult Validate(
            IEnumerable<StreamResult> inletStreams,
            IEnumerable<StreamResult> outletStreams,
            double tolerancePercent = DefaultTolerancePercent)
        {
            if (inletStreams == null)
                throw new ArgumentNullException(nameof(inletStreams));

            if (outletStreams == null)
                throw new ArgumentNullException(nameof(outletStreams));

            if (tolerancePercent < 0)
                throw new ArgumentException("Tolerance cannot be negative.", nameof(tolerancePercent));

            // Calculate total flows
            var inletList = inletStreams.ToList();
            var outletList = outletStreams.ToList();

            double totalInletFlow = inletList.Sum(s => s.MolarFlowMolPerSec);
            double totalOutletFlow = outletList.Sum(s => s.MolarFlowMolPerSec);

            // Create overall mass balance result (without per-component validation for now)
            return MassBalanceResult.Create(
                totalInletFlow,
                totalOutletFlow,
                tolerancePercent,
                null);
        }

        /// <summary>
        /// Validates mass balance for individual components using provided compound names.
        /// </summary>
        /// <param name="inletStreams">Collection of inlet stream results.</param>
        /// <param name="outletStreams">Collection of outlet stream results.</param>
        /// <param name="compoundNames">List of compound names in the order they appear in compositions.</param>
        /// <param name="tolerancePercent">The tolerance percentage for validation.</param>
        /// <returns>A list of ComponentMassBalance results, one for each component.</returns>
        public static IReadOnlyList<ComponentMassBalance> ValidateComponentBalances(
            IEnumerable<StreamResult> inletStreams,
            IEnumerable<StreamResult> outletStreams,
            IReadOnlyList<string> compoundNames,
            double tolerancePercent = DefaultTolerancePercent)
        {
            if (inletStreams == null || outletStreams == null || compoundNames == null)
                return new List<ComponentMassBalance>();

            var inletList = inletStreams.ToList();
            var outletList = outletStreams.ToList();

            if (compoundNames.Count == 0)
                return new List<ComponentMassBalance>();

            // Calculate component balances
            var componentBalances = new List<ComponentMassBalance>();

            for (int i = 0; i < compoundNames.Count; i++)
            {
                string compoundName = compoundNames[i];
                double inletMoles = CalculateComponentMolarFlow(inletList, i);
                double outletMoles = CalculateComponentMolarFlow(outletList, i);

                var balance = ComponentMassBalance.Create(
                    compoundName,
                    inletMoles,
                    outletMoles,
                    tolerancePercent);

                componentBalances.Add(balance);
            }

            return componentBalances.AsReadOnly();
        }

        /// <summary>
        /// Calculates the total molar flow for a specific component across multiple streams.
        /// </summary>
        /// <param name="streams">Collection of stream results.</param>
        /// <param name="componentIndex">The index of the component in the composition.</param>
        /// <returns>The total molar flow of the component in mol/sec.</returns>
        private static double CalculateComponentMolarFlow(
            IEnumerable<StreamResult> streams,
            int componentIndex)
        {
            double totalFlow = 0.0;

            foreach (var stream in streams)
            {
                if (stream.OverallComposition?.MoleFractions == null)
                    continue;

                if (componentIndex >= 0 && componentIndex < stream.OverallComposition.MoleFractions.Count)
                {
                    double moleFraction = stream.OverallComposition.MoleFractions[componentIndex];
                    // Component molar flow = total molar flow * mole fraction
                    totalFlow += stream.MolarFlowMolPerSec * moleFraction;
                }
            }

            return totalFlow;
        }

        /// <summary>
        /// Checks if the relative error is within the specified tolerance.
        /// </summary>
        /// <param name="inletValue">The inlet value.</param>
        /// <param name="outletValue">The outlet value.</param>
        /// <param name="tolerancePercent">The tolerance percentage.</param>
        /// <returns>True if within tolerance; otherwise, false.</returns>
        public static bool IsWithinTolerance(double inletValue, double outletValue, double tolerancePercent)
        {
            if (inletValue < 0 || outletValue < 0)
                return false;

            // If both are zero, that's valid
            if (inletValue == 0 && outletValue == 0)
                return true;

            // If inlet is zero but outlet is not, that's invalid
            if (inletValue == 0 && outletValue > 0)
                return false;

            // Calculate relative error percentage
            double relativeErrorPercent = Math.Abs(outletValue - inletValue) / inletValue * 100.0;

            return relativeErrorPercent <= tolerancePercent;
        }

        /// <summary>
        /// Calculates the relative error percentage between inlet and outlet values.
        /// </summary>
        /// <param name="inletValue">The inlet value.</param>
        /// <param name="outletValue">The outlet value.</param>
        /// <returns>The relative error as a percentage.</returns>
        public static double CalculateRelativeError(double inletValue, double outletValue)
        {
            if (inletValue < 0 || outletValue < 0)
                throw new ArgumentException("Values cannot be negative.");

            // If both are zero, error is 0%
            if (inletValue == 0 && outletValue == 0)
                return 0.0;

            // If inlet is zero but outlet is not, error is infinity
            if (inletValue == 0 && outletValue > 0)
                return double.PositiveInfinity;

            // Calculate relative error percentage
            return Math.Abs(outletValue - inletValue) / inletValue * 100.0;
        }
    }
}
