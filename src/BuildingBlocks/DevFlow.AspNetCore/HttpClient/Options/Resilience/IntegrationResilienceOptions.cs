namespace DevFlow.AspNetCore.HttpClient.Options.Resilience;

public sealed class IntegrationResilienceOptions
{
    public TimeoutOptions TotalRequestTimeout { get; init; } = new() { Timeout = TimeSpan.FromSeconds(8) };

    public TimeoutOptions AttemptTimeout { get; init; } = new() { Timeout = TimeSpan.FromSeconds(3) };

    public RetryOptions Retry { get; init; } = new();

    public CircuitBreakerOptions CircuitBreaker { get; init; } = new();
}
