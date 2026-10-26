namespace DevFlow.SharedKernel.Abstractions.Shared;

public sealed record ApiResponse(
    bool Success,
    object? Data,
    ApiErrorResponse? Error)
{
    public static ApiResponse Ok(object? data = null)
        => new(true, data, null);

    public static ApiResponse Fail(ApiErrorResponse error)
        => new(false, null, error);
}
