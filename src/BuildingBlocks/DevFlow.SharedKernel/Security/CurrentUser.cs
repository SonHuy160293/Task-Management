namespace DevFlow.SharedKernel.Security;

public sealed record CurrentUser(
    string? Id,
    string? UserName = null,
    string? Email = null,
    string[]? TenantIds = null,
    string[]? BranchIds = null,
    IReadOnlyCollection<string>? Permissions = null,
    string? SessionId = null);
