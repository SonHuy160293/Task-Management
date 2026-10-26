using System.Net;
using System.Security.Cryptography;
using System.Text;
using DevFlow.AspNetCore.Middleware;
using DevFlow.Notification.API.Webhooks;
using DevFlow.Notification.Application;
using DevFlow.Notification.Infrastructure;
using DevFlow.Notification.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevFlow.Notification.Tests;
public sealed class WebhookHttpIntegrationTests
{
    [DeliveryDatabaseFact]
    public async Task SignedHttpCallbackCommitsBefore204AndDeduplicatesAndRejectsForgery()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        byte[] key = RandomNumberGenerator.GetBytes(32);
        string secret = Path.Combine(fixture.Keys, "http-webhook.secret");
        File.WriteAllText(secret, "whsec_" + Convert.ToBase64String(key));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:NotificationDb"] = fixture.NotificationConnection, ["Notification:EmailDelivery:Mode"] = "Resend", ["Notification:EmailDelivery:WebhookSecretFile"] = secret, ["Notification:EmailDelivery:RecipientHashKeyFile"] = Path.Combine(fixture.Keys, "recipient-hmac.secret"), });
        builder.Services.AddNotificationApplication().AddNotificationPersistence(builder.Configuration).AddNotificationWebhookSecurity(builder.Configuration);
        builder.Services.AddScoped<ResendSignatureFilter>();
        builder.Services.AddControllers().AddApplicationPart(typeof(ResendWebhookController).Assembly);
        await using var app = builder.Build();
        app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        const string body = "{\"type\":\"email.delivered\",\"data\":{\"email_id\":\"http-test-id\",\"to\":[\"alice@example.test\"]}}";
        const string id = "msg_http_test";
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        string signature = "v1," + Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(id + "." + timestamp + "." + body)));
        HttpRequestMessage Request(string payload, string sig)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/email/resend")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("svix-id", id);
            request.Headers.Add("svix-timestamp", timestamp);
            request.Headers.Add("svix-signature", sig);
            return request;
        }

        using (var request = Request(body, signature))
        {
            using (var result = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
            }
        }

        using (var request = Request(body, signature))
        {
            using (var result = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
            }
        }

        await using var db = fixture.Notification();
        Assert.Single(await db.EmailWebhookReceipts.ToListAsync());
        using (var request = Request(body + " ", signature))
        {
            using (var result = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            }
        }

        using (var request = Request(new string ('x', 66000), signature))
        {
            using (var result = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.RequestEntityTooLarge, result.StatusCode);
            }
        }

        Assert.Single(await db.EmailWebhookReceipts.ToListAsync());
    }
}
