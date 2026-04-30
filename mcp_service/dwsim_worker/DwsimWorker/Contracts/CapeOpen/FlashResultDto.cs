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
