using System.Text.Json;
using DevFlow.Notification.Application.Features.EmailDelivery.Commands.AcceptEmailDelivery;
using DevFlow.Notification.Infrastructure.Email;
using DevFlow.Notification.Infrastructure.Email.Workers;
using DevFlow.Notification.Infrastructure.Persistence;
using DevFlow.SharedKernel.Cqrs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevFlow.Notification.Tests;
public sealed class DeliveryMailpitFactAttribute : FactAttribute
{
    public DeliveryMailpitFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_POSTGRES")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_SMTP_PORT")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_MAILPIT_URL")))
        {
            Skip = "Supply disposable PostgreSQL and Mailpit endpoints to verify actual local SMTP delivery.";
        }
    }
}

public sealed class MailpitIntegrationTests
{
    [DeliveryMailpitFact]
    public async Task RealSmtpDeliveryContainsAccessibleVerificationButtonAndPlainText()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        await using var services = fixture.Services(s => s.Configure<EmailDeliveryOptions>(o =>
        {
            o.MailpitPort = int.Parse(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_SMTP_PORT")!);
            o.MailpitHost = "127.0.0.1";
        }));
        var request = PersistenceIntegrationTests.Request(fixture);
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptEmailDeliveryCommand(request), default);
        }

        await new EmailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDeliveryWorker>.Instance).RunOnceAsync(default);
        await using var db = fixture.Notification();
        Assert.Equal("ProviderAccepted", (await db.EmailDeliveries.SingleAsync()).Status);
        using var client = new HttpClient
        {
            BaseAddress = new(Environment.GetEnvironmentVariable("DEVFLOW_DELIVERY_TEST_MAILPIT_URL")!)
        };
        using var list = JsonDocument.Parse(await client.GetStringAsync("api/v1/messages"));
        var message = list.RootElement.GetProperty("messages").EnumerateArray().First(x => x.GetProperty("Subject").GetString() == "Verify your DevFlow email");
        string id = message.GetProperty("ID").GetString()!;
        using var content = JsonDocument.Parse(await client.GetStringAsync("api/v1/message/" + id));
        Assert.Contains("Verify email", content.RootElement.GetProperty("HTML").GetString());
        Assert.Contains("#token=", content.RootElement.GetProperty("Text").GetString());
    }
}
