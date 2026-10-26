namespace DevFlow.Identity.Domain.Models;

public sealed class MemberRoleAssignment
{
    private MemberRoleAssignment() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid TenantId { get; private set; }
    public Guid MemberId { get; private set; }
    public Guid ScopeId { get; private set; }
    public string ScopeKind { get; private set; } = null!;
    public Guid RoleId { get; private set; }
    public Guid? AssignedByMemberId { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public Organization Tenant { get; private set; } = null!;
    public OrganizationMember Member { get; private set; } = null!;
    public OrganizationMember? AssignedByMember { get; private set; }
    public AccessScope Scope { get; private set; } = null!;
    public Role Role { get; private set; } = null!;

    public static MemberRoleAssignment Create(
        Guid tenantId,
        Guid memberId,
        Guid scopeId,
        string scopeKind,
        Guid roleId,
        Guid? assignedByMemberId = null,
        string? reason = null,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? revokedAt = null)
        => new MemberRoleAssignment
        {
            TenantId = tenantId,
            MemberId = memberId,
            ScopeId = scopeId,
            ScopeKind = scopeKind,
            RoleId = roleId,
            AssignedByMemberId = assignedByMemberId,
            Reason = reason,
            ExpiresAt = expiresAt?.ToUniversalTime(),
            RevokedAt = revokedAt?.ToUniversalTime(),
        };
}
