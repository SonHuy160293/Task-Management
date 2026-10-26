using DevFlow.Notification.Contracts;

namespace DevFlow.Notification.Application.Abstractions;
public interface IEmailDeliveryRepository
{
    Task AcceptAsync(EmailDeliveryRequestedV1 message, CancellationToken cancellationToken);
    Task RecordWebhookAsync(ProviderEmailEvent value, CancellationToken cancellationToken);
}

public sealed record ProviderEmailEvent(string EventId, string PayloadHash, string ProviderId, string Type, string? RecipientHash)
{
    public override string ToString() => "ProviderEmailEvent [REDACTED]";
}
