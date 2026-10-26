using DevFlow.EntityFrameworkCore.Reliability;

namespace DevFlow.Messaging.Outbox;
public interface IOutboxDispatchStore
{
    Task<IReadOnlyList<OutboxMessage>> ClaimAsync(CancellationToken cancellationToken);
    Task PublishedAsync(Guid id, Guid lease, CancellationToken cancellationToken);
    Task FailedAsync(Guid id, Guid lease, string code, bool permanent, CancellationToken cancellationToken);
}

public interface IIntegrationEventPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}

public sealed class InvalidIntegrationEventException() : Exception("Invalid integration event.");
public sealed record IntegrationEventRegistration(string EventType, Type ContractType, Action<object>? Validate = null);
public static class RetrySchedule
{
    public static TimeSpan Delay(int attempt, int maximumSeconds = 300) => TimeSpan.FromSeconds(Math.Max(1, Random.Shared.NextDouble() * Math.Min(maximumSeconds, 15 * Math.Pow(2, Math.Min(attempt, 16)))));
}
