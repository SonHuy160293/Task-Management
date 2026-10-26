using Serilog.Core;
using Serilog.Events;

namespace DevFlow.Gateway;

public sealed class GatewayLogPrivacy : ILogEventFilter
{
    public bool IsEnabled(LogEvent logEvent)
    {
        if (logEvent.Exception is not null)
        {
            return false;
        }

        bool completion = logEvent.MessageTemplate.Text == GatewayRequestMiddleware.CompletionTemplate;
        if (!completion && logEvent.MessageTemplate.Text is not("Gateway configuration accepted" or "Gateway configuration rejected"))
        {
            return false;
        }

        foreach (string name in logEvent.Properties.Keys.ToArray())
        {
            if (name is not("RequestMethod" or "RouteId" or "ClusterId" or "DestinationId" or "StatusCode" or "ElapsedMilliseconds"
                or "GatewayErrorCode" or "ForwarderErrorCode" or "CorrelationId" or "TraceId" or "SpanId"))
            {
                logEvent.RemovePropertyIfPresent(name);
            }
        }

        return logEvent.Properties.All(property => property.Value is ScalarValue scalar && property.Key switch
        {
            "StatusCode" => scalar.Value is int status && status is >= 100 and <= 599,
            "ElapsedMilliseconds" => scalar.Value is double duration && double.IsFinite(duration) && duration >= 0,
            "CorrelationId" => scalar.Value is string id && Guid.TryParse(id, out _),
            "TraceId" or "SpanId" => scalar.Value is string trace && (trace == "None" || (trace.Length == (property.Key == "TraceId" ? 32 : 16) && trace.All(char.IsAsciiHexDigit))),
            _ => scalar.Value is string token && GatewayConfigProvider.IsSafeToken(token),
        });
    }
}
