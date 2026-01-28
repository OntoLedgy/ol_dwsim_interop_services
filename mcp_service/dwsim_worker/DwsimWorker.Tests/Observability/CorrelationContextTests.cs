using System;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;
using DwsimWorker.Observability;

namespace DwsimWorker.Tests.Observability
{
    public class CorrelationContextTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithNullOrEmptyRequestId_Throws(string requestId)
        {
            Assert.Throws<ArgumentException>(() => new CorrelationContext(requestId));
        }

        [Fact]
        public void Current_WithNoContext_ReturnsNull()
        {
            Assert.Null(CorrelationContext.Current);
        }

        [Fact]
        public void Begin_SetsCurrentAndReturnsScope()
        {
            using var scope = CorrelationContext.Begin("req-123", "session-1", "tool-x");

            var current = CorrelationContext.Current;

            Assert.NotNull(current);
            Assert.Equal("req-123", current.RequestId);
            Assert.Equal("session-1", current.SessionId);
            Assert.Equal("tool-x", current.ToolName);
        }

        [Fact]
        public void Dispose_RestoresPreviousContext()
        {
            using var outer = CorrelationContext.Begin("outer");
            var original = CorrelationContext.Current;

            var inner = CorrelationContext.Begin("inner");
            inner.Dispose();

            Assert.Same(original, CorrelationContext.Current);
        }

        [Fact]
        public void NestedBegin_DisposeRestoresOuterContext()
        {
            using var outer = CorrelationContext.Begin("outer", "session-outer", "tool-outer");
            using (CorrelationContext.Begin("inner", "session-inner", "tool-inner"))
            {
                Assert.Equal("inner", CorrelationContext.Current?.RequestId);
            }

            Assert.Equal("outer", CorrelationContext.Current?.RequestId);
            Assert.Equal("session-outer", CorrelationContext.Current?.SessionId);
            Assert.Equal("tool-outer", CorrelationContext.Current?.ToolName);
        }

        [Fact]
        public async Task AsyncContext_IsolatedAcrossTasks()
        {
            var task1 = Task.Run(async () =>
            {
                using var scope = CorrelationContext.Begin("req-1");
                await Task.Delay(10);
                return CorrelationContext.Current?.RequestId;
            });

            var task2 = Task.Run(async () =>
            {
                using var scope = CorrelationContext.Begin("req-2");
                await Task.Delay(10);
                return CorrelationContext.Current?.RequestId;
            });

            var results = await Task.WhenAll(task1, task2);

            Assert.Contains("req-1", results);
            Assert.Contains("req-2", results);
            Assert.Null(CorrelationContext.Current);
        }

        [Fact]
        public void CorrelationEnricher_AddsPropertiesWhenContextExists()
        {
            var logEvent = CreateLogEvent();
            var propertyFactory = new TestPropertyFactory();
            var enricher = new CorrelationEnricher();

            using (CorrelationContext.Begin("req-77", "session-77", "tool-77"))
            {
                enricher.Enrich(logEvent, propertyFactory);
            }

            Assert.True(logEvent.Properties.ContainsKey("RequestId"));
            Assert.True(logEvent.Properties.ContainsKey("SessionId"));
            Assert.True(logEvent.Properties.ContainsKey("ToolName"));

            Assert.Equal("req-77", GetScalarValue(logEvent.Properties["RequestId"]));
            Assert.Equal("session-77", GetScalarValue(logEvent.Properties["SessionId"]));
            Assert.Equal("tool-77", GetScalarValue(logEvent.Properties["ToolName"]));
        }

        [Fact]
        public void CorrelationEnricher_WithNullContext_DoesNotModifyEvent()
        {
            var logEvent = CreateLogEvent();
            var propertyFactory = new TestPropertyFactory();
            var enricher = new CorrelationEnricher();

            enricher.Enrich(logEvent, propertyFactory);

            Assert.Empty(logEvent.Properties);
        }

        private static LogEvent CreateLogEvent()
        {
            return new LogEvent(
                DateTimeOffset.UtcNow,
                LogEventLevel.Information,
                exception: null,
                messageTemplate: new MessageTemplate("", Array.Empty<MessageTemplateToken>()),
                properties: Array.Empty<LogEventProperty>());
        }

        private static string? GetScalarValue(LogEventPropertyValue value)
        {
            return value is ScalarValue scalar ? scalar.Value?.ToString() : null;
        }

        private sealed class TestPropertyFactory : ILogEventPropertyFactory
        {
            public LogEventProperty CreateProperty(string name, object value, bool destructureObjects = false)
            {
                return new LogEventProperty(name, new ScalarValue(value));
            }
        }
    }
}
