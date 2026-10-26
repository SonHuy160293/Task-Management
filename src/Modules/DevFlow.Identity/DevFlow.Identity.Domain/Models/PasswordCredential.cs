using DevFlow.SharedKernel.Abstractions.Domain;

namespace DevFlow.Identity.Domain.Models;

public sealed class PasswordCredential : Entity<Guid>
{
    private PasswordCredential() : base(Guid.CreateVersion7())
    {
        UpdatedAt = CreatedAt;
    }

    public Guid UserId { get; private set; }
    public string PasswordHash { get; private set; } = null!;
    public int FailedAttempts { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset PasswordChangedAt { get; private set; } = DateTimeOffset.UtcNow;

    public User User { get; private set; } = null!;

    public static PasswordCredential Create(
        Guid userId,
        string passwordHash,
        int failedAttempts = 0,
        DateTimeOffset? lockedUntil = null)
        => new PasswordCredential
        {
            UserId = userId,
            PasswordHash = passwordHash,
            FailedAttempts = failedAttempts,
            LockedUntil = lockedUntil?.ToUniversalTime(),
        };
}
