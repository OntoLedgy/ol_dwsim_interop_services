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
using System.Threading;

namespace DwsimWorker.Observability
{
    /// <summary>
    /// Ambient correlation context for tracing requests across async boundaries.
    /// </summary>
    public sealed class CorrelationContext
    {
        private static readonly AsyncLocal<CorrelationContext?> CurrentContext = new AsyncLocal<CorrelationContext?>();

        public string RequestId { get; }
        public string? SessionId { get; }
        public string? ToolName { get; }

        public CorrelationContext(string requestId, string? sessionId = null, string? toolName = null)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new ArgumentException("requestId must be provided", nameof(requestId));
            }

            RequestId = requestId;
            SessionId = sessionId;
            ToolName = toolName;
        }

        /// <summary>
        /// Gets the current ambient correlation context, if any.
        /// </summary>
        public static CorrelationContext? Current => CurrentContext.Value;

        /// <summary>
        /// Begins a new correlation scope and returns an IDisposable that restores the previous context.
        /// </summary>
        public static IDisposable Begin(string requestId, string? sessionId = null, string? toolName = null)
        {
            return Begin(new CorrelationContext(requestId, sessionId, toolName));
        }

        /// <summary>
        /// Begins a new correlation scope using the supplied context.
        /// </summary>
        public static IDisposable Begin(CorrelationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var prior = CurrentContext.Value;
            CurrentContext.Value = context;
            return new Scope(prior);
        }

        private sealed class Scope : IDisposable
        {
            private readonly CorrelationContext? prior;
            private bool disposed;

            public Scope(CorrelationContext? priorContext)
            {
                prior = priorContext;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                CurrentContext.Value = prior;
            }
        }
    }
}
