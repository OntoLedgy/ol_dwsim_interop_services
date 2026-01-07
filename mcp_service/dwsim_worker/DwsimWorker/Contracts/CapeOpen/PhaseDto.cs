using System.Collections.Generic;

namespace DwsimWorker.Contracts.CapeOpen
{
    /// <summary>
    /// Represents a phase within a material stream or flash result.
    /// </summary>
    public sealed class PhaseDto
    {
        /// <summary>
        /// Gets or sets the phase label (e.g., Vapor, Liquid1, Liquid2).
        /// </summary>
        public string PhaseLabel { get; set; }

        /// <summary>
        /// Gets or sets the phase fraction (0-1).
        /// </summary>
        public double PhaseFraction { get; set; }

        /// <summary>
        /// Gets or sets the phase composition as compound fractions.
        /// </summary>
        public List<CompoundFractionDto> Composition { get; set; }

        /// <summary>
        /// Gets or sets additional phase properties keyed by CAPE-OPEN property name.
        /// </summary>
        public Dictionary<string, double> Properties { get; set; }
    }
}
