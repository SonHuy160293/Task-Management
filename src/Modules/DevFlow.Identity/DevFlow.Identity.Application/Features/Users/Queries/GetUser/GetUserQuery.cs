using DevFlow.Identity.Contracts;
using DevFlow.SharedKernel.Abstractions.Message;

namespace DevFlow.Identity.Application.Features.Users.Queries.GetUser;

public sealed record GetUserQuery(Guid UserId) : IQuery<UserIntegrationDto>;
