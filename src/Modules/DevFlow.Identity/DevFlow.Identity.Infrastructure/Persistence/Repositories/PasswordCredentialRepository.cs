using DevFlow.Identity.Application.Abstractions.Repositories;
using DevFlow.Identity.Domain.Models;
using DevFlow.Identity.Infrastructure.Persistence.Core;
using DevFlow.Identity.Infrastructure.Persistence.DbContexts;

namespace DevFlow.Identity.Infrastructure.Persistence.Repositories;

public sealed class PasswordCredentialRepository : RepositoryBase<PasswordCredential, Guid>, IPasswordCredentialRepository
{
    public PasswordCredentialRepository(IdentityDbContext dbContext) : base(dbContext)
    {
    }
}
