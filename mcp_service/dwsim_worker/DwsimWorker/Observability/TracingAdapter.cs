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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Remoting.Messaging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DwsimWorker.Observability
{
    /// <summary>
    /// Provides OpenTelemetry tracing integration for the DWSIM worker.
    /// </summary>
    public static class TracingAdapter
    {
        private static readonly object Sync = new object();
        private static bool configured;
        private static bool enabled;
        private static string activitySourceName = "DwsimWorker";
        private static ActivitySource? activitySource;
        private static TracerProvider? tracerProvider;

        /// <summary>
        /// Configures OpenTelemetry tracing using environment variables and provided defaults.
        /// </summary>
        /// <param name="serviceName">Service name used in resource attributes.</param>
        /// <param name="exporterEndpoint">Default exporter endpoint when not specified in environment.</param>
        public static void Configure(string serviceName, string exporterEndpoint)
        {
            if (configured)
            {
                return;
            }

            lock (Sync)
            {
                if (configured)
                {
                    return;
                }

                var tracingEnabled = ParseBool(Environment.GetEnvironmentVariable("DWSIM_TRACING_ENABLED"));
                if (!tracingEnabled)
                {
                    configured = true;
                    enabled = false;
                    return;
                }

                enabled = true;
                activitySourceName = string.IsNullOrWhiteSpace(serviceName) ? "DwsimWorker" : serviceName;
                activitySource = new ActivitySource(activitySourceName);

                var exporter = (Environment.GetEnvironmentVariable("DWSIM_TRACING_EXPORTER") ?? string.Empty).Trim().ToLowerInvariant();
                var endpoint = Environment.GetEnvironmentVariable("DWSIM_TRACING_ENDPOINT");
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    endpoint = exporterEndpoint;
                }

                var sampleRate = ParseDouble(Environment.GetEnvironmentVariable("DWSIM_TRACING_SAMPLE_RATE"), 1.0);
                if (sampleRate < 0.0)
                {
                    sampleRate = 0.0;
                }
                else if (sampleRate > 1.0)
                {
                    sampleRate = 1.0;
                }

                var builder = Sdk.CreateTracerProviderBuilder()
                    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(sampleRate)))
                    .AddSource(activitySourceName)
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(activitySourceName));

                switch (exporter)
                {
                    case "jaeger":
                        // Jaeger supports OTLP natively. Use OTLP exporter with Jaeger's OTLP endpoint.
                        // Default Jaeger OTLP endpoints: 4317 (gRPC) or 4318 (HTTP)
                        builder.AddOtlpExporter(options =>
                        {
                            if (TryCreateEndpoint(endpoint, out var uri))
                            {
                                options.Endpoint = uri;
                            }
                        });
                        break;
                    case "otlp":
                        builder.AddOtlpExporter(options =>
                        {
                            if (TryCreateEndpoint(endpoint, out var uri))
                            {
                                options.Endpoint = uri;
                            }
                        });
                        break;
                    case "console":
                        builder.AddConsoleExporter();
                        break;
                    case "none":
                    case "":
                        break;
                    default:
                        builder.AddOtlpExporter(options =>
                        {
                            if (TryCreateEndpoint(endpoint, out var uri))
                            {
                                options.Endpoint = uri;
                            }
                        });
                        break;
                }

                tracerProvider = builder.Build();
                configured = true;
            }
        }

        /// <summary>
        /// Starts a new span with the given operation name.
        /// </summary>
        /// <param name="operationName">Name of the operation to trace.</param>
        /// <param name="attributes">Optional span attributes.</param>
        /// <returns>An <see cref="IDisposable"/> that ends the span when disposed.</returns>
        public static IDisposable StartSpan(string operationName, IDictionary<string, object> attributes = null)
        {
            if (!enabled || activitySource == null)
            {
                return NoopScope.Instance;
            }

            if (string.IsNullOrWhiteSpace(operationName))
            {
                operationName = "operation";
            }

            Activity? activity = null;
            if (Activity.Current == null && TryGetParentContext(out var parentContext))
            {
                activity = activitySource.StartActivity(operationName, ActivityKind.Internal, parentContext);
            }
            else
            {
                activity = activitySource.StartActivity(operationName, ActivityKind.Internal);
            }

            if (activity == null)
            {
                return NoopScope.Instance;
            }

            ApplyCorrelationAttributes(activity);
            ApplyAttributes(activity, attributes);

            return new SpanScope(activity);
        }

        /// <summary>
        /// Adds an attribute to the current span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="value">Attribute value.</param>
        public static void SetSpanAttribute(string key, object value)
        {
            if (!enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            Activity.Current?.SetTag(key, value);
        }

        /// <summary>
        /// Records an exception on the current span and marks it as error.
        /// </summary>
        /// <param name="ex">Exception to record.</param>
        public static void RecordException(Exception ex)
        {
            if (!enabled || ex == null)
            {
                return;
            }

            var activity = Activity.Current;
            if (activity == null)
            {
                return;
            }

            activity.RecordException(ex);
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.SetTag("exception.message", ex.Message);
        }

        private static void ApplyCorrelationAttributes(Activity activity)
        {
            var context = CorrelationContext.Current;
            if (context == null)
            {
                return;
            }

            activity.SetTag("dwsim.request_id", context.RequestId);

            if (!string.IsNullOrWhiteSpace(context.SessionId))
            {
                activity.SetTag("dwsim.session_id", context.SessionId);
            }

            if (!string.IsNullOrWhiteSpace(context.ToolName))
            {
                activity.SetTag("dwsim.tool_name", context.ToolName);
            }
        }

        private static void ApplyAttributes(Activity activity, IDictionary<string, object> attributes)
        {
            if (attributes == null)
            {
                return;
            }

            foreach (var pair in attributes)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                activity.SetTag(pair.Key, pair.Value);
            }
        }

        private static bool TryGetParentContext(out ActivityContext parentContext)
        {
            parentContext = default;

            if (TryGetTraceContextFromCallContext(out var traceParent, out var traceState) ||
                TryGetTraceContextFromEnvironment(out traceParent, out traceState))
            {
                return ActivityContext.TryParse(traceParent, traceState, out parentContext);
            }

            return false;
        }

        private static bool TryGetTraceContextFromEnvironment(out string traceParent, out string traceState)
        {
            traceParent = Environment.GetEnvironmentVariable("TRACEPARENT")
                ?? Environment.GetEnvironmentVariable("DWSIM_TRACEPARENT")
                ?? string.Empty;
            traceState = Environment.GetEnvironmentVariable("TRACESTATE")
                ?? Environment.GetEnvironmentVariable("DWSIM_TRACESTATE")
                ?? string.Empty;

            return !string.IsNullOrWhiteSpace(traceParent);
        }

        private static bool TryGetTraceContextFromCallContext(out string traceParent, out string traceState)
        {
            traceParent = string.Empty;
            traceState = string.Empty;

            try
            {
                traceParent = (CallContext.LogicalGetData("traceparent") as string)
                    ?? (CallContext.LogicalGetData("TRACEPARENT") as string)
                    ?? (CallContext.LogicalGetData("DWSIM_TRACEPARENT") as string)
                    ?? string.Empty;
                traceState = (CallContext.LogicalGetData("tracestate") as string)
                    ?? (CallContext.LogicalGetData("TRACESTATE") as string)
                    ?? (CallContext.LogicalGetData("DWSIM_TRACESTATE") as string)
                    ?? string.Empty;
            }
            catch
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(traceParent);
        }

        private static bool TryCreateEndpoint(string endpoint, out Uri uri)
        {
            uri = null;
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return false;
            }

            return Uri.TryCreate(endpoint, UriKind.Absolute, out uri);
        }

        private static bool ParseBool(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (bool.TryParse(value, out var parsed))
            {
                return parsed;
            }

            value = value.Trim();
            return value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static double ParseDouble(string value, double fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private sealed class SpanScope : IDisposable
        {
            private Activity? activity;
            private bool disposed;

            public SpanScope(Activity activity)
            {
                this.activity = activity;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                activity?.Stop();
                activity = null;
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new NoopScope();

            public void Dispose()
            {
            }
        }
    }
}
