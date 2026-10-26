using System.Text;

namespace DevFlow.Notification.Contracts;
public sealed record EmailDeliveryRequestedV1(Guid MessageId, Guid CorrelationId, Guid? CausationId, string Producer, string Purpose, string TemplateId, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, EncryptedEmailPayload Payload)
{
    public const string EventType = "notification.email-requested.v1";
    public byte[] AssociatedData() => Encoding.UTF8.GetBytes($"{MessageId:D}|{Producer}|{Purpose}|{TemplateId}|1|{CreatedAt.UtcTicks}|{ExpiresAt.UtcTicks}");
    public void Validate()
    {
        if (MessageId.Version != 7 || CorrelationId == Guid.Empty || Producer != "identity" || Purpose != "identity.verify-email" || TemplateId != "verify-email.v1" || CreatedAt.Offset != TimeSpan.Zero || ExpiresAt.Offset != TimeSpan.Zero || ExpiresAt <= CreatedAt || ExpiresAt - CreatedAt > TimeSpan.FromDays(1) || Payload is null || Payload.Version != 1 || Payload.KeyId is null || Payload.KeyId.Length is < 1 or > 64 || Payload.Ciphertext is null || Payload.Ciphertext.Length is < 1 or > 32768 || Payload.WrappedKey is null || Payload.WrappedKey.Length is < 1 or > 2048 || Payload.Nonce is null || Payload.Nonce.Length is < 1 or > 32 || Payload.Tag is null || Payload.Tag.Length is < 1 or > 32)
        {
            throw new ArgumentException("Invalid email delivery contract.");
        }
    }

    public override string ToString() => $"EmailDeliveryRequestedV1 {MessageId} [REDACTED]";
}
