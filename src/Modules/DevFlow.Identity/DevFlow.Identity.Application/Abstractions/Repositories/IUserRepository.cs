using DevFlow.Identity.Application.Abstractions.Persistence;
using DevFlow.Identity.Domain.Models;

namespace DevFlow.Identity.Application.Abstractions.Repositories;

public interface IUserRepository : IRepositoryBase<User>
{
    Task<bool> ExistsForRegistrationAsync(string normalizedEmail, CancellationToken cancellationToken);
}
