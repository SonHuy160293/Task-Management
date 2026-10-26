using System.Text;
using System.Text.Json;
using DevFlow.Notification.Application.Abstractions;
using DevFlow.Notification.Infrastructure.Security;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DevFlow.Notification.API.Webhooks;
public sealed record VerifiedEmailEvent(ProviderEmailEvent Value);
public sealed class ResendSignatureFilter(ResendWebhookVerifier verifier) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[4096];
        try
        {
            int count;
            while ((count = await request.Body.ReadAsync(chunk, context.HttpContext.RequestAborted)) > 0)
            {
                if (buffer.Length + count > 65536)
                {
                    context.Result = new StatusCodeResult(413);
                    return;
                }

                buffer.Write(chunk, 0, count);
            }

            var value = verifier.Verify(Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length), request.Headers["svix-id"].ToString(), request.Headers["svix-timestamp"].ToString(), request.Headers["svix-signature"].ToString());
            context.HttpContext.Features.Set(new VerifiedEmailEvent(value));
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            context.Result = new BadRequestResult();
        }
        finally
        {
            Array.Clear(chunk);
            Array.Clear(buffer.GetBuffer());
        }
    }
}
