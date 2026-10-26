using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using DevFlow.SharedKernel.Security;
using Microsoft.AspNetCore.Http;

namespace DevFlow.AspNetCore.Security;

public sealed class HttpContextCurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public CurrentUser? GetCurrentUser()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var userName = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
            ?? user.FindFirstValue(ClaimTypes.Name);
        var email = user.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? user.FindFirstValue(ClaimTypes.Email);
        var permissions = user.FindAll(CustomClaims.Permission)
            .Select(claim => claim.Value)
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new CurrentUser(
            Id: userId,
            UserName: userName,
            Email: email,
            TenantIds: ReadIds(user, CustomClaims.TenantIds),
            BranchIds: ReadIds(user, CustomClaims.BranchIds),
            Permissions: permissions,
            SessionId: user.FindFirstValue(CustomClaims.SessionId));
    }

    private static string[] ReadIds(ClaimsPrincipal user, string claimType)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in user.FindAll(claimType))
        {
            var value = claim.Value.Trim();
            if (value.StartsWith("[", StringComparison.Ordinal))
            {
                try
                {
                    var values = JsonSerializer.Deserialize<string[]>(value);
                    foreach (var id in values ?? [])
                    {
                        AddId(id, ids);
                    }
                }
                catch (JsonException)
                {
                    // Malformed claims cannot grant an ID membership.
                }
            }
            else
            {
                AddId(value, ids);
            }
        }

        return ids.Order(StringComparer.Ordinal).ToArray();
    }

    private static void AddId(string? value, ISet<string> ids)
    {
        if (Guid.TryParse(value, out var id) && id != Guid.Empty)
        {
            ids.Add(id.ToString());
        }
    }
}
