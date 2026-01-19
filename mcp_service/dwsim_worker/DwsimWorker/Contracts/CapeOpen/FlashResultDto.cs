using System.Collections.Generic;

namespace DwsimWorker.Contracts.CapeOpen
{
    /// <summary>
    /// Represents a CAPE-OPEN flash calculation result.
    /// </summary>
    public sealed class FlashResultDto
    {
        /// <summary>
        /// Gets or sets the flash calculation type (e.g., TP, PH, PS).
        /// </summary>
        public string CalculationType { get; set; }

        /// <summary>
        /// Gets or sets the temperature in Kelvin.
        /// </summary>
        public double TemperatureK { get; set; }

        /// <summary>
        /// Gets or sets the pressure in Pascals.
        /// </summary>
        public double PressurePa { get; set; }

        /// <summary>
        /// Gets or sets the phase results.
        /// </summary>
        public List<PhaseDto> Phases { get; set; }

        /// <summary>
        /// Gets or sets whether the flash calculation converged.
        /// </summary>
        public bool Converged { get; set; }
    }
}
