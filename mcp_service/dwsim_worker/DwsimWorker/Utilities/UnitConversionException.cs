// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

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
