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

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Immutable configuration class for FlowsheetContext behavior.
    /// Use FlowsheetContextConfigBuilder to construct instances.
    /// </summary>
    public sealed class FlowsheetContextConfig
    {
        /// <summary>
        /// Default flowsheet name.
        /// </summary>
        public const string DefaultFlowsheetName = "Flowsheet1";

        /// <summary>
        /// Gets the assembly loader configuration for DWSIM assemblies.
        /// </summary>
        public AssemblyLoaderConfig AssemblyConfig { get; }

        /// <summary>
        /// Gets a value indicating whether to validate the flowsheet after initialization.
        /// Default: true
        /// </summary>
        public bool ValidateAfterInit { get; }

        /// <summary>
        /// Gets the name of the flowsheet for identification and logging.
        /// Default: "Flowsheet1"
        /// </summary>
        public string FlowsheetName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FlowsheetContextConfig"/> class.
        /// Private constructor - use CreateDefault() factory method or builder instead.
        /// </summary>
        /// <param name="assemblyConfig">The assembly loader configuration.</param>
        /// <param name="validateAfterInit">Whether to validate after initialization.</param>
        /// <param name="flowsheetName">The flowsheet name.</param>
        private FlowsheetContextConfig(
            AssemblyLoaderConfig assemblyConfig,
            bool validateAfterInit,
            string flowsheetName)
        {
            AssemblyConfig = assemblyConfig ?? throw new ArgumentNullException(nameof(assemblyConfig));
            ValidateAfterInit = validateAfterInit;
            FlowsheetName = flowsheetName ?? throw new ArgumentNullException(nameof(flowsheetName));
        }

        /// <summary>
        /// Creates a FlowsheetContextConfig with default settings.
        /// </summary>
        /// <returns>A new FlowsheetContextConfig with default values.</returns>
        public static FlowsheetContextConfig CreateDefault()
        {
            return new FlowsheetContextConfig(
                assemblyConfig: AssemblyLoaderConfig.Default(),
                validateAfterInit: true,
                flowsheetName: DefaultFlowsheetName);
        }

        /// <summary>
        /// Creates a FlowsheetContextConfig with specified settings.
        /// Internal method used by FlowsheetContextConfigBuilder.
        /// </summary>
        /// <param name="assemblyConfig">The assembly loader configuration.</param>
        /// <param name="validateAfterInit">Whether to validate after initialization.</param>
        /// <param name="flowsheetName">The flowsheet name.</param>
        /// <returns>A new FlowsheetContextConfig with the specified values.</returns>
        internal static FlowsheetContextConfig Create(
            AssemblyLoaderConfig assemblyConfig,
            bool validateAfterInit,
            string flowsheetName)
        {
            return new FlowsheetContextConfig(assemblyConfig, validateAfterInit, flowsheetName);
        }
    }
}
