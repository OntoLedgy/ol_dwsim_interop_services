using System.Collections.Generic;

namespace DwsimWorker.Contracts.CapeOpen
{
    /// <summary>
    /// Represents a CAPE-OPEN material stream in SI units.
    /// </summary>
    public sealed class MaterialStreamDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the stream.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the stream name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the temperature in Kelvin.
        /// </summary>
        public double TemperatureK { get; set; }

        /// <summary>
        /// Gets or sets the pressure in Pascals.
        /// </summary>
        public double PressurePa { get; set; }

        /// <summary>
        /// Gets or sets the total molar flow in mol/s.
        /// </summary>
        public double TotalMolarFlowMolPerS { get; set; }

        /// <summary>
        /// Gets or sets the phase data for the stream.
        /// </summary>
        public List<PhaseDto> Phases { get; set; }
    }
}
