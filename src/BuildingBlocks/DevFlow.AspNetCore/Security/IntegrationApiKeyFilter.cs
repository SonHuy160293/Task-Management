using System.Security.Cryptography;
using System.Text;
using DevFlow.AspNetCore.Options;
using DevFlow.SharedKernel.Abstractions.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace DevFlow.AspNetCore.Security;

public sealed class IntegrationApiKeyFilter : IAsyncAuthorizationFilter
{
    private const int MaximumApiKeyLength = 4096;

    private readonly IntegrationApiOptions _options;

    public IntegrationApiKeyFilter(IOptions<IntegrationApiOptions> options)
    {
        _options = options.Value;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (_options.ApiKeys.Length == 0)
        {
            context.Result = CreateErrorResult(
                StatusCodes.Status503ServiceUnavailable,
                "IntegrationApi.NotConfigured",
                "Integration API authentication is not configured.");

            return Task.CompletedTask;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(_options.HeaderName, out var headerValues)
            || headerValues.Count != 1
            || string.IsNullOrWhiteSpace(headerValues.FirstOrDefault())
            || headerValues.First()!.Length > MaximumApiKeyLength)
        {
            context.Result = CreateErrorResult(
                StatusCodes.Status401Unauthorized,
                "IntegrationApi.MissingApiKey",
                $"Missing {_options.HeaderName} header.");

            return Task.CompletedTask;
        }

        var apiKey = headerValues.First();
        var isValid = apiKey is not null && _options.ApiKeys.Any(configuredKey => FixedTimeEquals(apiKey, configuredKey));

        if (!isValid)
        {
            context.Result = CreateErrorResult(
                StatusCodes.Status401Unauthorized,
                "IntegrationApi.InvalidApiKey",
                "Integration API key is invalid.");
        }

        return Task.CompletedTask;
    }

    private static ObjectResult CreateErrorResult(int statusCode, string code, string message)
        => new(new ApiErrorResponse(code, message))
        {
            StatusCode = statusCode
        };

    private static bool FixedTimeEquals(string value, string expected)
    {
        var valueBytes = Encoding.UTF8.GetBytes(value);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return valueBytes.Length == expectedBytes.Length
               && CryptographicOperations.FixedTimeEquals(valueBytes, expectedBytes);
    }
}
