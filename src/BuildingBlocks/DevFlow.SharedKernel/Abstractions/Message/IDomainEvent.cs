namespace DevFlow.SharedKernel.Abstractions.Message;

public interface IEvent : INotification { }

public interface IDomainEvent : IEvent
{
    long Version { get; }
}

public interface IIntegrationEvent : IEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredOnUtc { get; }

    string EventType { get; }
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    protected IntegrationEvent()
    {
        EventId = Guid.NewGuid();
        OccurredOnUtc = DateTimeOffset.UtcNow;
        EventType = GetType().Name;
    }

    public Guid EventId { get; init; }

    public DateTimeOffset OccurredOnUtc { get; init; }

    public string EventType { get; init; }
}
