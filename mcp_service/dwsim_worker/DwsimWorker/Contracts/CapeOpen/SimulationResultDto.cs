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

using System.Collections.Generic;
using Newtonsoft.Json;

namespace DwsimWorker.Contracts.CapeOpen
{
    /// <summary>
    /// Represents the result of a simulation calculation for interop transfer.
    /// </summary>
    public sealed class SimulationResultDto
    {
        /// <summary>
        /// Gets the overall simulation status (e.g., converged, failed, timeout).
        /// </summary>
        public string Status { get; }

        /// <summary>
        /// Gets the convergence state (e.g., Converged, NotConverged, Error).
        /// </summary>
        public string ConvergenceState { get; }

        /// <summary>
        /// Gets the elapsed time in milliseconds.
        /// </summary>
        public double ElapsedMilliseconds { get; }

        /// <summary>
        /// Gets the calculated stream results.
        /// </summary>
        public IReadOnlyList<MaterialStreamDto> StreamResults { get; }

        /// <summary>
        /// Gets the solver diagnostic messages.
        /// </summary>
        public IReadOnlyList<string> Messages { get; }

        /// <summary>
        /// Gets the mass balance validation status.
        /// </summary>
        public bool? MassBalanceValid { get; }

        /// <summary>
        /// Gets the mass balance error percentage.
        /// </summary>
        public double? MassBalanceErrorPercent { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimulationResultDto"/> class.
        /// </summary>
        /// <param name="status">The overall simulation status.</param>
        /// <param name="convergenceState">The convergence state.</param>
        /// <param name="elapsedMilliseconds">Elapsed time in milliseconds.</param>
        /// <param name="streamResults">Calculated stream results.</param>
        /// <param name="messages">Solver diagnostic messages.</param>
        /// <param name="massBalanceValid">Mass balance validation status.</param>
        /// <param name="massBalanceErrorPercent">Mass balance error percentage.</param>
        [JsonConstructor]
        public SimulationResultDto(
            string status,
            string convergenceState,
            double elapsedMilliseconds,
            IReadOnlyList<MaterialStreamDto> streamResults,
            IReadOnlyList<string> messages,
            bool? massBalanceValid,
            double? massBalanceErrorPercent)
        {
            Status = status ?? string.Empty;
            ConvergenceState = convergenceState ?? string.Empty;
            ElapsedMilliseconds = elapsedMilliseconds;
            StreamResults = streamResults != null
                ? new List<MaterialStreamDto>(streamResults)
                : new List<MaterialStreamDto>();
            Messages = messages != null
                ? new List<string>(messages)
                : new List<string>();
            MassBalanceValid = massBalanceValid;
            MassBalanceErrorPercent = massBalanceErrorPercent;
        }
    }
}
