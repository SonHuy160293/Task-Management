using Microsoft.AspNetCore.Builder;

namespace DevFlow.Identity.API.Security;

public static class RegistrationProtectionExtensions
{
    public static IApplicationBuilder UseIdentityRegistrationProtection(this IApplicationBuilder app)
        => app.UseMiddleware<RegistrationProtectionMiddleware>();
}
