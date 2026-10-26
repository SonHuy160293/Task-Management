using DevFlow.Identity.Application.Abstractions.Persistence;
using DevFlow.Identity.Domain.Models;

namespace DevFlow.Identity.Application.Abstractions.Repositories;

public interface IOneTimeTokenRepository : IRepositoryBase<OneTimeToken>
{
}
