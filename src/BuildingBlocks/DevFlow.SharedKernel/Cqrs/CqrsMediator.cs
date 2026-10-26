using System.Reflection;
using DevFlow.SharedKernel.Abstractions.Message;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.SharedKernel.Cqrs;

public sealed class CqrsMediator : ISender, IPublisher
{
    private readonly IServiceProvider _serviceProvider;

    public CqrsMediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var method = typeof(CqrsMediator)
            .GetMethod(nameof(SendCoreAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(request.GetType(), typeof(TResponse));

        return (Task<TResponse>)method.Invoke(this, [request, cancellationToken])!;
    }

    public async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.GetType() != typeof(TNotification))
        {
            var method = typeof(CqrsMediator)
                .GetMethod(nameof(PublishCoreAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(notification.GetType());

            await (Task)method.Invoke(this, [notification, cancellationToken])!;
            return;
        }

        await PublishCoreAsync(notification, cancellationToken);
    }

    private async Task PublishCoreAsync<TNotification>(TNotification notification, CancellationToken cancellationToken)
        where TNotification : INotification
    {
        foreach (var handler in _serviceProvider.GetServices<INotificationHandler<TNotification>>())
        {
            await handler.HandleAsync(notification, cancellationToken);
        }
    }

    private async Task<TResponse> SendCoreAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        RequestHandlerDelegate<TResponse> handler = async () =>
        {
            var concreteHandler = _serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            return await concreteHandler.HandleAsync(request, cancellationToken);
        };

        foreach (var behavior in _serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
        {
            var next = handler;
            handler = () => behavior.HandleAsync(request, next, cancellationToken);
        }

        return await handler();
    }
}
