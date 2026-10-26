namespace DevFlow.Identity.Domain.Models;

public sealed class Permission
{
    private Permission() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string Code { get; private set; } = null!;
    public string Module { get; private set; } = null!;
    public string ScopeKind { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public bool IsSensitive { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static Permission Create(
        string code,
        string module,
        string scopeKind,
        string description,
        bool isSensitive = false,
        bool isActive = true)
        => new Permission
        {
            Code = code,
            Module = module,
            ScopeKind = scopeKind,
            Description = description,
            IsSensitive = isSensitive,
            IsActive = isActive,
        };
}
