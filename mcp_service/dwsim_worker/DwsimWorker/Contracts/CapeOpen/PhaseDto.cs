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
