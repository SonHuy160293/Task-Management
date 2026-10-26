using DevFlow.SharedKernel.Abstractions.Domain;

namespace DevFlow.Identity.Domain.Models;

public sealed class OrganizationMember : Entity<Guid>
{
    private OrganizationMember() : base(Guid.CreateVersion7())
    {
        UpdatedAt = CreatedAt;
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string MembershipType { get; private set; } = "employee";
    public string Status { get; private set; } = "active";
    public DateTimeOffset JoinedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RemovedAt { get; private set; }

    public Organization Tenant { get; private set; } = null!;
    public User User { get; private set; } = null!;

    public static OrganizationMember Create(
        Guid tenantId,
        Guid userId,
        string membershipType = "employee",
        string status = "active",
        DateTimeOffset? removedAt = null)
        => new OrganizationMember
        {
            TenantId = tenantId,
            UserId = userId,
            MembershipType = membershipType,
            Status = status,
            RemovedAt = removedAt?.ToUniversalTime(),
        };
}
