using System.Text.Json;
using DevFlow.SharedKernel.Validation;

namespace DevFlow.EntityFrameworkCore.Reliability;

internal static class ReliabilityGuard
{
    public static void Require(bool condition, string field, string message)
        => Guard.Require(condition, field, message);

    public static Guid Id(Guid value, string field)
        => Guard.Id(value, field);

    public static string Code(string value, int length, string field)
    {
        string normalized = value?.Trim() ?? string.Empty;
        Require(normalized.Length > 0 && normalized.Length <= length
            && normalized.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or ':'),
            field, "A bounded ASCII code is required.");
        return normalized;
    }

    public static string Hash(string value, string field)
        => Guard.Hash(value, field);

    public static string Json(string value)
    {
        Require(!string.IsNullOrWhiteSpace(value), nameof(value), "JSON payload is required.");
        try
        {
            using var document = JsonDocument.Parse(value);
        }
        catch (JsonException)
        {
            throw Guard.Invalid(nameof(value), "Payload must be valid JSON.");
        }

        return value;
    }

    public static void Lease(Guid token, DateTimeOffset now, DateTimeOffset until)
    {
        Id(token, nameof(token));
        Require(until > now, nameof(until), "Lease deadline must be later than the current time.");
    }

    public static void OwnLease(Guid expected, Guid? actual, DateTimeOffset? until, DateTimeOffset now)
        => Require(expected != Guid.Empty && actual == expected && until.HasValue && until.Value > now,
            nameof(expected), "A current matching lease is required.");
}
