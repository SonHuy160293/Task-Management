using DevFlow.SharedKernel.Exceptions;

namespace DevFlow.SharedKernel.Validation;

/// <summary>Reusable validation primitives; callers retain their business rules.</summary>
public static class Guard
{
    public static void Require(bool condition, string field, string message)
    {
        if (!condition)
        {
            throw Invalid(field, message);
        }
    }

    public static ValidationException Invalid(string field, string message)
        => new([new ValidationError(field, message)]);

    public static Guid Id(Guid value, string field)
    {
        Require(value != Guid.Empty, field, "A nonempty identifier is required.");
        return value;
    }

    public static string Text(string? value, int maximum, string field, bool rejectControlCharacters = false)
    {
        string text = value?.Trim() ?? string.Empty;
        Require(text.Length > 0 && text.Length <= maximum
            && (!rejectControlCharacters || !text.Any(char.IsControl)),
            field, "A nonempty bounded value is required.");
        return text;
    }

    public static string? OptionalText(string? value, int? maximum, string field)
    {
        string? normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        Require(!maximum.HasValue || normalized.Length <= maximum.Value,
            field, $"{field} must not exceed {maximum} characters.");
        return normalized;
    }

    public static string Hash(string value, string field)
    {
        Require(value is { Length: 64 } && value.All(char.IsAsciiHexDigit),
            field, "A SHA-256 hexadecimal hash is required.");
        return value.ToLowerInvariant();
    }

    public static void DefinedEnum<TEnum>(TEnum value, string field, string message)
        where TEnum : struct, Enum
        => Require(Enum.IsDefined(value), field, message);
}
