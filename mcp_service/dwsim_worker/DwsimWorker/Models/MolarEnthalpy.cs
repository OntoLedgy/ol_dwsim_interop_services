// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents the physical quantity of molar enthalpy.
    /// </summary>
    public sealed class MolarEnthalpy : PhysicalQuantities
    {
        /// <summary>
        /// Gets the name of this physical quantity.
        /// </summary>
        public override string QuantityName => "MolarEnthalpy";
    }
}
