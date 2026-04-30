// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents the complete calculated properties for a material stream.
    /// This is an immutable model capturing all stream results including phase-specific data.
    /// </summary>
    public sealed class StreamResult
    {
        /// <summary>
        /// Gets the unique identifier of the stream.
        /// </summary>
        public string StreamId { get; }

        /// <summary>
        /// Gets the name of the stream.
        /// </summary>
        public string StreamName { get; }

        /// <summary>
        /// Gets the temperature in Kelvin.
        /// </summary>
        public double TemperatureK { get; }

        /// <summary>
        /// Gets the pressure in Pascals.
        /// </summary>
        public double PressurePa { get; }

        /// <summary>
        /// Gets the total molar flow rate in mol/sec.
        /// </summary>
        public double MolarFlowMolPerSec { get; }

        /// <summary>
        /// Gets the total mass flow rate in kg/sec.
        /// </summary>
        public double MassFlowKgPerSec { get; }

        /// <summary>
        /// Gets the overall composition of the stream.
        /// </summary>
        public Composition OverallComposition { get; }

        /// <summary>
        /// Gets the vapor fraction (0 to 1).
        /// </summary>
        public double VaporFraction { get; }

        /// <summary>
        /// Gets the liquid fraction (0 to 1).
        /// </summary>
        public double LiquidFraction { get; }

        /// <summary>
        /// Gets the dictionary of phase properties keyed by phase name.
        /// Phase names typically include "Vapor", "Liquid1", "Liquid2", "Overall".
        /// </summary>
        public IReadOnlyDictionary<string, PhaseProperties> Phases { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamResult"/> class without phase details.
        /// </summary>
        /// <param name="streamId">The unique identifier of the stream.</param>
        /// <param name="streamName">The name of the stream.</param>
        /// <param name="temperatureK">The temperature in Kelvin.</param>
        /// <param name="pressurePa">The pressure in Pascals.</param>
        /// <param name="molarFlowMolPerSec">The total molar flow rate in mol/sec.</param>
        /// <param name="massFlowKgPerSec">The total mass flow rate in kg/sec.</param>
        /// <param name="overallComposition">The overall composition of the stream.</param>
        /// <param name="vaporFraction">The vapor fraction (0 to 1).</param>
        /// <param name="liquidFraction">The liquid fraction (0 to 1).</param>
        public StreamResult(
            string streamId,
            string streamName,
            double temperatureK,
            double pressurePa,
            double molarFlowMolPerSec,
            double massFlowKgPerSec,
            Composition overallComposition,
            double vaporFraction,
            double liquidFraction)
            : this(streamId, streamName, temperatureK, pressurePa, molarFlowMolPerSec, massFlowKgPerSec,
                  overallComposition, vaporFraction, liquidFraction, new Dictionary<string, PhaseProperties>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamResult"/> class with phase details.
        /// </summary>
        /// <param name="streamId">The unique identifier of the stream.</param>
        /// <param name="streamName">The name of the stream.</param>
        /// <param name="temperatureK">The temperature in Kelvin.</param>
        /// <param name="pressurePa">The pressure in Pascals.</param>
        /// <param name="molarFlowMolPerSec">The total molar flow rate in mol/sec.</param>
        /// <param name="massFlowKgPerSec">The total mass flow rate in kg/sec.</param>
        /// <param name="overallComposition">The overall composition of the stream.</param>
        /// <param name="vaporFraction">The vapor fraction (0 to 1).</param>
        /// <param name="liquidFraction">The liquid fraction (0 to 1).</param>
        /// <param name="phases">The dictionary of phase properties keyed by phase name.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        /// <exception cref="ArgumentException">Thrown when values are out of valid ranges.</exception>
        public StreamResult(
            string streamId,
            string streamName,
            double temperatureK,
            double pressurePa,
            double molarFlowMolPerSec,
            double massFlowKgPerSec,
            Composition overallComposition,
            double vaporFraction,
            double liquidFraction,
            IDictionary<string, PhaseProperties> phases)
        {
            if (string.IsNullOrWhiteSpace(streamId))
                throw new ArgumentNullException(nameof(streamId), "Stream ID cannot be null or empty.");

            if (string.IsNullOrWhiteSpace(streamName))
                throw new ArgumentNullException(nameof(streamName), "Stream name cannot be null or empty.");

            if (overallComposition == null)
                throw new ArgumentNullException(nameof(overallComposition), "Overall composition cannot be null.");

            if (temperatureK < 0)
                throw new ArgumentException("Temperature cannot be negative.", nameof(temperatureK));

            if (pressurePa < 0)
                throw new ArgumentException("Pressure cannot be negative.", nameof(pressurePa));

            if (molarFlowMolPerSec < 0)
                throw new ArgumentException("Molar flow rate cannot be negative.", nameof(molarFlowMolPerSec));

            if (massFlowKgPerSec < 0)
                throw new ArgumentException("Mass flow rate cannot be negative.", nameof(massFlowKgPerSec));

            if (vaporFraction < 0 || vaporFraction > 1)
                throw new ArgumentException("Vapor fraction must be between 0 and 1.", nameof(vaporFraction));

            if (liquidFraction < 0 || liquidFraction > 1)
                throw new ArgumentException("Liquid fraction must be between 0 and 1.", nameof(liquidFraction));

            // Validate that vapor + liquid fractions don't exceed 1 (with small tolerance for rounding)
            if (vaporFraction + liquidFraction > 1.0 + 1e-6)
                throw new ArgumentException($"Vapor fraction ({vaporFraction}) + Liquid fraction ({liquidFraction}) cannot exceed 1.0");

            StreamId = streamId;
            StreamName = streamName;
            TemperatureK = temperatureK;
            PressurePa = pressurePa;
            MolarFlowMolPerSec = molarFlowMolPerSec;
            MassFlowKgPerSec = massFlowKgPerSec;
            OverallComposition = overallComposition;
            VaporFraction = vaporFraction;
            LiquidFraction = liquidFraction;

            // Create immutable copy of phases dictionary
            if (phases == null)
            {
                Phases = new Dictionary<string, PhaseProperties>();
            }
            else
            {
                Phases = new Dictionary<string, PhaseProperties>(phases);
            }
        }

        /// <summary>
        /// Gets the phase properties for a specific phase name.
        /// </summary>
        /// <param name="phaseName">The name of the phase (e.g., "Vapor", "Liquid1", "Liquid2").</param>
        /// <returns>The phase properties if found; otherwise, null.</returns>
        public PhaseProperties GetPhase(string phaseName)
        {
            if (string.IsNullOrWhiteSpace(phaseName))
                return null;

            return Phases.ContainsKey(phaseName) ? Phases[phaseName] : null;
        }

        /// <summary>
        /// Checks if the stream has a specific phase.
        /// </summary>
        /// <param name="phaseName">The name of the phase.</param>
        /// <returns>True if the phase exists; otherwise, false.</returns>
        public bool HasPhase(string phaseName)
        {
            return !string.IsNullOrWhiteSpace(phaseName) && Phases.ContainsKey(phaseName);
        }

        /// <summary>
        /// Returns a string representation of the stream result.
        /// </summary>
        /// <returns>A string containing the stream details.</returns>
        public override string ToString()
        {
            var phaseList = string.Join(", ", Phases.Keys);
            return $"Stream '{StreamName}' (ID: {StreamId}): " +
                   $"T={TemperatureK:F2}K, P={PressurePa:F0}Pa, " +
                   $"Flow={MolarFlowMolPerSec:F4}mol/s ({MassFlowKgPerSec:F4}kg/s), " +
                   $"Vapor={VaporFraction:F3}, Liquid={LiquidFraction:F3}, " +
                   $"Phases=[{phaseList}]";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current StreamResult.
        /// </summary>
        /// <param name="obj">The object to compare with the current StreamResult.</param>
        /// <returns>True if the specified object is equal to the current StreamResult; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is StreamResult other)
            {
                return StreamId == other.StreamId &&
                       StreamName == other.StreamName &&
                       TemperatureK == other.TemperatureK &&
                       PressurePa == other.PressurePa &&
                       MolarFlowMolPerSec == other.MolarFlowMolPerSec &&
                       MassFlowKgPerSec == other.MassFlowKgPerSec &&
                       VaporFraction == other.VaporFraction &&
                       LiquidFraction == other.LiquidFraction &&
                       Phases.Count == other.Phases.Count &&
                       Phases.Keys.All(k => other.Phases.ContainsKey(k));
            }

            return false;
        }

        /// <summary>
        /// Returns a hash code for the current StreamResult.
        /// </summary>
        /// <returns>A hash code for the current StreamResult.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (StreamId?.GetHashCode() ?? 0);
                hash = hash * 23 + (StreamName?.GetHashCode() ?? 0);
                hash = hash * 23 + TemperatureK.GetHashCode();
                hash = hash * 23 + PressurePa.GetHashCode();
                hash = hash * 23 + MolarFlowMolPerSec.GetHashCode();
                hash = hash * 23 + MassFlowKgPerSec.GetHashCode();
                hash = hash * 23 + VaporFraction.GetHashCode();
                hash = hash * 23 + LiquidFraction.GetHashCode();
                hash = hash * 23 + Phases.Count.GetHashCode();
                return hash;
            }
        }
    }
}
