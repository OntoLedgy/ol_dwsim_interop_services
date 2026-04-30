// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Result of a property set operation on a DWSIM object (stream, unit, etc.).
    /// Follows the result pattern to avoid throwing exceptions for expected failures.
    /// </summary>
    public sealed class PropertySetResult
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
        /// Gets the ID of the object that was created or modified (e.g., stream ID, unit ID).
        /// Only set for create operations that return an ID.
        /// </summary>
        public string ObjectId { get; }

        /// <summary>
        /// Gets the name of the property that was set.
        /// Only set for property set operations (not for create operations).
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Gets the data returned from a get operation (e.g., property value).
        /// Only set for get operations.
        /// </summary>
        public object Data { get; }

        /// <summary>
        /// Gets the exception that caused the failure, if any.
        /// Null if the operation succeeded or failed without an exception.
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertySetResult"/> class.
        /// Private constructor - use static factory methods instead.
        /// </summary>
        /// <param name="success">Whether the operation succeeded.</param>
        /// <param name="message">A message describing the result.</param>
        /// <param name="objectId">The ID of the created/modified object.</param>
        /// <param name="propertyName">The name of the property that was set.</param>
        /// <param name="data">The data returned from a get operation.</param>
        /// <param name="error">The exception that caused the failure, if any.</param>
        private PropertySetResult(
            bool success,
            string message,
            string objectId = null,
            string propertyName = null,
            object data = null,
            Exception error = null)
        {
            Success = success;
            Message = message ?? string.Empty;
            ObjectId = objectId;
            PropertyName = propertyName;
            Data = data;
            Error = error;
        }

        /// <summary>
        /// Creates a success result for an object creation operation.
        /// </summary>
        /// <param name="objectId">The ID of the created object (e.g., stream ID, unit ID).</param>
        /// <param name="message">Optional success message. If not provided, a default message is generated.</param>
        /// <returns>A PropertySetResult indicating success.</returns>
        public static PropertySetResult SuccessResultForCreate(string objectId, string message = null)
        {
            return new PropertySetResult(
                success: true,
                message: message ?? $"Object created successfully with ID: {objectId}",
                objectId: objectId);
        }

        /// <summary>
        /// Creates a success result for a property set operation.
        /// </summary>
        /// <param name="objectId">The ID of the object whose property was set.</param>
        /// <param name="propertyName">The name of the property that was set.</param>
        /// <param name="message">Optional success message. If not provided, a default message is generated.</param>
        /// <returns>A PropertySetResult indicating success.</returns>
        public static PropertySetResult SuccessResultForPropertySet(
            string objectId,
            string propertyName,
            string message = null)
        {
            return new PropertySetResult(
                success: true,
                message: message ?? $"Property '{propertyName}' set successfully on object '{objectId}'",
                objectId: objectId,
                propertyName: propertyName);
        }

        /// <summary>
        /// Creates a success result for an operation that doesn't return an ID.
        /// </summary>
        /// <param name="message">The success message.</param>
        /// <returns>A PropertySetResult indicating success.</returns>
        public static PropertySetResult SuccessResult(string message)
        {
            return new PropertySetResult(
                success: true,
                message: message ?? "Operation completed successfully");
        }

        /// <summary>
        /// Creates a success result with data returned from a get operation.
        /// </summary>
        /// <param name="data">The data being returned (e.g., property value).</param>
        /// <param name="message">Optional success message.</param>
        /// <returns>A PropertySetResult indicating success with data.</returns>
        public static PropertySetResult SuccessResultWithData(object data, string message = null)
        {
            return new PropertySetResult(
                success: true,
                message: message ?? "Data retrieved successfully",
                data: data);
        }

        /// <summary>
        /// Creates a failure result with an error message.
        /// </summary>
        /// <param name="message">The error message describing what went wrong.</param>
        /// <param name="error">The exception that caused the failure, if any.</param>
        /// <returns>A PropertySetResult indicating failure.</returns>
        public static PropertySetResult FailureResult(string message, Exception error = null)
        {
            return new PropertySetResult(
                success: false,
                message: message ?? "Operation failed",
                error: error);
        }

        /// <summary>
        /// Creates a failure result with context about the object and property.
        /// </summary>
        /// <param name="message">The error message describing what went wrong.</param>
        /// <param name="objectId">The ID of the object being operated on.</param>
        /// <param name="propertyName">The name of the property being set, if applicable.</param>
        /// <param name="error">The exception that caused the failure, if any.</param>
        /// <returns>A PropertySetResult indicating failure.</returns>
        public static PropertySetResult FailureResultWithContext(
            string message,
            string objectId,
            string propertyName = null,
            Exception error = null)
        {
            return new PropertySetResult(
                success: false,
                message: message ?? "Operation failed",
                objectId: objectId,
                propertyName: propertyName,
                error: error);
        }
    }
}
