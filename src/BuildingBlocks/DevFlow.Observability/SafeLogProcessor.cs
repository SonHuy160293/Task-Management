using OpenTelemetry;
using OpenTelemetry.Logs;

namespace DevFlow.Observability;

public sealed class SafeLogProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data)
    {
        var attributes = new List<KeyValuePair<string, object?>>();
        string? template = null;
        foreach (var attribute in data.Attributes ?? [])
        {
            if (attribute.Key == "{OriginalFormat}" && attribute.Value is string value)
            {
                template = value;
            }
            else if (attribute.Key is "ElapsedMilliseconds" or "Elapsed" or "StatusCode"
                && attribute.Value is int or long or double)
            {
                attributes.Add(attribute);
            }
            else if (attribute.Key is "RequestName" or "RequestType" && attribute.Value is string name
                && name.Length <= 200 && name.All(character => char.IsLetterOrDigit(character) || character == '_'))
            {
                attributes.Add(attribute);
            }
            else if (attribute.Key == "CorrelationId" && Guid.TryParse(attribute.Value?.ToString(), out var id))
            {
                attributes.Add(new("CorrelationId", id.ToString("D")));
            }
            else if (attribute.Key is "RouteId" or "ClusterId" or "DestinationId" or "GatewayErrorCode" or "ForwarderErrorCode"
                or "DependencyName" or "HttpFailureKind"
                && attribute.Value is string token && token.Length is > 0 and <= 100
                && token.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'))
            {
                attributes.Add(attribute);
            }
            else if (attribute.Key == "RequestMethod" && attribute.Value is string method
                && method is "GET" or "HEAD" or "POST" or "PUT" or "PATCH" or "DELETE" or "OPTIONS" or "OTHER")
            {
                attributes.Add(attribute);
            }
            else if (attribute.Key is "TraceId" or "SpanId" && attribute.Value is string trace
                && (trace == "None" || trace.Length == (attribute.Key == "TraceId" ? 32 : 16) && trace.All(char.IsAsciiHexDigit)))
            {
                attributes.Add(attribute);
            }
        }

        // Export reviewed static templates only. Arbitrary formatted messages and exceptions
        // can contain SQL, passwords, reset links, or personal data. Gateway local sinks
        // apply their own allowlist before forwarding to this provider.
        data.Body = template switch
        {
            "Handling {RequestType}" or "Handled {RequestType}"
                or "Request {RequestName} completed in {ElapsedMilliseconds} ms"
                or "Long running request: {RequestName} took {ElapsedMilliseconds} ms" => template,
            "Gateway {RequestMethod} {RouteId} via {ClusterId} responded {StatusCode} in {ElapsedMilliseconds} ms; {GatewayErrorCode} {ForwarderErrorCode} {TraceId} {SpanId}" => template,
            "Gateway configuration accepted" or "Gateway configuration rejected" => template,
            "Dependency {DependencyName} {RequestMethod} responded {StatusCode} in {ElapsedMilliseconds} ms; {HttpFailureKind} {CorrelationId}" => template,
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms" => "HTTP request completed",
            _ => "Application log"
        };
        data.FormattedMessage = null;
        data.Exception = null;
        data.TraceState = null;
        data.Attributes = attributes;
    }
}
