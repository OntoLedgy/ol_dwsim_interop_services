// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Immutable data class containing metadata about a loaded DWSIM assembly.
    /// </summary>
    public sealed class AssemblyInfo
    {
        /// <summary>
        /// Gets the assembly name (e.g., "DWSIM.Interfaces").
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the assembly version (e.g., "6.5.3.0").
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Gets the full file path where the assembly was loaded from.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the timestamp when the assembly was loaded.
        /// </summary>
        public DateTime LoadedAt { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyInfo"/> class.
        /// </summary>
        /// <param name="name">The assembly name.</param>
        /// <param name="version">The assembly version.</param>
        /// <param name="path">The full file path where the assembly was loaded from.</param>
        /// <param name="loadedAt">The timestamp when the assembly was loaded.</param>
        public AssemblyInfo(string name, Version version, string path, DateTime loadedAt)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            LoadedAt = loadedAt;
        }

        /// <summary>
        /// Returns a string representation of the assembly information.
        /// </summary>
        /// <returns>A string containing assembly name, version, and path.</returns>
        public override string ToString()
        {
            return $"{Name} v{Version} ({Path})";
        }
    }
}
