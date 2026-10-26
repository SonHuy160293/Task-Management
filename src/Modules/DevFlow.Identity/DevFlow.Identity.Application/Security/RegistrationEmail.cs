using System.Net.Mail;
namespace DevFlow.Identity.Application.Security;

public static class RegistrationEmail
{
    public static string? Normalize(string? email)
    {
        string value = email?.Trim() ?? string.Empty;
        return value.Length is >= 3 and <= 320
            && MailAddress.TryCreate(value, out var parsed)
            && string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase)
            ? value.ToLowerInvariant() : null;
    }
}
