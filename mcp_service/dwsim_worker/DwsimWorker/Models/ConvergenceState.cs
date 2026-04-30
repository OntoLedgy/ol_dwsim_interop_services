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

namespace DwsimWorker.Models
{
    /// <summary>
    /// Represents the convergence state of a solver calculation.
    /// </summary>
    public enum ConvergenceState
    {
        /// <summary>
        /// Calculation has not been started.
        /// </summary>
        NotStarted,

        /// <summary>
        /// Calculation is currently in progress.
        /// </summary>
        InProgress,

        /// <summary>
        /// Calculation converged successfully.
        /// </summary>
        Converged,

        /// <summary>
        /// Calculation did not converge within iteration limits.
        /// </summary>
        NotConverged,

        /// <summary>
        /// Calculation encountered an error.
        /// </summary>
        Error
    }
}
