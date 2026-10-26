using DevFlow.SharedKernel.Abstractions.Shared;
using Microsoft.AspNetCore.Http;

namespace DevFlow.AspNetCore.Middleware;

public sealed class ApiStatusCodeEnvelopeMiddleware
{
    private readonly RequestDelegate _next;

    public ApiStatusCodeEnvelopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (!ShouldWriteEnvelope(context))
        {
            return;
        }

        var statusCode = context.Response.StatusCode;

        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(new ApiErrorResponse(
            GetDefaultCode(statusCode),
            GetDefaultMessage(statusCode),
            TraceId: context.TraceIdentifier)));
    }

    private static bool ShouldWriteEnvelope(HttpContext context)
    {
        var response = context.Response;

        return context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && response.StatusCode >= StatusCodes.Status400BadRequest
            && !response.HasStarted
            && response.ContentLength is null
            && string.IsNullOrWhiteSpace(response.ContentType);
    }

    private static string GetDefaultCode(int statusCode)
        => statusCode switch
        {
            StatusCodes.Status400BadRequest => "Request.BadRequest",
            StatusCodes.Status401Unauthorized => "Request.Unauthorized",
            StatusCodes.Status403Forbidden => "Request.Forbidden",
            StatusCodes.Status404NotFound => "Request.NotFound",
            StatusCodes.Status405MethodNotAllowed => "Request.MethodNotAllowed",
            StatusCodes.Status415UnsupportedMediaType => "Request.UnsupportedMediaType",
            _ => "Request.Failed"
        };

    private static string GetDefaultMessage(int statusCode)
        => statusCode switch
        {
            StatusCodes.Status400BadRequest => "The request is invalid.",
            StatusCodes.Status401Unauthorized => "Authentication is required.",
            StatusCodes.Status403Forbidden => "Access is forbidden.",
            StatusCodes.Status404NotFound => "The resource was not found.",
            StatusCodes.Status405MethodNotAllowed => "The HTTP method is not allowed for this endpoint.",
            StatusCodes.Status415UnsupportedMediaType => "The request content type is not supported.",
            _ => "The request failed."
        };
}
