// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// This file is part of the OntoLedgy Thermodynamics Architecture and is
// dual-licensed:
//
//   1. Open source under the GNU Affero General Public License v3.0 or
//      later (AGPL-3.0-or-later). See the LICENSE file in the repository
//      root for the full licence text and NOTICE for attribution.
//   2. Commercial under a separate proprietary licence offered by
//      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
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
