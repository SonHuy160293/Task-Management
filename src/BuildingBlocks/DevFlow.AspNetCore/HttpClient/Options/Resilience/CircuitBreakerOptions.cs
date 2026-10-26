namespace DevFlow.AspNetCore.HttpClient.Options.Resilience;

public sealed class CircuitBreakerOptions
{
    public double FailureRatio { get; init; } = 0.5;

    public int MinimumThroughput { get; init; } = 20;

    public TimeSpan SamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(15);
}
