// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Immutable data class representing the result of a DWSIM assembly loading operation.
    /// Contains success status, loaded assemblies, error details, and exit code.
    /// </summary>
    public sealed class LoadResult
    {
        /// <summary>
        /// Gets a value indicating whether the assembly loading operation succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets a human-readable message describing the result.
        /// Success example: "Successfully loaded 4 DWSIM assemblies"
        /// Failure example: "Failed to load DWSIM.Interfaces.dll: File not found"
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the list of successfully loaded assemblies with metadata.
        /// Empty if Success is false.
        /// </summary>
        public IReadOnlyList<AssemblyInfo> LoadedAssemblies { get; }

        /// <summary>
        /// Gets the exception that caused the failure (null if Success is true).
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Gets the exit code for Program.cs.
        /// 0 = Success
        /// 1 = Assembly loading failed
        /// 2 = Validation failed
        /// </summary>
        public int ExitCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoadResult"/> class.
        /// Private constructor - use factory methods instead.
        /// </summary>
        /// <param name="success">Whether the operation succeeded.</param>
        /// <param name="message">Human-readable message.</param>
        /// <param name="loadedAssemblies">List of loaded assemblies.</param>
        /// <param name="error">Exception that caused failure (null if success).</param>
        /// <param name="exitCode">Exit code for the application.</param>
        private LoadResult(
            bool success,
            string message,
            IReadOnlyList<AssemblyInfo> loadedAssemblies,
            Exception error,
            int exitCode)
        {
            Success = success;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            LoadedAssemblies = loadedAssemblies ?? new List<AssemblyInfo>().AsReadOnly();
            Error = error;
            ExitCode = exitCode;
        }

        /// <summary>
        /// Creates a success result with loaded assemblies.
        /// </summary>
        /// <param name="assemblies">The successfully loaded assemblies.</param>
        /// <returns>A LoadResult indicating success.</returns>
        public static LoadResult SuccessResult(IEnumerable<AssemblyInfo> assemblies)
        {
            if (assemblies == null)
                throw new ArgumentNullException(nameof(assemblies));

            var assemblyList = assemblies.ToList().AsReadOnly();
            var message = $"Successfully loaded {assemblyList.Count} DWSIM assembly(ies)";

            return new LoadResult(
                success: true,
                message: message,
                loadedAssemblies: assemblyList,
                error: null,
                exitCode: 0);
        }

        /// <summary>
        /// Creates a failure result with error details.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="error">The exception that caused the failure.</param>
        /// <returns>A LoadResult indicating failure.</returns>
        public static LoadResult FailureResult(string message, Exception error)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            // Determine exit code based on error type
            int exitCode = 1; // Default: assembly loading failed

            if (error is DwsimLoadException dwsimEx)
            {
                exitCode = dwsimEx.Code == ErrorCode.ValidationFailure ? 2 : 1;
            }

            return new LoadResult(
                success: false,
                message: message,
                loadedAssemblies: new List<AssemblyInfo>().AsReadOnly(),
                error: error,
                exitCode: exitCode);
        }

        /// <summary>
        /// Creates a failure result for validation failures.
        /// Preserves the list of successfully loaded assemblies even though validation failed.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="loadedAssemblies">List of assemblies that were successfully loaded before validation failed.</param>
        /// <param name="error">The exception that caused the validation failure.</param>
        /// <returns>A LoadResult indicating validation failure with exit code 2.</returns>
        public static LoadResult ValidationFailureResult(string message, IEnumerable<AssemblyInfo> loadedAssemblies, Exception error)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            return new LoadResult(
                success: false,
                message: message,
                loadedAssemblies: loadedAssemblies?.ToList().AsReadOnly() ?? new List<AssemblyInfo>().AsReadOnly(),
                error: error,
                exitCode: 2);
        }

        /// <summary>
        /// Returns a string representation of the load result.
        /// </summary>
        /// <returns>A string containing the result details.</returns>
        public override string ToString()
        {
            if (Success)
            {
                var assemblies = string.Join(", ", LoadedAssemblies.Select(a => $"{a.Name} v{a.Version}"));
                return $"Success: {Message}\nAssemblies: {assemblies}";
            }
            else
            {
                return $"Failure: {Message}\nExitCode: {ExitCode}\nError: {Error?.Message}";
            }
        }
    }
}
