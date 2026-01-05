using System;

namespace DwsimWorker.Exceptions
{
    /// <summary>
    /// Exception thrown when setting a property on a DWSIM object fails.
    /// Captures the property name and value that caused the failure.
    /// </summary>
    public sealed class PropertySetException : DwsimException
    {
        /// <summary>
        /// Gets the name of the property that failed to set.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Gets the value that was attempted to be set.
        /// </summary>
        public object ProvidedValue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertySetException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="propertyName">The name of the property that failed to set.</param>
        /// <param name="providedValue">The value that was attempted to be set.</param>
        public PropertySetException(string message, string propertyName, object providedValue)
            : base(message)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            ProvidedValue = providedValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertySetException"/> class with an inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="propertyName">The name of the property that failed to set.</param>
        /// <param name="providedValue">The value that was attempted to be set.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public PropertySetException(string message, string propertyName, object providedValue, Exception innerException)
            : base(message, innerException)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            ProvidedValue = providedValue;
        }

        /// <summary>
        /// Returns a string representation of the exception including property context.
        /// </summary>
        /// <returns>A string containing the exception details.</returns>
        public override string ToString()
        {
            return $"{base.ToString()}\nPropertyName: {PropertyName}\nProvidedValue: {ProvidedValue}";
        }
    }
}
