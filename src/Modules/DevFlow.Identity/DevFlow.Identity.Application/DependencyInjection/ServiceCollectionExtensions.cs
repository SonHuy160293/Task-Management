using DevFlow.SharedKernel.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.Identity.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
       => services
           .AddApplicationCqrs()
           .AddApplicationPipelineBehaviors();

    public static IServiceCollection AddApplicationCqrs(this IServiceCollection services)
        => services.AddSelfMadeCqrs(cfg =>
            cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly));
}
