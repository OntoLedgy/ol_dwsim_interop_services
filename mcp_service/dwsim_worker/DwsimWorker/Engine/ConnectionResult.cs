using System;
using DwsimWorker.Models;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Result of a stream connection operation.
    /// Follows the result pattern to avoid throwing exceptions for expected failures.
    /// </summary>
    public sealed class ConnectionResult
    {
        /// <summary>
        /// Gets a value indicating whether the operation succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets a message describing the result (error message if failed, success message if succeeded).
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the connection information if the operation succeeded.
        /// Null if the operation failed.
        /// </summary>
        public ConnectionInfo Connection { get; }

        /// <summary>
        /// Gets the data returned from an operation (e.g., list of connections).
        /// Only set for get operations that return collections.
        /// </summary>
        public object Data { get; }

        /// <summary>
        /// Gets the exception that caused the failure, if any.
        /// Null if the operation succeeded or failed without an exception.
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionResult"/> class.
        /// Private constructor - use static factory methods instead.
        /// </summary>
        /// <param name="success">Whether the operation succeeded.</param>
        /// <param name="message">A message describing the result.</param>
        /// <param name="connection">The connection information if successful.</param>
        /// <param name="data">The data returned from a get operation.</param>
        /// <param name="error">The exception that caused the failure, if any.</param>
        private ConnectionResult(
            bool success,
            string message,
            ConnectionInfo connection = null,
            object data = null,
            Exception error = null)
        {
            Success = success;
            Message = message ?? string.Empty;
            Connection = connection;
            Data = data;
            Error = error;
        }

        /// <summary>
        /// Creates a success result with connection information.
        /// </summary>
        /// <param name="connection">The connection information.</param>
        /// <param name="message">Optional success message. If not provided, a default message is generated.</param>
        /// <returns>A ConnectionResult indicating success.</returns>
        /// <exception cref="ArgumentNullException">Thrown when connection is null.</exception>
        public static ConnectionResult SuccessResult(ConnectionInfo connection, string message = null)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            return new ConnectionResult(
                success: true,
                message: message ?? $"Stream '{connection.StreamId}' connected successfully to unit '{connection.UnitId}' port '{connection.PortName}'",
                connection: connection);
        }

        /// <summary>
        /// Creates a success result for a disconnection operation.
        /// </summary>
        /// <param name="message">The success message.</param>
        /// <returns>A ConnectionResult indicating success.</returns>
        public static ConnectionResult SuccessResultForDisconnect(string message)
        {
            return new ConnectionResult(
                success: true,
                message: message ?? "Stream disconnected successfully");
        }

        /// <summary>
        /// Creates a success result with data returned from a get operation.
        /// </summary>
        /// <param name="data">The data being returned (e.g., list of connections).</param>
        /// <param name="message">Optional success message.</param>
        /// <returns>A ConnectionResult indicating success with data.</returns>
        public static ConnectionResult SuccessResultWithData(object data, string message = null)
        {
            return new ConnectionResult(
                success: true,
                message: message ?? "Data retrieved successfully",
                data: data);
        }

        /// <summary>
        /// Creates a failure result with an error message.
        /// </summary>
        /// <param name="message">The error message describing what went wrong.</param>
        /// <param name="error">The exception that caused the failure, if any.</param>
        /// <returns>A ConnectionResult indicating failure.</returns>
        public static ConnectionResult FailureResult(string message, Exception error = null)
        {
            return new ConnectionResult(
                success: false,
                message: message ?? "Connection operation failed",
                error: error);
        }

        /// <summary>
        /// Creates a failure result with context about the attempted connection.
        /// </summary>
        /// <param name="message">The error message describing what went wrong.</param>
        /// <param name="streamId">The ID of the stream being connected.</param>
        /// <param name="unitId">The ID of the unit operation.</param>
        /// <param name="portName">The name of the port.</param>
        /// <param name="error">The exception that caused the failure, if any.</param>
        /// <returns>A ConnectionResult indicating failure.</returns>
        public static ConnectionResult FailureResultWithContext(
            string message,
            string streamId,
            string unitId,
            string portName,
            Exception error = null)
        {
            string contextMessage = $"{message} (Stream: '{streamId}', Unit: '{unitId}', Port: '{portName}')";
            return new ConnectionResult(
                success: false,
                message: contextMessage,
                error: error);
        }
    }
}
