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
