using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevFlow.Messaging.Security;
using DevFlow.Notification.Contracts;
using DevFlow.Notification.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace DevFlow.Notification.Infrastructure.Security;
public sealed class EmailPayloadProtection(IOptions<EmailDeliveryOptions> settings)
{
    private readonly EmailDeliveryOptions _options = settings.Value;
    private string KeyPath(string id, string suffix)
    {
        if (id.Length is < 1 or > 64 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
        {
            throw new CryptographicException("Invalid key identifier.");
        }

        return Path.Combine(_options.KeyDirectory, id + suffix);
    }

    public byte[] Decrypt(EmailDeliveryRequestedV1 request) => EnvelopeEncryption.Decrypt(new(request.Payload.WrappedKey, request.Payload.Nonce, request.Payload.Tag, request.Payload.Ciphertext), request.AssociatedData(), File.ReadAllText(KeyPath(request.Payload.KeyId, ".private.pem")));
    public string Freeze(Guid id, ReadOnlySpan<byte> plain)
    {
        var cipher = EnvelopeEncryption.Encrypt(plain, Encoding.UTF8.GetBytes($"{id:D}|rendered:v1"), File.ReadAllText(KeyPath(_options.ActiveKeyId, ".public.pem")));
        return JsonSerializer.Serialize(new FrozenPayload(_options.ActiveKeyId, cipher));
    }

    public byte[] Thaw(Guid id, string json)
    {
        var payload = JsonSerializer.Deserialize<FrozenPayload>(json) ?? throw new CryptographicException("Invalid frozen payload.");
        return EnvelopeEncryption.Decrypt(payload.Envelope, Encoding.UTF8.GetBytes($"{id:D}|rendered:v1"), File.ReadAllText(KeyPath(payload.KeyId, ".private.pem")));
    }

    public string RecipientHash(string recipient)
    {
        byte[] key = Convert.FromBase64String(File.ReadAllText(_options.RecipientHashKeyFile).Trim());
        try
        {
            if (key.Length < 32)
            {
                throw new CryptographicException("Invalid suppression key.");
            }

            return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(recipient.Trim().ToLowerInvariant())));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private sealed record FrozenPayload(string KeyId, CipherEnvelope Envelope);
}
