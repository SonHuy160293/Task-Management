using DevFlow.Identity.Application.Features.Messaging;
using DevFlow.SharedKernel.Abstractions.Message;
using DevFlow.SharedKernel.Abstractions.Shared;
using DevFlow.SharedKernel.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.Identity.Application.DependencyInjection;

public static class EmailReceiptApplicationExtensions
{
    public static IServiceCollection AddEmailReceiptApplication(this IServiceCollection services)
    {
        // A receipt worker must not require HTTP registration/password/Redis services.
        services.AddSelfMadeCqrs(_ => { }).AddApplicationPipelineBehaviors();
        services.AddScoped<IRequestHandler<RecordEmailDeliveryReceiptCommand, Result<bool>>, RecordEmailDeliveryReceiptHandler>();
        return services;
    }
}
