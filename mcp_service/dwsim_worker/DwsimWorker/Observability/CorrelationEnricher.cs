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

using Serilog.Core;
using Serilog.Events;

namespace DwsimWorker.Observability
{
    /// <summary>
    /// Enriches Serilog events with correlation identifiers from the current context.
    /// </summary>
    public sealed class CorrelationEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var context = CorrelationContext.Current;
            if (context == null)
            {
                return;
            }

            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("RequestId", context.RequestId));

            if (!string.IsNullOrWhiteSpace(context.SessionId))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("SessionId", context.SessionId));
            }

            if (!string.IsNullOrWhiteSpace(context.ToolName))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ToolName", context.ToolName));
            }
        }
    }
}
