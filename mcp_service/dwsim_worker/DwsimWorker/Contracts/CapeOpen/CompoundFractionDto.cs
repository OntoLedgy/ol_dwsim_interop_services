// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace DwsimWorker.Contracts.CapeOpen
{
    /// <summary>
    /// Represents a compound fraction within a phase or overall composition.
    /// </summary>
    public sealed class CompoundFractionDto
    {
        /// <summary>
        /// Gets or sets the compound identifier or name.
        /// </summary>
        public string Compound { get; set; }

        /// <summary>
        /// Gets or sets the mole fraction for the compound.
        /// </summary>
        public double MoleFraction { get; set; }
    }
}
