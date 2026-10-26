using DevFlow.SharedKernel.Abstractions.Message;

namespace DevFlow.SharedKernel.Cqrs;

public interface IPublisher
{
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
