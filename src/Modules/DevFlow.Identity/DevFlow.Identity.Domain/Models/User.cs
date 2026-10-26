using DevFlow.SharedKernel.Abstractions.Domain;

namespace DevFlow.Identity.Domain.Models;

public sealed class User : Entity<Guid>
{
    private User() : base(Guid.CreateVersion7())
    {
        UpdatedAt = CreatedAt;
    }

    public string PrimaryEmail { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string? AvatarUrl { get; private set; }
    public string Locale { get; private set; } = "en";
    public string TimeZone { get; private set; } = "UTC";
    public string Status { get; private set; } = "pending";
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    public static User Create(
        string primaryEmail,
        string displayName,
        string? avatarUrl = null,
        string locale = "en",
        string timeZone = "UTC",
        string status = "pending",
        DateTimeOffset? emailVerifiedAt = null,
        DateTimeOffset? lastLoginAt = null)
        => new User
        {
            PrimaryEmail = primaryEmail,
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
            Locale = locale,
            TimeZone = timeZone,
            Status = status,
            EmailVerifiedAt = emailVerifiedAt?.ToUniversalTime(),
            LastLoginAt = lastLoginAt?.ToUniversalTime(),
        };
}
