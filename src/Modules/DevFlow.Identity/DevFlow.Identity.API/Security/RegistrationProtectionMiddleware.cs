using System.Globalization;
using System.Text.Json;
using DevFlow.Identity.Application.Security;
using DevFlow.Identity.Application.Abstractions.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DevFlow.Identity.API.Security;

public sealed class RegistrationProtectionMiddleware(RequestDelegate next)
{
    public const int MaxBodyBytes = 16 * 1024;

    public async Task InvokeAsync(HttpContext context, IRegistrationRateLimiter limiter)
    {
        if (!HttpMethods.IsPost(context.Request.Method)
            || !string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/api/v1/auth/register", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        CancellationToken cancellationToken = context.RequestAborted;
        if (context.Connection.RemoteIpAddress is not { } ip)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        if (!ApplyLimit(context, await limiter.CheckIpAsync(ip, cancellationToken)))
        {
            return;
        }

        if (context.Request.ContentLength > MaxBodyBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        // Bound chunked bodies too; replay the exact bytes to MVC without logging them.
        await using var buffer = new MemoryStream();
        byte[] chunk = new byte[4096];
        try
        {
            while (true)
            {
                int remaining = (int)Math.Min(chunk.Length, MaxBodyBytes + 1 - buffer.Length);
                int read = await context.Request.Body.ReadAsync(chunk.AsMemory(0, remaining), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBodyBytes)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return;
                }
            }

            try
            {
                using var json = JsonDocument.Parse(buffer.GetBuffer().AsMemory(0, (int)buffer.Length));
                string? email = null;
                if (json.RootElement.ValueKind != JsonValueKind.Object)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                foreach (var property in json.RootElement.EnumerateObject())
                {
                    if (string.Equals(property.Name, "email", StringComparison.OrdinalIgnoreCase))
                    {
                        // Reject ambiguous duplicate email fields rather than letting model binding pick one.
                        if (email is not null || property.Value.ValueKind != JsonValueKind.String)
                        {
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            return;
                        }

                        email = property.Value.GetString();
                    }
                }

                string? normalized = RegistrationEmail.Normalize(email);
                if (normalized is null)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                if (!ApplyLimit(context, await limiter.CheckEmailAsync(normalized, cancellationToken)))
                {
                    return;
                }
            }
            catch (JsonException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            Stream original = context.Request.Body;
            buffer.Position = 0;
            context.Request.Body = buffer;
            try
            {
                await next(context);
            }
            finally
            {
                context.Request.Body = original;
            }
        }
        finally
        {
            Array.Clear(chunk);
            Array.Clear(buffer.GetBuffer());
        }
    }

    private static bool ApplyLimit(HttpContext context, RegistrationLimitResult result)
    {
        if (result.Status == RegistrationLimitStatus.Allowed)
        {
            return true;
        }

        context.Response.StatusCode = result.Status == RegistrationLimitStatus.Limited
            ? StatusCodes.Status429TooManyRequests : StatusCodes.Status503ServiceUnavailable;
        if (result.Status == RegistrationLimitStatus.Limited)
        {
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(result.RetryAfter.TotalSeconds))
                .ToString(CultureInfo.InvariantCulture);
        }

        return false;
    }
}
