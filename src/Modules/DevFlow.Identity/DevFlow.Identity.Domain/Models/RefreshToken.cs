namespace DevFlow.Identity.Domain.Models;

public sealed class RefreshToken
{
    private RefreshToken() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid SessionId { get; private set; }
    public byte[] TokenHash { get; private set; } = null!;
    public Guid? ReplacedByTokenId { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public AuthSession Session { get; private set; } = null!;
    public RefreshToken? ReplacedByToken { get; private set; }

    public static RefreshToken Create(
        Guid sessionId,
        byte[] tokenHash,
        DateTimeOffset expiresAt,
        Guid? replacedByTokenId = null,
        DateTimeOffset? consumedAt = null,
        DateTimeOffset? revokedAt = null)
        => new RefreshToken
        {
            SessionId = sessionId,
            TokenHash = tokenHash.ToArray(),
            ReplacedByTokenId = replacedByTokenId,
            ExpiresAt = expiresAt.ToUniversalTime(),
            ConsumedAt = consumedAt?.ToUniversalTime(),
            RevokedAt = revokedAt?.ToUniversalTime(),
        };
}
