using System.Text;
using System.Text.Json;
using DevFlow.Notification.Application.Features.EmailDelivery.Commands.RecordEmailWebhook;
using DevFlow.Notification.Infrastructure.Security;
using DevFlow.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace DevFlow.Notification.API.Webhooks;
[ApiController]
[Route("api/v1/webhooks/email/resend")]
public sealed class ResendWebhookController(ISender sender) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(65536)]
    [ServiceFilter(typeof(ResendSignatureFilter))]
    public async Task<IActionResult> ReceiveAsync(CancellationToken ct)
    {
        var value = HttpContext.Features.Get<VerifiedEmailEvent>()?.Value;
        if (value is null)
        {
            return BadRequest();
        }

        var result = await sender.SendAsync(new RecordEmailWebhookCommand(value), ct);
        return result.IsSuccess ? NoContent() : StatusCode(503);
    }
}
