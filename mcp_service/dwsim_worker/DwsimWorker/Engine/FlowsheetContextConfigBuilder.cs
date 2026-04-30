// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Fluent builder for FlowsheetContextConfig.
    /// Provides a convenient way to construct configuration with custom settings.
    /// </summary>
    public sealed class FlowsheetContextConfigBuilder
    {
        private AssemblyLoaderConfig _assemblyConfig;
        private bool _validateAfterInit;
        private string _flowsheetName;

        /// <summary>
        /// Initializes a new instance of the <see cref="FlowsheetContextConfigBuilder"/> class with default values.
        /// </summary>
        public FlowsheetContextConfigBuilder()
        {
            // Start with defaults
            _assemblyConfig = AssemblyLoaderConfig.Default();
            _validateAfterInit = true;
            _flowsheetName = FlowsheetContextConfig.DefaultFlowsheetName;
        }

        /// <summary>
        /// Sets the assembly loader configuration.
        /// </summary>
        /// <param name="assemblyConfig">The assembly loader configuration.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when assemblyConfig is null.</exception>
        public FlowsheetContextConfigBuilder WithAssemblyConfig(AssemblyLoaderConfig assemblyConfig)
        {
            _assemblyConfig = assemblyConfig ?? throw new ArgumentNullException(nameof(assemblyConfig));
            return this;
        }

        /// <summary>
        /// Sets whether to validate the flowsheet after initialization.
        /// </summary>
        /// <param name="enabled">True to enable validation; false to disable.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public FlowsheetContextConfigBuilder WithValidation(bool enabled)
        {
            _validateAfterInit = enabled;
            _assemblyConfig = new AssemblyLoaderConfigBuilder()
                .WithAssemblyPath(_assemblyConfig.AssemblyPath)
                .WithValidationEnabled(enabled)
                .WithTimeout(_assemblyConfig.LoadTimeout)
                .WithRequiredAssemblies(_assemblyConfig.RequiredAssemblies)
                .Build();
            return this;
        }

        /// <summary>
        /// Sets the flowsheet name.
        /// </summary>
        /// <param name="name">The flowsheet name for identification and logging.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when name is null or whitespace.</exception>
        public FlowsheetContextConfigBuilder WithFlowsheetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Flowsheet name cannot be null or empty.", nameof(name));

            _flowsheetName = name;
            return this;
        }

        /// <summary>
        /// Sets the DWSIM assembly path (overrides auto-detection).
        /// </summary>
        /// <param name="assemblyPath">The path to DWSIM assemblies directory.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when assemblyPath is null or whitespace.</exception>
        public FlowsheetContextConfigBuilder WithAssemblyPath(string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentException("Assembly path cannot be null or empty.", nameof(assemblyPath));

            // Create new AssemblyLoaderConfig with custom path
            _assemblyConfig = new AssemblyLoaderConfigBuilder()
                .WithAssemblyPath(assemblyPath)
                .WithValidationEnabled(_assemblyConfig.ValidateAfterLoad)
                .WithTimeout(_assemblyConfig.LoadTimeout)
                .Build();

            return this;
        }

        /// <summary>
        /// Builds the FlowsheetContextConfig with the current settings.
        /// </summary>
        /// <returns>A new FlowsheetContextConfig instance.</returns>
        public FlowsheetContextConfig Build()
        {
            return FlowsheetContextConfig.Create(
                _assemblyConfig,
                _validateAfterInit,
                _flowsheetName);
        }
    }
}
