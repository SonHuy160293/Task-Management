namespace DevFlow.Identity.Infrastructure.Security.Options;

public sealed class VerificationTokenOptions
{
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(24);
    public bool IsValid() => Lifetime >= TimeSpan.FromMinutes(5) && Lifetime <= TimeSpan.FromHours(24);
}
