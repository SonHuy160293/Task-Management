namespace DevFlow.Identity.Application.Security;

public sealed class VerificationToken(string rawToken, byte[] tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
{
    public string RawToken { get; } = rawToken;
    public byte[] TokenHash { get; } = tokenHash;
    public DateTimeOffset CreatedAt { get; } = createdAt;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public string Purpose { get; } = "verify_email";
    public override string ToString() => "VerificationToken [REDACTED]";
}
