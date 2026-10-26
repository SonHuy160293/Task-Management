using DevFlow.Notification.Contracts;

namespace DevFlow.Identity.Application.Abstractions.Messaging;
public interface IVerificationEmailOutbox
{
    Task StageAsync(EmailDeliveryRequestedV1 request, CancellationToken cancellationToken);
}

public interface IEmailDeliveryReceiptStore
{
    Task RecordAsync(EmailDeliveryRecordedV1 receipt, CancellationToken cancellationToken);
}
