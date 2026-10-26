using DevFlow.Notification.Application.Features.EmailDelivery.Commands.AcceptEmailDelivery;
using DevFlow.Notification.Contracts;
using DevFlow.SharedKernel.Cqrs;
using MassTransit;

namespace DevFlow.Notification.Infrastructure.Messaging.Consumers;
public sealed class EmailDeliveryRequestedConsumer(ISender sender) : IConsumer<EmailDeliveryRequestedV1>
{
    public async Task Consume(ConsumeContext<EmailDeliveryRequestedV1> context)
    {
        if (context.MessageId != context.Message.MessageId)
        {
            throw new InvalidOperationException("Message identity mismatch.");
        }

        var result = await sender.SendAsync(new AcceptEmailDeliveryCommand(context.Message), context.CancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException("Notification intake rejected.");
        }
    }
}
