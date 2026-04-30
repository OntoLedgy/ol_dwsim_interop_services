// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace DwsimWorker.Models
{
    /// <summary>
    /// Defines the severity level of solver diagnostic messages.
    /// </summary>
    public enum SolverMessageLevel
    {
        /// <summary>
        /// Debug-level message for detailed diagnostics.
        /// </summary>
        Debug = 0,

        /// <summary>
        /// Informational message about solver progress.
        /// </summary>
        Info = 1,

        /// <summary>
        /// Warning message indicating potential issues.
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Error message indicating solver failure or critical issues.
        /// </summary>
        Error = 3
    }
}
