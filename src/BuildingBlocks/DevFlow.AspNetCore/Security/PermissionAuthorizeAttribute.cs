using DevFlow.SharedKernel.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.AspNetCore.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class PermissionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public PermissionAuthorizeAttribute(
        string function,
        ActionType action,
        bool userIdCheck = false,
        bool tenantIdCheck = false,
        bool branchIdCheck = false)
    {
        Function = function;
        Action = action;
        UserIdCheck = userIdCheck;
        TenantIdCheck = tenantIdCheck;
        BranchIdCheck = branchIdCheck;
    }

    public string Function { get; }
    public ActionType Action { get; }
    public bool UserIdCheck { get; }
    public bool TenantIdCheck { get; }
    public bool BranchIdCheck { get; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (HasPermission(context) && HasRequestedIds(context))
        {
            return;
        }

        context.Result = context.HttpContext.User.Identity?.IsAuthenticated == true
            ? new ForbidResult()
            : new ChallengeResult();
    }

    private bool HasPermission(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        return user.Identity?.IsAuthenticated == true
            && (string.IsNullOrWhiteSpace(Function)
                || user.HasClaim(CustomClaims.Permission, PermissionCatalog.ToValue(Function, Action)));
    }

    private bool HasRequestedIds(AuthorizationFilterContext context)
    {
        if (!UserIdCheck && !TenantIdCheck && !BranchIdCheck)
        {
            return true;
        }

        var currentUser = context.HttpContext.RequestServices
            .GetRequiredService<ICurrentUserProvider>().GetCurrentUser();
        if (currentUser is null)
        {
            return false;
        }

        // Explicit ID restrictions only. Handlers must verify actual resource ownership
        // and the scope of the assignment granting the requested permission.
        return (!UserIdCheck || MatchesId(context, "userId", [currentUser.Id]))
            && (!TenantIdCheck || MatchesId(context, "tenantId", currentUser.TenantIds))
            && (!BranchIdCheck || MatchesId(context, "branchId", currentUser.BranchIds));
    }

    private static bool MatchesId(AuthorizationFilterContext context, string parameter, IEnumerable<string?>? allowedIds)
        => TryGetRequestedId(context, parameter, out var requestedId)
            && allowedIds is not null
            && allowedIds.Any(value => Guid.TryParse(value, out var allowedId) && allowedId == requestedId);

    private static bool TryGetRequestedId(AuthorizationFilterContext context, string parameter, out Guid id)
    {
        id = Guid.Empty;
        var request = context.HttpContext.Request;
        bool hasRoute = request.RouteValues.TryGetValue(parameter, out var routeValue);
        bool hasQuery = request.Query.TryGetValue(parameter, out var queryValues);

        if (!hasRoute && !hasQuery)
        {
            return false;
        }

        if (hasRoute && (!Guid.TryParse(routeValue?.ToString(), out id) || id == Guid.Empty))
        {
            return false;
        }

        if (hasQuery)
        {
            if (queryValues.Count != 1 || !Guid.TryParse(queryValues[0], out var queryId) || queryId == Guid.Empty)
            {
                return false;
            }

            if (hasRoute && id != queryId)
            {
                return false;
            }

            id = queryId;
        }

        return true;
    }
}
