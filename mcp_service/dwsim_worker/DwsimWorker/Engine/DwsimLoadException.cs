// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Error codes for assembly loading failures.
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>
        /// Assembly file not found at the specified path.
        /// </summary>
        AssemblyNotFound,

        /// <summary>
        /// Assembly file found but failed to load (version conflict, corruption, etc.).
        /// </summary>
        AssemblyLoadFailure,

        /// <summary>
        /// Required dependency assembly is missing.
        /// </summary>
        DependencyMissing,

        /// <summary>
        /// Assembly loaded but types cannot be resolved.
        /// </summary>
        TypeLoadFailure,

        /// <summary>
        /// Assembly validation failed (object instantiation test failed).
        /// </summary>
        ValidationFailure
    }

    /// <summary>
    /// Exception thrown when DWSIM assembly loading fails.
    /// Provides structured error information including assembly name, attempted path, and error code.
    /// </summary>
    public sealed class DwsimLoadException : Exception
    {
        /// <summary>
        /// Gets the name of the assembly that failed to load.
        /// </summary>
        public string AssemblyName { get; }

        /// <summary>
        /// Gets the file path where the assembly was expected or attempted to load from.
        /// </summary>
        public string AttemptedPath { get; }

        /// <summary>
        /// Gets the error code categorizing the type of failure.
        /// </summary>
        public ErrorCode Code { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DwsimLoadException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="assemblyName">The name of the assembly that failed to load.</param>
        /// <param name="attemptedPath">The path where the assembly was expected or attempted to load from.</param>
        /// <param name="code">The error code categorizing the failure type.</param>
        public DwsimLoadException(string message, string assemblyName, string attemptedPath, ErrorCode code)
            : base(message)
        {
            AssemblyName = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
            AttemptedPath = attemptedPath ?? throw new ArgumentNullException(nameof(attemptedPath));
            Code = code;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DwsimLoadException"/> class with an inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="assemblyName">The name of the assembly that failed to load.</param>
        /// <param name="attemptedPath">The path where the assembly was expected or attempted to load from.</param>
        /// <param name="code">The error code categorizing the failure type.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public DwsimLoadException(
            string message,
            string assemblyName,
            string attemptedPath,
            ErrorCode code,
            Exception innerException)
            : base(message, innerException)
        {
            AssemblyName = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
            AttemptedPath = attemptedPath ?? throw new ArgumentNullException(nameof(attemptedPath));
            Code = code;
        }

        /// <summary>
        /// Returns a string representation of the exception including all context information.
        /// </summary>
        /// <returns>A string containing the exception details.</returns>
        public override string ToString()
        {
            return $"{base.ToString()}\nAssemblyName: {AssemblyName}\nAttemptedPath: {AttemptedPath}\nErrorCode: {Code}";
        }
    }
}
