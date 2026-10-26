namespace DevFlow.Identity.Domain.Models;

public sealed class PlatformRoleAssignment
{
    private PlatformRoleAssignment() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public string ScopeKind { get; private set; } = "platform";
    public Guid? AssignedByUserId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; private set; }

    public User User { get; private set; } = null!;
    public User? AssignedByUser { get; private set; }
    public Role Role { get; private set; } = null!;

    public static PlatformRoleAssignment Create(
        Guid userId,
        Guid roleId,
        string scopeKind = "platform",
        Guid? assignedByUserId = null,
        DateTimeOffset? revokedAt = null)
        => new PlatformRoleAssignment
        {
            UserId = userId,
            RoleId = roleId,
            ScopeKind = scopeKind,
            AssignedByUserId = assignedByUserId,
            RevokedAt = revokedAt?.ToUniversalTime(),
        };
}
