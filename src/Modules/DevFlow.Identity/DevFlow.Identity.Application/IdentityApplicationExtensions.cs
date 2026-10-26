using DevFlow.Identity.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.Identity.Application;

public static class IdentityApplicationExtensions
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
        => services.AddApplication();
}
