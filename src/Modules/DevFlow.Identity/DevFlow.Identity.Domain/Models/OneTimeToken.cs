using DevFlow.SharedKernel.Abstractions.Domain;

namespace DevFlow.Identity.Domain.Models;

public sealed class OneTimeToken : IEntity<Guid>
{
    private OneTimeToken() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }
    public string Purpose { get; private set; } = null!;
    public byte[] TokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public User User { get; private set; } = null!;

    public static OneTimeToken Create(
        Guid userId,
        string purpose,
        byte[] tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt = null)
        => new OneTimeToken
        {
            UserId = userId,
            Purpose = purpose,
            TokenHash = tokenHash.ToArray(),
            CreatedAt = createdAt.ToUniversalTime(),
            ExpiresAt = expiresAt.ToUniversalTime(),
            ConsumedAt = consumedAt?.ToUniversalTime(),
        };
}
