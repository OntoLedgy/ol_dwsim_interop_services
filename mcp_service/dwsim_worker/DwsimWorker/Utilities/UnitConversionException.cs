using System;

namespace DwsimWorker.Utilities
{
    /// <summary>
    /// Exception thrown when a unit conversion cannot be performed.
    /// </summary>
    public sealed class UnitConversionException : Exception
    {
        public UnitConversionException(string message)
            : base(message)
        {
        }

        public UnitConversionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
