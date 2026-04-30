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
    /// Represents a CAPE-OPEN property package configuration.
    /// </summary>
    public sealed class PropertyPackageDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the property package.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the display name for the property package.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the property package type or model identifier.
        /// </summary>
        public string PackageType { get; set; }

        /// <summary>
        /// Gets or sets configuration parameters for the property package.
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; }
    }
}
