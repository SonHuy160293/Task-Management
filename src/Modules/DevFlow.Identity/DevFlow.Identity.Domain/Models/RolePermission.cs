namespace DevFlow.Identity.Domain.Models;

public sealed class RolePermission
{
    private RolePermission() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public string ScopeKind { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public Role Role { get; private set; } = null!;
    public Permission Permission { get; private set; } = null!;

    public static RolePermission Create(
        Guid roleId,
        Guid permissionId,
        string scopeKind)
        => new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
            ScopeKind = scopeKind,
        };
}
