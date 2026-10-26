using System.Net;

namespace DevFlow.SharedKernel.Integrations;

public sealed class IntegrationResult<T> : IntegrationResult
{
    private IntegrationResult(T? data, bool isSuccess, HttpStatusCode? statusCode, IntegrationError? error)
        : base(isSuccess, statusCode, error)
    {
        Data = data;
    }

    public T? Data { get; }

    public IntegrationResult<T> WithRetryAfter(TimeSpan? delay)
        => new(Data, IsSuccess, StatusCode, Error) { RetryAfter = delay };

    public static IntegrationResult<T> Success(T data, HttpStatusCode statusCode)
        => new(data, true, statusCode, null);

    public static new IntegrationResult<T> Failure(HttpStatusCode? statusCode, IntegrationError error)
        => new(default, false, statusCode, error);
}
