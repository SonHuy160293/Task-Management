using System.Net;

namespace DevFlow.Identity.Domain.Models;

public sealed class AuthSession
{
    private AuthSession() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }
    public IPAddress? ClientIp { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSeenAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public User User { get; private set; } = null!;

    public static AuthSession Create(
        Guid userId,
        DateTimeOffset expiresAt,
        IPAddress? clientIp = null,
        string? userAgent = null,
        DateTimeOffset? lastSeenAt = null,
        DateTimeOffset? revokedAt = null)
        => new AuthSession
        {
            UserId = userId,
            ClientIp = clientIp,
            UserAgent = userAgent,
            LastSeenAt = lastSeenAt?.ToUniversalTime(),
            ExpiresAt = expiresAt.ToUniversalTime(),
            RevokedAt = revokedAt?.ToUniversalTime(),
        };
}
