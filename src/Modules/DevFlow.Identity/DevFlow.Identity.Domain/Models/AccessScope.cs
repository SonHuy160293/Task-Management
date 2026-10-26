namespace DevFlow.Identity.Domain.Models;

public sealed class AccessScope
{
    private AccessScope() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid TenantId { get; private set; }
    public string ScopeKind { get; private set; } = null!;
    // Logical identifier owned by another module; intentionally has no cross-module navigation or FK.
    public Guid ResourceId { get; private set; }
    public string Status { get; private set; } = "active";
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ArchivedAt { get; private set; }

    public Organization Tenant { get; private set; } = null!;

    public static AccessScope Create(
        Guid tenantId,
        string scopeKind,
        Guid resourceId,
        string status = "active",
        DateTimeOffset? archivedAt = null)
        => new AccessScope
        {
            TenantId = tenantId,
            ScopeKind = scopeKind,
            ResourceId = resourceId,
            Status = status,
            ArchivedAt = archivedAt?.ToUniversalTime(),
        };
}
