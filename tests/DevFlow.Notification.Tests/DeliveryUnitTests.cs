using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevFlow.AspNetCore.HttpClient.Clients;
using DevFlow.EntityFrameworkCore.Reliability;
using DevFlow.Identity.Infrastructure.Messaging;
using DevFlow.Messaging.Security;
using DevFlow.Notification.Contracts;
using DevFlow.Notification.Domain.Models;
using DevFlow.Notification.Infrastructure.Email;
using DevFlow.Notification.Infrastructure.Email.Templates;
using Xunit;

namespace DevFlow.Notification.Tests;
public sealed class DeliveryUnitTests
{
    [Fact]
    public void EnvelopeIsAuthenticatedBoundToMetadataAndUsesRandomKeys()
    {
        using var rsa = RSA.Create(3072);
        byte[] plain = Encoding.UTF8.GetBytes("secret verification token");
        byte[] aad = Encoding.UTF8.GetBytes("purpose/message-id");
        var a = EnvelopeEncryption.Encrypt(plain, aad, rsa.ExportSubjectPublicKeyInfoPem());
        var b = EnvelopeEncryption.Encrypt(plain, aad, rsa.ExportSubjectPublicKeyInfoPem());
        Assert.NotEqual(a.Ciphertext, b.Ciphertext);
        Assert.Equal(plain, EnvelopeEncryption.Decrypt(a, aad, rsa.ExportPkcs8PrivateKeyPem()));
        Assert.ThrowsAny<CryptographicException>(() => EnvelopeEncryption.Decrypt(a, "different"u8, rsa.ExportPkcs8PrivateKeyPem()));
        byte[] tampered = Convert.FromBase64String(a.Ciphertext);
        tampered[0] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => EnvelopeEncryption.Decrypt(a with { Ciphertext = Convert.ToBase64String(tampered) }, aad, rsa.ExportPkcs8PrivateKeyPem()));
        Assert.DoesNotContain("secret", a.ToString());
    }

    [Fact]
    public void ProducerEncryptsTokenAndTemplateEscapesMarkupAndUsesFragment()
    {
        using var rsa = RSA.Create(3072);
        var now = DateTimeOffset.UtcNow;
        var request = VerificationEmailEnvelopeFactory.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), null, "alice@example.test", "<script>bad</script>", "unknown", new string ('a', 43), now, now.AddHours(24), "v1", rsa.ExportSubjectPublicKeyInfoPem());
        string json = JsonSerializer.Serialize(request);
        Assert.DoesNotContain("alice@example.test", json);
        Assert.DoesNotContain(new string ('a', 43), json);
        byte[] plain = EnvelopeEncryption.Decrypt(new(request.Payload.WrappedKey, request.Payload.Nonce, request.Payload.Tag, request.Payload.Ciphertext), request.AssociatedData(), rsa.ExportPkcs8PrivateKeyPem());
        var rendered = VerificationTemplate.Render(request, plain, new() { Sender = "DevFlow <auth@example.test>", FrontendOrigin = "https://app.example.test" });
        Assert.Contains("&lt;script&gt;", rendered.Html);
        Assert.DoesNotContain("<script>", rendered.Html);
        Assert.Contains("/verify-email#token=", rendered.Text);
        Assert.DoesNotContain("?token=", rendered.Html);
        Assert.Contains("Verify email", rendered.Html);
        Assert.Contains("UTC", rendered.Text);
        Assert.DoesNotContain(new string ('a', 43), rendered.ToString());
        Assert.Throws<ArgumentException>(() => VerificationTemplate.Render(request, plain, new() { FrontendOrigin = "https://app.example.test/evil" }));
    }

    [Fact]
    public void ExpiredAndAmbiguousDeliveriesCannotBeBlindlyRedriven()
    {
        var now = DateTimeOffset.UtcNow;
        var job = EmailDelivery.Create(Guid.CreateVersion7(), new string ('A', 64), "encrypted", now, now.AddHours(24));
        job.Claim(now);
        job.BeginAttempt(now);
        Assert.False(job.CanSend(now.AddHours(23)));
        job.Expire();
        Assert.Equal("AcceptanceUnknown", job.FailureCode);
        Assert.Throws<InvalidOperationException>(() => job.Redrive(now));
        Assert.Equal("{}", job.RequestJson);
    }

    [Fact]
    public void OutcomeCannotUndoBounceOrComplaintAndAcceptanceClearsSecrets()
    {
        var now = DateTimeOffset.UtcNow;
        var job = EmailDelivery.Create(Guid.CreateVersion7(), new string ('A', 64), "secret ciphertext", now, now.AddHours(24));
        job.Claim(now);
        job.Freeze("frozen secret");
        job.Accept("provider-id");
        Assert.Null(job.FrozenRequestJson);
        Assert.Equal("{}", job.RequestJson);
        job.RecordOutcome("Bounced");
        job.RecordOutcome("Delivered");
        Assert.Equal("Bounced", job.Outcome);
    }

    [Fact]
    public void OutboxRequiresLeaseAndExplicitRecoveryBeforeRepublishing()
    {
        var now = DateTimeOffset.UtcNow;
        var token = Guid.CreateVersion7();
        var row = OutboxMessage.Create(Guid.CreateVersion7(), "test.v1", 1, Guid.CreateVersion7(), 1, "{}", Guid.CreateVersion7(), null, now);
        row.Claim(token, now, now.AddMinutes(1));
        Assert.ThrowsAny<Exception>(() => row.MarkPublished(Guid.CreateVersion7(), now));
        row.MarkPublished(token, now);
        row.RequeuePublished(now.AddMinutes(5));
        Assert.Equal(OutboxStatus.Pending, row.Status);
        Assert.Null(row.PublishedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReconciliationRequiresAnAttemptAndNeverResends(bool attempted)
    {
        var now = DateTimeOffset.UtcNow;
        var job = EmailDelivery.Create(Guid.CreateVersion7(), new string('A', 64), "encrypted", now, now.AddHours(24));
        if (!attempted)
        {
            Assert.Throws<InvalidOperationException>(() => job.ReconcileAccepted(now, "provider-reference"));
            return;
        }
        job.Claim(now);
        job.BeginAttempt(now);
        Assert.Throws<InvalidOperationException>(() => job.ReconcileAccepted(now, "provider-reference"));
        job.Expire();
        job.ReconcileAccepted(now, "provider-reference");
        job.ReconcileAccepted(now, "provider-reference");
        Assert.Equal("ProviderAccepted", job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.Null(job.FrozenRequestJson);
    }

    [Theory]
    [InlineData("Mailpit", false, false)]
    [InlineData("Resend", false, false)]
    [InlineData("Disabled", false, true)]
    public void ConfigurationFailsClosed(string mode, bool development, bool valid) => Assert.Equal(valid, new EmailDeliveryOptions { Mode = mode, Development = development }.Valid());
    [Fact]
    public async Task ExternalTransportPreservesIdempotencyAndRetryAfterWithoutLeakingProviderBody()
    {
        var handler = new HttpStub();
        using var client = new HttpClient(handler)
        {
            BaseAddress = new("https://api.resend.com/")
        };
        var transport = new IntegrationHttpClient<object>(client);
        var failed = await transport.PostExternalJsonAsync<JsonElement>("emails", "{}", "devflow/email/" + Guid.CreateVersion7());
        Assert.Equal(TimeSpan.FromSeconds(13), failed.RetryAfter);
        Assert.Equal(HttpStatusCode.TooManyRequests, failed.StatusCode);
        Assert.DoesNotContain("sensitive", failed.Error!.Message);
        Assert.StartsWith("devflow/email/", handler.Idempotency);
        Assert.Equal(1, handler.Calls);
    }

    private sealed class HttpStub : HttpMessageHandler
    {
        public string Idempotency { get; private set; } = "";
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Idempotency = request.Headers.GetValues("Idempotency-Key").Single();
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("sensitive downstream content")
            };
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(13));
            return Task.FromResult(response);
        }
    }
}
