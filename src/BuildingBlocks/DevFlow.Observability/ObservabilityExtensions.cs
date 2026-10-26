using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DevFlow.Observability;

public static class ObservabilityExtensions
{
    public static TBuilder AddDevFlowObservability<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Register once, even when multiple host extensions call this method.
        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(ObservabilityOptions)))
        {
            return builder;
        }

        var options = new ObservabilityOptions();
        builder.Configuration.GetSection("Observability").Bind(options);
        builder.Services.AddSingleton(options);
        if (!options.Enabled)
        {
            return builder;
        }

        string endpointValue = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? options.Endpoint;
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            // Invalid telemetry configuration must not prevent the host from starting.
            return builder;
        }

        string serviceName = builder.Configuration["OTEL_SERVICE_NAME"]
            ?? options.ServiceName ?? builder.Environment.ApplicationName;
        var resource = ResourceBuilder.CreateEmpty()
            .AddService(
                serviceName,
                serviceNamespace: "DevFlow",
                serviceVersion: options.ServiceVersion,
                autoGenerateServiceInstanceId: false)
            .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]);
        int timeout = Math.Clamp(options.ExportTimeoutMilliseconds, 500, 10000);

        void ConfigureExporter(OtlpExporterOptions exporter)
        {
            exporter.Endpoint = endpoint;
            exporter.Protocol = OtlpExportProtocol.Grpc;
            exporter.TimeoutMilliseconds = timeout;
            exporter.ExportProcessorType = ExportProcessorType.Batch;
            exporter.BatchExportProcessorOptions.MaxQueueSize = 2048;
            exporter.BatchExportProcessorOptions.MaxExportBatchSize = 512;
            exporter.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 1000;
            exporter.BatchExportProcessorOptions.ExporterTimeoutMilliseconds = timeout;
        }

        // Serilog forwards to this provider; do not add an OTLP Serilog sink as well.
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(resource);
            logging.IncludeScopes = false;
            logging.IncludeFormattedMessage = false;
            logging.ParseStateValues = true;
            logging.AddProcessor(new SafeLogProcessor());
            logging.AddOtlpExporter(ConfigureExporter);
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resources => resources.Clear().AddService(
                serviceName,
                serviceNamespace: "DevFlow",
                serviceVersion: options.ServiceVersion,
                autoGenerateServiceInstanceId: false)
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(
                    double.IsFinite(options.TraceSamplingRatio) ? Math.Clamp(options.TraceSamplingRatio, 0, 1) : 1)))
                .AddSource("DevFlow.Cqrs")
                .AddSource("Npgsql")
                .AddAspNetCoreInstrumentation(instrumentation =>
                {
                    instrumentation.RecordException = false;
                    instrumentation.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health")
                        && !context.Request.Path.StartsWithSegments("/alive");
                })
                .AddHttpClientInstrumentation(instrumentation => instrumentation.RecordException = false)
                .AddSqlClientInstrumentation(instrumentation => instrumentation.RecordException = false)
                .AddProcessor(new SafeTraceProcessor())
                .AddOtlpExporter(ConfigureExporter))
            .WithMetrics(metrics => metrics
                .SetExemplarFilter(ExemplarFilterType.TraceBased)
                .AddMeter("DevFlow.Cqrs")
                .AddMeter("DevFlow.Messaging")
                .AddMeter("DevFlow.Notification")
                .AddMeter("DevFlow.Gateway")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()

                // Restrict dimensions rather than exporting hosts, paths, IDs, or exception messages.
                .AddView(instrument =>
                {
                    string[] tags = ["http.request.method", "http.response.status_code", "http.route", "error.type",
                        "cqrs.request_type", "cqrs.outcome", "gc.heap.generation", "gc.collection.generation"];
                    return instrument.Name == "devflow.gateway.duration"
                        ? new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 15, 30],
                            TagKeys = tags,
                        }
                        : new MetricStreamConfiguration { TagKeys = tags };
                })
                .AddOtlpExporter((exporter, reader) =>
                {
                    ConfigureExporter(exporter);
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                        Math.Clamp(options.MetricExportIntervalMilliseconds, 1000, 60000);
                    reader.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds = timeout;
                }));

        return builder;
    }
}
