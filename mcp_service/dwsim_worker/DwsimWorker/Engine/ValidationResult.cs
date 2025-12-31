using System;
using System.Collections.Generic;
using System.Linq;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Immutable data class representing the result of a DWSIM assembly validation operation.
    /// Contains success status, validated type names, and error details.
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the validation operation succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets a human-readable message describing the validation result.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the list of type names that were successfully instantiated during validation.
        /// Example: ["DWSIM.SharedClasses.Flowsheet", "DWSIM.Thermodynamics.Streams.MaterialStream"]
        /// </summary>
        public IReadOnlyList<string> ValidatedTypes { get; }

        /// <summary>
        /// Gets the exception that caused the validation failure (null if Success is true).
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationResult"/> class.
        /// Private constructor - use factory methods instead.
        /// </summary>
        /// <param name="success">Whether the validation succeeded.</param>
        /// <param name="message">Human-readable message.</param>
        /// <param name="validatedTypes">List of successfully validated type names.</param>
        /// <param name="error">Exception that caused failure (null if success).</param>
        private ValidationResult(
            bool success,
            string message,
            IReadOnlyList<string> validatedTypes,
            Exception error)
        {
            Success = success;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            ValidatedTypes = validatedTypes ?? new List<string>().AsReadOnly();
            Error = error;
        }

        /// <summary>
        /// Creates a success result with validated type names.
        /// </summary>
        /// <param name="validatedTypes">The list of type names that were successfully instantiated.</param>
        /// <returns>A ValidationResult indicating success.</returns>
        public static ValidationResult SuccessResult(IEnumerable<string> validatedTypes)
        {
            if (validatedTypes == null)
                throw new ArgumentNullException(nameof(validatedTypes));

            var typeList = validatedTypes.ToList().AsReadOnly();
            var message = typeList.Count > 0
                ? $"Successfully validated {typeList.Count} DWSIM type(s): {string.Join(", ", typeList)}"
                : "Validation completed successfully";

            return new ValidationResult(
                success: true,
                message: message,
                validatedTypes: typeList,
                error: null);
        }

        /// <summary>
        /// Creates a failure result with error details.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="error">The exception that caused the validation failure.</param>
        /// <returns>A ValidationResult indicating failure.</returns>
        public static ValidationResult FailureResult(string message, Exception error)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            return new ValidationResult(
                success: false,
                message: message,
                validatedTypes: new List<string>().AsReadOnly(),
                error: error);
        }

        /// <summary>
        /// Creates a partial failure result with some validated types.
        /// Used when validation partially succeeds (some types validate, others fail).
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="validatedTypes">List of types that were successfully validated.</param>
        /// <param name="error">The exception that caused the partial failure.</param>
        /// <returns>A ValidationResult indicating partial failure.</returns>
        public static ValidationResult PartialFailureResult(string message, IEnumerable<string> validatedTypes, Exception error)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            return new ValidationResult(
                success: false,
                message: message,
                validatedTypes: validatedTypes?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly(),
                error: error);
        }

        /// <summary>
        /// Creates a failure result with a single failed type name.
        /// </summary>
        /// <param name="typeName">The name of the type that failed to validate.</param>
        /// <param name="error">The exception that caused the failure.</param>
        /// <returns>A ValidationResult indicating failure.</returns>
        public static ValidationResult FailureResultForType(string typeName, Exception error)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("Type name cannot be null or empty", nameof(typeName));

            var message = $"Failed to validate type '{typeName}': {error?.Message ?? "Unknown error"}";

            return new ValidationResult(
                success: false,
                message: message,
                validatedTypes: new List<string>().AsReadOnly(),
                error: error);
        }

        /// <summary>
        /// Returns a string representation of the validation result.
        /// </summary>
        /// <returns>A string containing the result details.</returns>
        public override string ToString()
        {
            if (Success)
            {
                return $"Success: {Message}";
            }
            else
            {
                return $"Failure: {Message}\nError: {Error?.Message}";
            }
        }
    }
}
