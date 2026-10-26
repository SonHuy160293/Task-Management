using DevFlow.AspNetCore.HttpClient.Options;
using DevFlow.AspNetCore.HttpClient.Options.Resilience;
using Microsoft.Extensions.Options;

namespace DevFlow.AspNetCore.HttpClient.Validation;

internal sealed class IntegrationClientOptionsValidator(string name) : IValidateOptions<IntegrationClientOptions>
{
    public ValidateOptionsResult Validate(string? optionsName, IntegrationClientOptions options)
    {
        if (!string.Equals(optionsName, name, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Skip;
        }

        var failures = new List<string>();
        if (options.BaseUrl is null || !options.BaseUrl.IsAbsoluteUri
            || (options.BaseUrl.Scheme != Uri.UriSchemeHttp && options.BaseUrl.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add($"Integrations:{name}:BaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKeyHeaderName)
            || !IsValidHeaderName(options.ApiKeyHeaderName))
        {
            failures.Add($"Integrations:{name}:ApiKeyHeaderName is invalid.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add($"Integrations:{name}:ApiKey is required and should be supplied by a secret provider.");
        }

        ValidateResilience(options.Resilience, failures);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidHeaderName(string value)
        => value.Length <= 100 && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    private static void ValidateResilience(IntegrationResilienceOptions options, ICollection<string> failures)
    {
        if (options.TotalRequestTimeout.Timeout <= TimeSpan.Zero)
        {
            failures.Add("Total request timeout must be positive.");
        }

        if (options.AttemptTimeout.Timeout <= TimeSpan.Zero
            || options.AttemptTimeout.Timeout > options.TotalRequestTimeout.Timeout)
        {
            failures.Add("Attempt timeout must be positive and no greater than the total request timeout.");
        }

        if (options.Retry.MaxRetryAttempts <= 0 || options.Retry.Delay < TimeSpan.Zero)
        {
            failures.Add("Retry attempts must be positive and delay cannot be negative.");
        }

        if (options.Retry.BackoffType != "Constant"
            && options.Retry.BackoffType != "Linear"
            && options.Retry.BackoffType != "Exponential")
        {
            failures.Add("Retry BackoffType must be Constant, Linear, or Exponential.");
        }

        if (options.CircuitBreaker.FailureRatio is <= 0 or > 1
            || options.CircuitBreaker.MinimumThroughput <= 0
            || options.CircuitBreaker.SamplingDuration <= TimeSpan.Zero
            || options.CircuitBreaker.BreakDuration <= TimeSpan.Zero)
        {
            failures.Add("Circuit-breaker values must be positive and FailureRatio must be at most 1.");
        }
    }
}
