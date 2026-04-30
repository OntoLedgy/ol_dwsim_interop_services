// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Builder class for constructing AssemblyLoaderConfig instances using fluent API.
    /// </summary>
    public sealed class AssemblyLoaderConfigBuilder
    {
        private string _assemblyPath;
        private bool _validateAfterLoad = true;
        private TimeSpan _loadTimeout = TimeSpan.FromSeconds(30);
        private List<string> _requiredAssemblies;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyLoaderConfigBuilder"/> class.
        /// Internal constructor - use AssemblyLoaderConfig.Create() instead.
        /// </summary>
        internal AssemblyLoaderConfigBuilder()
        {
            _requiredAssemblies = AssemblyLoaderConfig.DefaultRequiredAssemblies.ToList();
        }

        /// <summary>
        /// Sets the path to DWSIM assemblies directory.
        /// If not set, PathResolver will auto-detect the path.
        /// </summary>
        /// <param name="path">The directory path containing DWSIM assemblies.</param>
        /// <returns>This builder instance for fluent chaining.</returns>
        public AssemblyLoaderConfigBuilder WithAssemblyPath(string path)
        {
            _assemblyPath = path;
            return this;
        }

        /// <summary>
        /// Enables or disables assembly validation after loading.
        /// Validation instantiates test objects to verify assemblies are functional.
        /// </summary>
        /// <param name="enabled">True to enable validation; false to disable.</param>
        /// <returns>This builder instance for fluent chaining.</returns>
        public AssemblyLoaderConfigBuilder WithValidationEnabled(bool enabled)
        {
            _validateAfterLoad = enabled;
            return this;
        }

        /// <summary>
        /// Sets the maximum time to wait for assembly loading to complete.
        /// </summary>
        /// <param name="timeout">The timeout duration.</param>
        /// <returns>This builder instance for fluent chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when timeout is negative or zero.</exception>
        public AssemblyLoaderConfigBuilder WithTimeout(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero");

            _loadTimeout = timeout;
            return this;
        }

        /// <summary>
        /// Sets the maximum time to wait for assembly loading to complete (in seconds).
        /// </summary>
        /// <param name="seconds">The timeout in seconds.</param>
        /// <returns>This builder instance for fluent chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when seconds is negative or zero.</exception>
        public AssemblyLoaderConfigBuilder WithTimeoutSeconds(int seconds)
        {
            if (seconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Timeout seconds must be greater than zero");

            _loadTimeout = TimeSpan.FromSeconds(seconds);
            return this;
        }

        /// <summary>
        /// Sets the list of required assemblies to load.
        /// Replaces the default list.
        /// </summary>
        /// <param name="assemblies">The assembly names (without .dll extension).</param>
        /// <returns>This builder instance for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when assemblies is null.</exception>
        /// <exception cref="ArgumentException">Thrown when assemblies is empty.</exception>
        public AssemblyLoaderConfigBuilder WithRequiredAssemblies(IEnumerable<string> assemblies)
        {
            if (assemblies == null)
                throw new ArgumentNullException(nameof(assemblies));

            var assemblyList = assemblies.ToList();
            if (assemblyList.Count == 0)
                throw new ArgumentException("Required assemblies list cannot be empty", nameof(assemblies));

            _requiredAssemblies = assemblyList;
            return this;
        }

        /// <summary>
        /// Adds additional required assemblies to the default list.
        /// </summary>
        /// <param name="assemblies">The assembly names to add (without .dll extension).</param>
        /// <returns>This builder instance for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when assemblies is null.</exception>
        public AssemblyLoaderConfigBuilder AddRequiredAssemblies(params string[] assemblies)
        {
            if (assemblies == null)
                throw new ArgumentNullException(nameof(assemblies));

            _requiredAssemblies.AddRange(assemblies.Where(a => !string.IsNullOrWhiteSpace(a)));
            return this;
        }

        /// <summary>
        /// Builds the AssemblyLoaderConfig instance with the configured settings.
        /// </summary>
        /// <returns>A new immutable AssemblyLoaderConfig instance.</returns>
        public AssemblyLoaderConfig Build()
        {
            return new AssemblyLoaderConfig(
                assemblyPath: _assemblyPath,
                validateAfterLoad: _validateAfterLoad,
                loadTimeout: _loadTimeout,
                requiredAssemblies: _requiredAssemblies.AsReadOnly());
        }
    }
}
