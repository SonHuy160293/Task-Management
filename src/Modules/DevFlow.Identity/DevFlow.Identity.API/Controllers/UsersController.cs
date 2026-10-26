using DevFlow.AspNetCore.Controllers;
using DevFlow.Identity.Application.Features.Users.Queries.GetUser;
using DevFlow.Identity.Contracts;
using DevFlow.SharedKernel.Abstractions.Shared;
using DevFlow.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DevFlow.Identity.API.Controllers;

public sealed class UsersController(ISender sender) : IntegrationApiController(sender)
{
    [HttpGet("{userId:guid}")]
    [ProducesResponseType<UserIntegrationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserIntegrationDto>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        Result<UserIntegrationDto> result = await Sender.SendAsync(new GetUserQuery(userId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
