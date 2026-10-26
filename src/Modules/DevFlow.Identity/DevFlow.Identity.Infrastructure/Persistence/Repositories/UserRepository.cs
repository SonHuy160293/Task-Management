using DevFlow.Identity.Application.Abstractions.Persistence;
using DevFlow.Identity.Application.Abstractions.Repositories;
using DevFlow.Identity.Domain.Models;
using DevFlow.Identity.Infrastructure.Persistence.Core;
using DevFlow.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace DevFlow.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : RepositoryBase<User, Guid>, IUserRepository
{
    private readonly IdentityDbContext _dbContext;

    public UserRepository(IdentityDbContext dbContext)
      : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ExistsForRegistrationAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        if (_dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Registration requires the pipeline-owned transaction.");
        }

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended(lower({normalizedEmail}), 1920001))",
            cancellationToken);

        return await _dbContext.Users.FromSqlInterpolated(
                $"SELECT * FROM shared.\"Users\" WHERE lower(\"PrimaryEmail\") = lower({normalizedEmail})")
            .AsNoTracking().AnyAsync(cancellationToken);
    }
}
