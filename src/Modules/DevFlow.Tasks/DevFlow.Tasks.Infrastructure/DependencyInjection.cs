using DevFlow.AspNetCore.HttpClient.DependencyInjection;
using DevFlow.Tasks.Application.Abstractions.Integrations;
using DevFlow.Tasks.Infrastructure.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.Tasks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTasksInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddIntegrationHttpClient<IIdentityIntegrationService, IdentityIntegrationService>(
            "Identity",
            configuration.GetSection("Integrations:Identity"));

        // Register the module DbContext and IUnitOfWork when persistence is introduced.
        return services;
    }
}
