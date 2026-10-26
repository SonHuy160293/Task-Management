using System.Security.Cryptography;
using System.Text;
using DevFlow.Notification.Infrastructure.Email;
using DevFlow.Notification.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevFlow.Notification.Tests;
public sealed class WebhookSecurityTests
{
    [Fact]
    public void SignedCallbackIsBoundToRawBodyRejectsReplayAndNeverExposesRecipient()
    {
        string directory = Path.Combine(Path.GetTempPath(), "devflow-mail-webhook-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        byte[] key = RandomNumberGenerator.GetBytes(32);
        string secret = Path.Combine(directory, "webhook.secret");
        string hmac = Path.Combine(directory, "recipient.secret");
        File.WriteAllText(secret, "whsec_" + Convert.ToBase64String(key));
        File.WriteAllText(hmac, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        try
        {
            var options = Options.Create(new EmailDeliveryOptions { Mode = "Resend", WebhookSecretFile = secret, RecipientHashKeyFile = hmac });
            var verifier = new ResendWebhookVerifier(options, new EmailPayloadProtection(options));
            const string body = "{\"type\":\"email.delivered\",\"data\":{\"email_id\":\"test-id\",\"to\":[\"alice@example.test\"]}}";
            string id = "msg_test";
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            string Sign(string time) => "v1," + Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(id + "." + time + "." + body)));
            var result = verifier.Verify(body, id, timestamp, Sign(timestamp));
            Assert.Equal("email.delivered", result.Type);
            Assert.Equal(64, result.RecipientHash!.Length);
            Assert.DoesNotContain("alice", result.ToString());
            Assert.Throws<ArgumentException>(() => verifier.Verify(body + " ", id, timestamp, Sign(timestamp)));
            string old = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Throws<ArgumentException>(() => verifier.Verify(body, id, old, Sign(old)));
            Assert.Throws<ArgumentException>(() => verifier.Verify(body, id, timestamp, "v1,bad"));
        }
        finally
        {
            File.Delete(secret);
            File.Delete(hmac);
            Directory.Delete(directory);
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
