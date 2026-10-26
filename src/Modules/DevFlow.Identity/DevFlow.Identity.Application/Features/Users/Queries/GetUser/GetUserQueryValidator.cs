using DevFlow.SharedKernel.Abstractions.Message;

namespace DevFlow.Identity.Application.Features.Users.Queries.GetUser;

public sealed class GetUserQueryValidator : IValidator<GetUserQuery>
{
    public Task ValidateAsync(GetUserQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            throw new CqrsValidationException(["UserId must not be empty."]);
        }

        return Task.CompletedTask;
    }
}
