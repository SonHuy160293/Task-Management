using DevFlow.SharedKernel.Abstractions.Shared;

namespace DevFlow.Gateway;

internal static class GatewayErrors
{
    public static async Task WriteAsync(HttpContext context, int status, string code)
    {
        context.Items["GatewayErrorCode"] = code;
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested)
        {
            context.Abort();
            return;
        }

        string allow = context.Response.Headers.Allow.ToString();
        context.Response.Clear();
        context.Response.StatusCode = status;
        if (status == 405 && allow.Length > 0)
        {
            context.Response.Headers.Allow = allow;
        }
        string message = status switch
        {
            400 => "The request is invalid.",
            404 => "The route was not found.",
            405 => "The HTTP method is not allowed.",
            413 => "The request body is too large.",
            502 => "The destination could not complete the request.",
            503 => "No destination is available.",
            504 => "The request timed out.",
            _ => "An unexpected gateway error occurred.",
        };
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(new ApiErrorResponse(code, message, TraceId: context.TraceIdentifier)), CancellationToken.None);
    }
}
