using Asp.Versioning;
using DevFlow.Identity.API.Contracts.Auth;
using DevFlow.Identity.Application.Features.Auth.Commands.Registration;
using DevFlow.SharedKernel.Abstractions.Shared;
using DevFlow.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DevFlow.Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[AllowAnonymous]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<RegistrationAcceptedResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RegistrationAcceptedResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        Result<RegistrationAcceptedResponse> result = await sender.SendAsync(
            new RegisterAccountCommand(request.Email, request.Password, request.DisplayName, request.Locale, request.TimeZone),
            cancellationToken);

        return Accepted(result.Value);
    }
}
