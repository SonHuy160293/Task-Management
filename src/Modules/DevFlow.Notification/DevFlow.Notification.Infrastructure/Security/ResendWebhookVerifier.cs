using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevFlow.Notification.Application.Abstractions;
using DevFlow.Notification.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace DevFlow.Notification.Infrastructure.Security;
public sealed class ResendWebhookVerifier(IOptions<EmailDeliveryOptions> settings, EmailPayloadProtection protection)
{
    public ProviderEmailEvent Verify(string body, string id, string timestamp, string signature)
    {
        if (settings.Value.Mode != "Resend" || body.Length > 65536 || id.Length is < 1 or > 200 || timestamp.Length > 20 || signature.Length > 2048)
        {
            throw new ArgumentException("Invalid callback.");
        }

        var headers = new WebHeaderCollection
        {
            ["svix-id"] = id,
            ["svix-timestamp"] = timestamp,
            ["svix-signature"] = signature
        };
        try
        {
            new Svix.Webhook(File.ReadAllText(settings.Value.WebhookSecretFile).Trim()).Verify(body, headers);
        }
        catch (Exception ex)when (ex is not IOException and not UnauthorizedAccessException)
        {
            throw new ArgumentException("Invalid callback signature.");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        string type = root.GetProperty("type").GetString() ?? "";
        string providerId = root.GetProperty("data").GetProperty("email_id").GetString() ?? "";
        if (type.Length is < 1 or > 64 || providerId.Length is < 1 or > 200)
        {
            throw new ArgumentException("Invalid callback.");
        }

        string? recipient = null;
        if (root.GetProperty("data").TryGetProperty("to", out var to) && to.ValueKind == JsonValueKind.Array && to.GetArrayLength() == 1)
        {
            recipient = to[0].GetString();
        }

        return new(id, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))), providerId, type, recipient is null ? null : protection.RecipientHash(recipient));
    }
}
