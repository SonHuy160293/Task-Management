using System.Text.Json;
using DevFlow.EntityFrameworkCore.Reliability;
using DevFlow.Messaging.Outbox;
using MassTransit;

namespace DevFlow.Messaging.MassTransit;
public sealed class MassTransitEventPublisher(IBus bus, IEnumerable<IntegrationEventRegistration> registrations) : IIntegrationEventPublisher
{
    public async Task PublishAsync(OutboxMessage message, CancellationToken ct)
    {
        var registration = registrations.SingleOrDefault(x => x.EventType == message.MessageType);
        if (registration is null || message.SchemaVersion != 1)
        {
            throw new InvalidIntegrationEventException();
        }

        object value;
        try
        {
            value = JsonSerializer.Deserialize(message.PayloadJson, registration.ContractType) ?? throw new InvalidIntegrationEventException();
            registration.Validate?.Invoke(value);
        }
        catch (Exception ex)when (ex is JsonException or ArgumentException)
        {
            throw new InvalidIntegrationEventException();
        }

        await bus.Publish(value, registration.ContractType, context =>
        {
            context.MessageId = message.MessageId;
            context.CorrelationId = message.CorrelationId;
            context.SetRoutingKey("");
            if (context.TryGetPayload<RabbitMqSendContext>(out var rabbit))
            {
                rabbit.Mandatory = true;
            }
        }, ct);
    }
}
