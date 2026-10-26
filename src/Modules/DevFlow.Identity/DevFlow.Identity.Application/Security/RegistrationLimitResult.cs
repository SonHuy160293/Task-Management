namespace DevFlow.Identity.Application.Security;
public enum RegistrationLimitStatus
{
    Allowed,
    Limited,
    Unavailable,
}
public sealed record RegistrationLimitResult(RegistrationLimitStatus Status, TimeSpan RetryAfter);
