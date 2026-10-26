using Microsoft.AspNetCore.Builder;

namespace DevFlow.AspNetCore.Middleware;

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();

    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

    public static IApplicationBuilder UseApiStatusCodeEnvelope(this IApplicationBuilder app)
        => app.UseMiddleware<ApiStatusCodeEnvelopeMiddleware>();
}
