using System.Globalization;
using DevFlow.Identity.Application.Security;
using DevFlow.Identity.Application.Abstractions.Security;
using DevFlow.SharedKernel.Abstractions.Message;

namespace DevFlow.Identity.Application.Features.Auth.Commands.Registration;

public sealed class RegisterAccountValidator : IValidator<RegisterAccountCommand>
{
    public Task ValidateAsync(RegisterAccountCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<string>();

        if (RegistrationEmail.Normalize(request.Email) is null)
        {
            errors.Add("Email must be a valid address between 3 and 320 characters.");
        }

        string displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length is < 1 or > 160)
        {
            errors.Add("DisplayName must be between 1 and 160 characters after trimming.");
        }

        if (!PasswordPolicy.IsValid(request.Password))
        {
            errors.Add($"Password must contain between {PasswordPolicy.MinimumLength} and {PasswordPolicy.MaximumLength} Unicode characters.");
        }

        if (!IsValidLocale(request.Locale))
        {
            errors.Add("Locale must be a valid language tag no longer than 16 characters.");
        }

        if (!IsValidTimeZone(request.TimeZone))
        {
            errors.Add("TimeZone must be a valid IANA time-zone identifier no longer than 64 characters.");
        }

        if (errors.Count > 0)
        {
            throw new CqrsValidationException(errors);
        }

        return Task.CompletedTask;
    }

    private static bool IsValidLocale(string? locale)
    {
        string value = string.IsNullOrWhiteSpace(locale) ? "en" : locale.Trim();
        if (value.Length > 16 || value.Contains('_', StringComparison.Ordinal)
            || !System.Text.RegularExpressions.Regex.IsMatch(
                value,
                "^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*$",
                System.Text.RegularExpressions.RegexOptions.NonBacktracking))
        {
            return false;
        }

        try
        {
            _ = CultureInfo.GetCultureInfo(value);
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private static bool IsValidTimeZone(string? timeZone)
    {
        string value = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone.Trim();
        if (value.Length > 64 || (!value.Contains('/', StringComparison.Ordinal) && value != "UTC"))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(value);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
