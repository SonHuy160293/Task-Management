using DevFlow.SharedKernel.Abstractions.Domain;

namespace DevFlow.Identity.Domain.Models;

public sealed class Organization : Entity<Guid>
{
    private Organization() : base(Guid.CreateVersion7())
    {
        UpdatedAt = CreatedAt;
    }

    public string Slug { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string Status { get; private set; } = "active";
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset? SuspendedAt { get; private set; }

    public User CreatedByUser { get; private set; } = null!;

    public static Organization Create(
        string slug,
        string displayName,
        Guid createdByUserId,
        string status = "active",
        DateTimeOffset? suspendedAt = null)
        => new Organization
        {
            Slug = slug,
            DisplayName = displayName,
            Status = status,
            CreatedByUserId = createdByUserId,
            SuspendedAt = suspendedAt?.ToUniversalTime(),
        };
}
