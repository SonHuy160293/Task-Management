namespace DevFlow.Identity.Domain.Models;

public sealed class Role
{
    private Role() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string Code { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string ScopeKind { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsAssignable { get; private set; } = true;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static Role Create(
        string code,
        string displayName,
        string scopeKind,
        string? description = null,
        bool isAssignable = true,
        bool isActive = true)
        => new Role
        {
            Code = code,
            DisplayName = displayName,
            ScopeKind = scopeKind,
            Description = description,
            IsAssignable = isAssignable,
            IsActive = isActive,
        };
}
