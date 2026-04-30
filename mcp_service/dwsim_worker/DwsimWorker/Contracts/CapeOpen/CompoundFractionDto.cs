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
