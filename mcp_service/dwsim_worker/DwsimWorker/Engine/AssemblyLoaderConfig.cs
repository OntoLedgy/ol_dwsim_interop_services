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

using System;
using System.Collections.Generic;
using System.Linq;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Immutable configuration class for AssemblyLoader behavior.
    /// Use AssemblyLoaderConfigBuilder to construct instances.
    /// </summary>
    public sealed class AssemblyLoaderConfig
    {
        /// <summary>
        /// Default required DWSIM assemblies to load.
        /// </summary>
        public static readonly string[] DefaultRequiredAssemblies = new[]
        {
            "DWSIM.Interfaces",
            "DWSIM.Thermodynamics",
            "DWSIM.SharedClasses",
            "DWSIM.GlobalSettings",  // Required for solver configuration (CalculatorActivated, SolverMode, etc.)
            "DWSIM.FlowsheetBase",
            "DWSIM.UnitOperations",
            "DWSIM.Drawing.SkiaSharp",
            "DWSIM.UI.Desktop.Shared",
            "DWSIM",  // Main DWSIM executable assembly
            "CapeOpen"
            // Note: DWSIM.Automation not loaded - it's x64 only and we handle UI exceptions instead
        };

        /// <summary>
        /// Gets the path to DWSIM assemblies directory.
        /// If null, PathResolver will auto-detect using environment variables and defaults.
        /// </summary>
        public string AssemblyPath { get; }

        /// <summary>
        /// Gets a value indicating whether to validate assemblies after loading (instantiate test objects).
        /// Default: true
        /// </summary>
        public bool ValidateAfterLoad { get; }

        /// <summary>
        /// Gets the maximum time to wait for assembly loading to complete.
        /// Default: 30 seconds
        /// </summary>
        public TimeSpan LoadTimeout { get; }

        /// <summary>
        /// Gets the list of required assembly names to load (without .dll extension).
        /// Default: ["DWSIM.Interfaces", "DWSIM.Thermodynamics", "DWSIM.SharedClasses", "CapeOpen"]
        /// </summary>
        public IReadOnlyList<string> RequiredAssemblies { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyLoaderConfig"/> class.
        /// Private constructor - use Create() factory method and builder instead.
        /// </summary>
        /// <param name="assemblyPath">Path to DWSIM assemblies directory.</param>
        /// <param name="validateAfterLoad">Whether to validate assemblies after loading.</param>
        /// <param name="loadTimeout">Maximum time for assembly loading.</param>
        /// <param name="requiredAssemblies">List of required assembly names.</param>
        internal AssemblyLoaderConfig(
            string assemblyPath,
            bool validateAfterLoad,
            TimeSpan loadTimeout,
            IReadOnlyList<string> requiredAssemblies)
        {
            AssemblyPath = assemblyPath;
            ValidateAfterLoad = validateAfterLoad;
            LoadTimeout = loadTimeout;
            RequiredAssemblies = requiredAssemblies ?? DefaultRequiredAssemblies.ToList().AsReadOnly();
        }

        /// <summary>
        /// Creates a new builder for constructing AssemblyLoaderConfig instances.
        /// </summary>
        /// <returns>A new AssemblyLoaderConfigBuilder with default settings.</returns>
        public static AssemblyLoaderConfigBuilder Create()
        {
            return new AssemblyLoaderConfigBuilder();
        }

        /// <summary>
        /// Creates a default configuration instance.
        /// </summary>
        /// <returns>An AssemblyLoaderConfig with default settings.</returns>
        public static AssemblyLoaderConfig Default()
        {
            return new AssemblyLoaderConfig(
                assemblyPath: null,
                validateAfterLoad: true,
                loadTimeout: TimeSpan.FromSeconds(30),
                requiredAssemblies: DefaultRequiredAssemblies.ToList().AsReadOnly());
        }
    }
}
