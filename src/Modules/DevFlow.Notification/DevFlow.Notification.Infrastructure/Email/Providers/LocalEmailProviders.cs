using System.Net.Mail;
using System.Text.Json;
using DevFlow.Notification.Infrastructure.Email.Templates;
using Microsoft.Extensions.Options;

namespace DevFlow.Notification.Infrastructure.Email.Providers;
public sealed class DisabledEmailProvider : IEmailProvider
{
    public Task<ProviderResult> SendAsync(Guid id, string frozenJson, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ProviderResult("Paused", Code: "DeliveryDisabled"));
    }
}

public sealed class MailpitEmailProvider(IOptions<EmailDeliveryOptions> settings) : IEmailProvider
{
    public async Task<ProviderResult> SendAsync(Guid id, string frozenJson, CancellationToken ct)
    {
        if (!settings.Value.Development)
        {
            throw new InvalidOperationException("Local SMTP is development-only.");
        }

        var message = JsonSerializer.Deserialize<RenderedEmail>(frozenJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        using var smtp = new SmtpClient(settings.Value.MailpitHost, settings.Value.MailpitPort);
        using var mail = new MailMessage(message.From, message.To[0])
        {
            Subject = message.Subject,
            Body = message.Text
        };
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.Html, null, "text/html"));
        await smtp.SendMailAsync(mail, ct);
        return new("Accepted", "mailpit/" + id.ToString("D"));
    }
}
