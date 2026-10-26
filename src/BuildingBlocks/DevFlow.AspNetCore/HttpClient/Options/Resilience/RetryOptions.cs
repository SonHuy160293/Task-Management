namespace DevFlow.AspNetCore.HttpClient.Options.Resilience;

public sealed class RetryOptions
{
    public int MaxRetryAttempts { get; init; } = 2;

    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(250);

    public string BackoffType { get; init; } = "Exponential";

    public bool UseJitter { get; init; } = true;

    public bool ShouldRetryAfterHeader { get; init; } = true;
}
