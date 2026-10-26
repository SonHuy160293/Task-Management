using System.Net;

namespace DevFlow.SharedKernel.Integrations;

public class IntegrationResult
{
    protected IntegrationResult(bool isSuccess, HttpStatusCode? statusCode, IntegrationError? error)
    {
        IsSuccess = isSuccess;
        StatusCode = statusCode;
        Error = error;
    }

    public bool IsSuccess { get; }
    public TimeSpan? RetryAfter { get; init; }

    public HttpStatusCode? StatusCode { get; }

    public IntegrationError? Error { get; }

    public static IntegrationResult Success(HttpStatusCode statusCode) => new(true, statusCode, null);

    public static IntegrationResult Failure(HttpStatusCode? statusCode, IntegrationError error)
        => new(false, statusCode, error);
}
