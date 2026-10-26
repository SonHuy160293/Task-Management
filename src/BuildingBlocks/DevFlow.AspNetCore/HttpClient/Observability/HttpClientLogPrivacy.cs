using DevFlow.AspNetCore.HttpClient.Handlers;
using Serilog.Core;
using Serilog.Events;

namespace DevFlow.AspNetCore.HttpClient.Observability;

internal sealed class HttpClientLogPrivacy : ILogEventEnricher, ILogEventFilter
{
    private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
    {
        "SourceContext", "DependencyName", "RequestMethod", "StatusCode", "ElapsedMilliseconds",
        "HttpFailureKind", "CorrelationId"
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!IsClientLog(logEvent))
        {
            return;
        }

        // Remove ambient HTTP/Serilog scope properties before local sinks or OTLP see them.
        foreach (string name in logEvent.Properties.Keys.ToArray())
        {
            if (!AllowedProperties.Contains(name))
            {
                logEvent.RemovePropertyIfPresent(name);
            }
        }
    }

    public bool IsEnabled(LogEvent logEvent)
    {
        if (!IsClientLog(logEvent))
        {
            return true;
        }

        if (logEvent.Exception is not null || logEvent.MessageTemplate.Text != HttpClientTelemetry.CompletionTemplate)
        {
            return false;
        }

        foreach (var (name, value) in logEvent.Properties)
        {
            if (!AllowedProperties.Contains(name) || value is not ScalarValue scalar)
            {
                return false;
            }

            bool safe = name switch
            {
                "SourceContext" => scalar.Value is string source && source == typeof(IntegrationTelemetryHandler).FullName,
                "StatusCode" => scalar.Value is int status && (status == 0 || status is >= 100 and <= 599),
                "ElapsedMilliseconds" => scalar.Value is double elapsed && double.IsFinite(elapsed) && elapsed >= 0,
                "CorrelationId" => scalar.Value is string id && (id == "None" || Guid.TryParse(id, out _)),
                "RequestMethod" => scalar.Value is string method
                    && method is "GET" or "HEAD" or "POST" or "PUT" or "PATCH" or "DELETE" or "OPTIONS" or "OTHER",
                "HttpFailureKind" => scalar.Value is string kind && kind is "None" or "Cancelled" or "InvalidRequest"
                    or "HttpFailure" or "NetworkFailure" or "Timeout" or "CircuitOpen" or "InvalidResponse" or "AuthenticationFailure",
                "DependencyName" => scalar.Value is string token && token.Length is > 0 and <= 100
                    && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'),
                _ => false
            };
            if (!safe)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsClientLog(LogEvent logEvent)
        => logEvent.Properties.TryGetValue("SourceContext", out var source)
            && source is ScalarValue { Value: string category } && category == typeof(IntegrationTelemetryHandler).FullName;
}
