namespace DevFlow.AspNetCore.HttpClient.Observability;

public static class HttpClientTelemetry
{
    public const string CompletionTemplate = "Dependency {DependencyName} {RequestMethod} responded {StatusCode} in {ElapsedMilliseconds} ms; {HttpFailureKind} {CorrelationId}";
}
