namespace DevFlow.Identity.API.Contracts.Auth;

public sealed record RegisterRequest(
    string? Email,
    string? Password,
    string? DisplayName,
    string? Locale,
    string? TimeZone)
{
    public override string ToString() => "RegisterRequest [REDACTED]";
}
