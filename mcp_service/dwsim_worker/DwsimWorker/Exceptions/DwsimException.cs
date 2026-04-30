// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace DwsimWorker.Exceptions
{
    /// <summary>
    /// Base exception class for all DWSIM worker exceptions.
    /// Provides a common exception type for catching all DWSIM-related errors.
    /// </summary>
    public abstract class DwsimException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DwsimException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        protected DwsimException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DwsimException"/> class with an inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        protected DwsimException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
