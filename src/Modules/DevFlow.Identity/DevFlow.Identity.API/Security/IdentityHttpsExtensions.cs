using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DevFlow.Identity.API.Security;

public static class IdentityHttpsExtensions
{
    public static IServiceCollection AddIdentityHttpsRedirection(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddHttpsRedirection(options =>
            options.HttpsPort = configuration.GetValue<int?>("HttpsRedirection:HttpsPort") ?? 443);
    }

    public static WebApplication UseIdentityHttpsRedirection(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseWhen(
                context => context.Request.Path != "/health/live" && context.Request.Path != "/health/ready",
                branch => branch.UseHttpsRedirection());
        }

        return app;
    }
}
