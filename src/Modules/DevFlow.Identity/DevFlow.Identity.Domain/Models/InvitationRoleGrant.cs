namespace DevFlow.Identity.Domain.Models;

public sealed class InvitationRoleGrant
{
    private InvitationRoleGrant() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid TenantId { get; private set; }
    public Guid InvitationId { get; private set; }
    public Guid ScopeId { get; private set; }
    public string ScopeKind { get; private set; } = null!;
    public Guid RoleId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public Organization Tenant { get; private set; } = null!;
    public Invitation Invitation { get; private set; } = null!;
    public AccessScope Scope { get; private set; } = null!;
    public Role Role { get; private set; } = null!;

    public static InvitationRoleGrant Create(
        Guid tenantId,
        Guid invitationId,
        Guid scopeId,
        string scopeKind,
        Guid roleId)
        => new InvitationRoleGrant
        {
            TenantId = tenantId,
            InvitationId = invitationId,
            ScopeId = scopeId,
            ScopeKind = scopeKind,
            RoleId = roleId,
        };
}
