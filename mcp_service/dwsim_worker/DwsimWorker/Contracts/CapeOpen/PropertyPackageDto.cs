// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
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
