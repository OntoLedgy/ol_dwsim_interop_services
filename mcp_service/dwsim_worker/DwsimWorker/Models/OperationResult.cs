using System;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents the result of an operation with optional data and error details.
    /// </summary>
    /// <typeparam name="T">The data type returned on success.</typeparam>
    public sealed class OperationResult<T>
    {
        /// <summary>
        /// Gets a value indicating whether the operation succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets a human-readable message describing the result.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the data returned by the operation (default if not successful).
        /// </summary>
        public T Data { get; }

        /// <summary>
        /// Gets the exception that caused the failure (null if Success is true).
        /// </summary>
        public Exception Error { get; }

        private OperationResult(bool success, string message, T data, Exception error)
        {
            Success = success;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Data = data;
            Error = error;
        }

        /// <summary>
        /// Creates a successful operation result.
        /// </summary>
        /// <param name="data">The data returned by the operation.</param>
        /// <param name="message">Optional success message.</param>
        /// <returns>An OperationResult indicating success.</returns>
        public static OperationResult<T> SuccessResult(T data, string message = null)
        {
            var resolvedMessage = string.IsNullOrWhiteSpace(message) ? "Operation completed successfully." : message;
            return new OperationResult<T>(true, resolvedMessage, data, null);
        }

        /// <summary>
        /// Creates a failure operation result.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="error">The exception that caused the failure.</param>
        /// <returns>An OperationResult indicating failure.</returns>
        /// <exception cref="ArgumentException">Thrown when message is null or whitespace.</exception>
        public static OperationResult<T> FailureResult(string message, Exception error = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            return new OperationResult<T>(false, message, default, error);
        }
    }
}
