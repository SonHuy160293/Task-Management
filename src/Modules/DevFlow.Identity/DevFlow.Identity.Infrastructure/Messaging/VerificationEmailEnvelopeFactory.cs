using System.Security.Cryptography;
using System.Text.Json;
using DevFlow.Messaging.Security;
using DevFlow.Notification.Contracts;

namespace DevFlow.Identity.Infrastructure.Messaging;
public static class VerificationEmailEnvelopeFactory
{
    public static EmailDeliveryRequestedV1 Create(Guid messageId, Guid correlationId, Guid? causationId, string recipient, string displayName, string locale, string rawToken, DateTimeOffset created, DateTimeOffset expires, string keyId, string publicKeyPem)
    {
        var blank = new EncryptedEmailPayload(1, keyId, "", "", "", "");
        var request = new EmailDeliveryRequestedV1(messageId, correlationId, causationId, "identity", "identity.verify-email", "verify-email.v1", created, expires, blank);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(new { Recipient = recipient, DisplayName = displayName, Locale = locale, Token = rawToken });
        try
        {
            var encrypted = EnvelopeEncryption.Encrypt(plain, request.AssociatedData(), publicKeyPem);
            var result = request with
            {
                Payload = new(1, keyId, encrypted.WrappedKey, encrypted.Nonce, encrypted.Tag, encrypted.Ciphertext)
            };
            result.Validate();
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }
}
