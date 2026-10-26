using DevFlow.AspNetCore.HttpClient.Handlers;
using DevFlow.AspNetCore.HttpClient.Observability;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DevFlow.AspNetCore.HttpClient.Tests.Security;

public sealed class HttpClientLogPrivacyTests
{
    [Fact]
    public void NewTelemetryCategoryRetainsSafeLogsAndRemovesSensitiveProperties()
    {
        var sink = new CaptureSink();
        using Logger logger = new LoggerConfiguration()
            .Enrich.With(new HttpClientLogPrivacy())
            .Filter.With(new HttpClientLogPrivacy())
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.ForContext<IntegrationTelemetryHandler>()
            .ForContext("ApiKey", "must-not-be-logged")
            .Warning(
                HttpClientTelemetry.CompletionTemplate,
                "Identity",
                "GET",
                503,
                12.0,
                "HttpFailure",
                Guid.NewGuid().ToString("D"));
        logger.ForContext<IntegrationTelemetryHandler>()
            .Warning("Unexpected payload {Body}", "sensitive");

        LogEvent logEvent = Assert.Single(sink.Events);
        Assert.Equal(typeof(IntegrationTelemetryHandler).FullName,
            Assert.IsType<ScalarValue>(logEvent.Properties["SourceContext"]).Value);
        Assert.False(logEvent.Properties.ContainsKey("ApiKey"));
        Assert.False(logEvent.Properties.ContainsKey("Body"));
    }

    private sealed class CaptureSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
