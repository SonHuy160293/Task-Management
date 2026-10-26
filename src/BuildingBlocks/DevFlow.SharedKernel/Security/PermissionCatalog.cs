namespace DevFlow.SharedKernel.Security;

public static class PermissionCatalog
{
    public static readonly IReadOnlyCollection<PermissionCatalogItem> DashboardPermissions =
    [
        Create(Functions.Dashboard, ActionType.View, Functions.DashboardViewDescription, RbacScopeType.Branch)
    ];

    public static readonly IReadOnlyCollection<PermissionCatalogItem> TenantPermissions =
    [
        Create(Functions.Tenants, ActionType.View, Functions.TenantsViewDescription, RbacScopeType.Platform),
        Create(Functions.Tenants, ActionType.Create, Functions.TenantsCreateDescription, RbacScopeType.Platform),
        Create(Functions.Tenants, ActionType.Update, Functions.TenantsUpdateDescription, RbacScopeType.Platform),
        Create(Functions.Tenants, ActionType.Delete, Functions.TenantsDeleteDescription, RbacScopeType.Platform)
    ];

    public static readonly IReadOnlyCollection<PermissionCatalogItem> UserPermissions =
    [
        Create(Functions.Users, ActionType.View, Functions.UsersViewDescription, RbacScopeType.Branch),
        Create(Functions.Users, ActionType.Create, Functions.UsersCreateDescription, RbacScopeType.Branch),
        Create(Functions.Users, ActionType.Update, Functions.UsersUpdateDescription, RbacScopeType.Branch),
        Create(Functions.Users, ActionType.Delete, Functions.UsersDeleteDescription, RbacScopeType.Branch)
    ];

    public static readonly IReadOnlyCollection<PermissionCatalogItem> RolePermissions =
    [
        Create(Functions.Roles, ActionType.View, Functions.RolesViewDescription, RbacScopeType.Branch),
        Create(Functions.Roles, ActionType.Create, Functions.RolesCreateDescription, RbacScopeType.Branch),
        Create(Functions.Roles, ActionType.Update, Functions.RolesUpdateDescription, RbacScopeType.Branch),
        Create(Functions.Roles, ActionType.Delete, Functions.RolesDeleteDescription, RbacScopeType.Branch),
        Create(Functions.Roles, ActionType.Assign, Functions.RolesAssignDescription, RbacScopeType.Branch)
    ];

    public static readonly IReadOnlyCollection<PermissionCatalogItem> MenuPermissions =
    [
        Create(Functions.Menus, ActionType.View, Functions.MenusViewDescription),
        Create(Functions.Menus, ActionType.Create, Functions.MenusCreateDescription),
        Create(Functions.Menus, ActionType.Update, Functions.MenusUpdateDescription),
        Create(Functions.Menus, ActionType.Delete, Functions.MenusDeleteDescription)
    ];

    public static readonly IReadOnlyCollection<PermissionCatalogItem> CinemaPermissions =
    [
        Create(Functions.Cinemas, ActionType.View, Functions.CinemasViewDescription, RbacScopeType.Branch),
        Create(Functions.Cinemas, ActionType.Create, Functions.CinemasCreateDescription, RbacScopeType.Branch),
        Create(Functions.Cinemas, ActionType.Update, Functions.CinemasUpdateDescription, RbacScopeType.Branch)
    ];

    public static readonly IReadOnlyCollection<PermissionCatalogItem> FilmPermissions =
    [
        Create(Functions.Films, ActionType.View, Functions.FilmsViewDescription, RbacScopeType.Platform),
        Create(Functions.Films, ActionType.Create, Functions.FilmsCreateDescription, RbacScopeType.Platform),
        Create(Functions.Films, ActionType.Update, Functions.FilmsUpdateDescription, RbacScopeType.Platform)
    ];

    public static readonly IReadOnlyCollection<PermissionCatalogGroup> Groups =
    [
        new(Functions.Dashboard, Functions.DashboardGroupName, DashboardPermissions),
        new(Functions.Users, Functions.UsersGroupName, UserPermissions),
        new(Functions.Roles, Functions.RolesGroupName, RolePermissions),
        new(Functions.Tenants, Functions.TenantsGroupName, TenantPermissions),
        new(Functions.Menus, Functions.MenusGroupName, MenuPermissions),
        new(Functions.Cinemas, Functions.CinemasGroupName, CinemaPermissions),
        new(Functions.Films, Functions.FilmsGroupName, FilmPermissions)
    ];

    public static string[] GetAdministrativePermissionValues()
        => Groups
            .SelectMany(group => group.Permissions)
            .Select(permission => permission.Value)
            .ToArray();

    public static string ToValue(string function, ActionType action)
        => $"{function}.{action}";

    private static PermissionCatalogItem Create(string function, ActionType action, string description, RbacScopeType? scopeType = null)
        => new(action, $"{action} {function}", ToValue(function, action), description, scopeType);
}

public sealed record PermissionCatalogGroup(
    string Function,
    string GroupName,
    IReadOnlyCollection<PermissionCatalogItem> Permissions);

public sealed record PermissionCatalogItem(
    ActionType Action,
    string Name,
    string Value,
    string? Description,
    RbacScopeType? ScopeType);
