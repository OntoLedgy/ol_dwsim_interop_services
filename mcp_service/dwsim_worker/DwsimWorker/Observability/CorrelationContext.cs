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
