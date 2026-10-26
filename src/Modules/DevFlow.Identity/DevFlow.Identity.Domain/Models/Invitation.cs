namespace DevFlow.Identity.Domain.Models;

public sealed class Invitation
{
    private Invitation() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid TenantId { get; private set; }
    public string InviteeEmail { get; private set; } = null!;
    public string MembershipType { get; private set; } = "employee";
    public byte[] TokenHash { get; private set; } = null!;
    public string Status { get; private set; } = "pending";
    public Guid InvitedByMemberId { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public Organization Tenant { get; private set; } = null!;
    public User? AcceptedByUser { get; private set; }
    public OrganizationMember InvitedByMember { get; private set; } = null!;

       public static Invitation Create(
        Guid tenantId,
        string inviteeEmail,
        byte[] tokenHash,
        Guid invitedByMemberId,
        DateTimeOffset expiresAt,
        string membershipType = "employee",
        string status = "pending",
        Guid? acceptedByUserId = null,
        DateTimeOffset? acceptedAt = null,
        DateTimeOffset? revokedAt = null)
        => new Invitation
        {
            TenantId = tenantId,
            InviteeEmail = inviteeEmail,
            MembershipType = membershipType,
            TokenHash = tokenHash.ToArray(),
            Status = status,
            InvitedByMemberId = invitedByMemberId,
            AcceptedByUserId = acceptedByUserId,
            ExpiresAt = expiresAt.ToUniversalTime(),
            AcceptedAt = acceptedAt?.ToUniversalTime(),
            RevokedAt = revokedAt?.ToUniversalTime(),
        };
}
