using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using DevFlow.Notification.Contracts;

namespace DevFlow.Notification.Infrastructure.Email.Templates;
public sealed record VerificationEmailData(string Recipient, string DisplayName, string Locale, string Token)
{
    public override string ToString() => "VerificationEmailData [REDACTED]";
}

public sealed record RenderedEmail(string From, string[] To, string Subject, string Html, string Text)
{
    public override string ToString() => "RenderedEmail [REDACTED]";
}

public static class VerificationTemplate
{
    public static RenderedEmail Render(EmailDeliveryRequestedV1 request, ReadOnlySpan<byte> data, EmailDeliveryOptions options)
    {
        request.Validate();
        var payload = JsonSerializer.Deserialize<VerificationEmailData>(data) ?? throw new ArgumentException("Invalid template data.");
        if (!MailAddress.TryCreate(payload.Recipient, out var address) || address.Address != payload.Recipient || payload.DisplayName is null || payload.DisplayName.Length > 160 || payload.Locale is null || payload.Locale.Length > 16 || payload.Token is null || !Regex.IsMatch(payload.Token, "^[A-Za-z0-9_-]{43}$", RegexOptions.NonBacktracking))
        {
            throw new ArgumentException("Invalid template data.");
        }

        var origin = new Uri(options.FrontendOrigin, UriKind.Absolute);
        if (origin.AbsolutePath != "/" || origin.Query.Length > 0 || origin.Fragment.Length > 0 || origin.UserInfo.Length > 0 || origin.Scheme != "https" && !(options.Development && origin.IsLoopback))
        {
            throw new ArgumentException("Invalid frontend origin.");
        }

        string link = new Uri(origin, "/verify-email").AbsoluteUri + "#token=" + payload.Token;
        string expiry = request.ExpiresAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        string name = WebUtility.HtmlEncode(payload.DisplayName);
        string url = WebUtility.HtmlEncode(link);
        string html = $"<!doctype html><html lang=\"en\"><body style=\"font-family:Arial,sans-serif;color:#162033\"><main style=\"max-width:560px;margin:auto;padding:24px\"><h1>Verify your DevFlow email</h1><p>Hello {name},</p><p>Confirm your email address to finish setting up your DevFlow account.</p><p><a href=\"{url}\" style=\"display:inline-block;background:#2449c8;color:white;padding:14px 24px;border-radius:6px\">Verify email</a></p><p>This link expires at {expiry}.</p><p>If the button does not work, copy this link:</p><p style=\"overflow-wrap:anywhere\">{url}</p><p>If you did not request this, you can ignore this email.</p></main></body></html>";
        string text = $"Hello {payload.DisplayName},\nVerify your DevFlow email: {link}\nExpires: {expiry}.\nIf you did not request this, you can ignore this email.";
        return new(options.Sender, [address.Address], "Verify your DevFlow email", html, text);
    }
}
