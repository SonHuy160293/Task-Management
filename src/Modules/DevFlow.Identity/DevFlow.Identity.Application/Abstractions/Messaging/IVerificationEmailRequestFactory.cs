using DevFlow.Notification.Contracts;

namespace DevFlow.Identity.Application.Abstractions.Messaging;

public interface IVerificationEmailRequestFactory
{
    EmailDeliveryRequestedV1 Create(Guid messageId, string recipient, string displayName, string locale, string rawToken, DateTimeOffset createdAt, DateTimeOffset expiresAt);
}
