namespace BookingSystem.Identity.Infrastructure.DependencyInjection.Options;

public sealed class PostgreOptions
{
    public const string SectionName = "Postgre";

    public int MaxRetryCount { get; init; } = 3;
    public int MaxRetryDelaySeconds { get; init; } = 10;
}
