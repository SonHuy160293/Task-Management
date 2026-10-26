using DevFlow.Notification.Contracts;
using DevFlow.SharedKernel.Abstractions.Message;

namespace DevFlow.Notification.Application.Features.EmailDelivery.Commands.AcceptEmailDelivery;

public sealed record AcceptEmailDeliveryCommand(EmailDeliveryRequestedV1 Event) : ICommand<bool>
{
    public override string ToString() => $"AcceptEmailDeliveryCommand {Event.MessageId} [REDACTED]";
}
