using Asp.Versioning;
using DevFlow.AspNetCore.Security;
using DevFlow.AspNetCore.Controllers;
using DevFlow.SharedKernel.Abstractions.Shared;
using DevFlow.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DevFlow.AspNetCore.Controllers;

[ApiController]
[ApiVersion(1.0)]
[ServiceFilter(typeof(IntegrationApiKeyFilter))]
[Route("api/v{version:apiVersion}/integrations/[controller]")]
[Produces("application/json")]
public abstract class IntegrationApiController : ControllerBase
{
    protected IntegrationApiController(ISender sender)
    {
        Sender = sender;
    }

    protected ISender Sender { get; }

    protected ActionResult HandleFailure(Result result)
    {
        if (result.Error == Error.None)
        {
            return BadRequest();
        }

        return result.Error.Code switch
        {
            var code when code.EndsWith(".NotFound", StringComparison.Ordinal) => NotFound(result.Error),
            var code when code.Contains("Unauthorized", StringComparison.Ordinal) => Unauthorized(result.Error),
            var code when code.Contains("Forbidden", StringComparison.Ordinal) => StatusCode(StatusCodes.Status403Forbidden, result.Error),
            var code when code.Contains("Conflict", StringComparison.Ordinal) => Conflict(result.Error),
            _ => BadRequest(result.Error),
        };
    }
}
