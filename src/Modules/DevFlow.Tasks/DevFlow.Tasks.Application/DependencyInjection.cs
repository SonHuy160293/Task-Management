using DevFlow.SharedKernel.Behaviors;
using DevFlow.SharedKernel.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.Tasks.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTasksApplication(this IServiceCollection services)
        => services.AddSelfMadeCqrs(options => options
            .RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly)
            .AddPipelineBehavior(typeof(ValidationPipelineBehavior<,>))
            .AddPipelineBehavior(typeof(LoggingBehavior<,>))
            .AddPipelineBehavior(typeof(PerformancePipelineBehavior<,>))
            .AddPipelineBehavior(typeof(TracingPipelineBehavior<,>))
            .AddPipelineBehavior(typeof(TransactionPipelineBehavior<,>)));
}