using System.Collections.Generic;

namespace DwsimWorker.Contracts.CapeOpen
{
    /// <summary>
    /// Represents a CAPE-OPEN unit operation configuration.
    /// </summary>
    public sealed class UnitOperationDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the unit operation.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the unit operation name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the unit operation type.
        /// </summary>
        public string UnitType { get; set; }

        /// <summary>
        /// Gets or sets unit operation parameters in SI units.
        /// </summary>
        public Dictionary<string, double> Parameters { get; set; }
    }
}
