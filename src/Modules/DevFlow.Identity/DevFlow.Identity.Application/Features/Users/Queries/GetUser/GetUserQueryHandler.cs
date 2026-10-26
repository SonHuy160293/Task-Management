using DevFlow.Identity.Application.Abstractions.Repositories;
using DevFlow.Identity.Contracts;
using DevFlow.Identity.Domain.Models;
using DevFlow.SharedKernel.Abstractions.Message;
using DevFlow.SharedKernel.Abstractions.Shared;

namespace DevFlow.Identity.Application.Features.Users.Queries.GetUser;

public sealed class GetUserQueryHandler(IUserRepository users) : IQueryHandler<GetUserQuery, UserIntegrationDto>
{
    public async Task<Result<UserIntegrationDto>> HandleAsync(
        GetUserQuery request,
        CancellationToken cancellationToken)
    {
        User user = await users.GetByIdAsync(request.UserId, cancellationToken);
        return Result.Success(new UserIntegrationDto(user.Id, user.DisplayName, user.AvatarUrl, user.Status));
    }
}
