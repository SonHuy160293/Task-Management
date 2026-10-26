namespace DevFlow.Identity.Domain.Models;

// An additional ALLOW grant at one scope, not a DENY or a role-permission override.
public sealed class MemberPermissionGrant
{
    private MemberPermissionGrant() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid TenantId { get; private set; }
    public Guid MemberId { get; private set; }
    public Guid ScopeId { get; private set; }
    public string ScopeKind { get; private set; } = null!;
    public Guid PermissionId { get; private set; }
    public Guid? GrantedByMemberId { get; private set; }
    public string Justification { get; private set; } = null!;
    public DateTimeOffset GrantedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public Organization Tenant { get; private set; } = null!;
    public OrganizationMember Member { get; private set; } = null!;
    public OrganizationMember? GrantedByMember { get; private set; }
    public AccessScope Scope { get; private set; } = null!;
    public Permission Permission { get; private set; } = null!;

    public static MemberPermissionGrant Create(
        Guid tenantId,
        Guid memberId,
        Guid scopeId,
        string scopeKind,
        Guid permissionId,
        string justification,
        Guid? grantedByMemberId = null,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? revokedAt = null)
        => new MemberPermissionGrant
        {
            TenantId = tenantId,
            MemberId = memberId,
            ScopeId = scopeId,
            ScopeKind = scopeKind,
            PermissionId = permissionId,
            GrantedByMemberId = grantedByMemberId,
            Justification = justification,
            ExpiresAt = expiresAt?.ToUniversalTime(),
            RevokedAt = revokedAt?.ToUniversalTime(),
        };
}
