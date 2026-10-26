using DevFlow.EntityFrameworkCore.Repositories;
using DevFlow.Identity.Infrastructure.Persistence.DbContexts;
using DevFlow.SharedKernel.Abstractions.Domain;
using DevFlow.SharedKernel.Abstractions.Persistence;
using DevFlow.SharedKernel.Exceptions;

namespace DevFlow.Identity.Infrastructure.Persistence.Core;

public abstract class RepositoryBase<TEntity, TId> : EfRepositoryBase<TEntity, TId, IdentityDbContext>, IRepositoryBase<TEntity, TId>, IDeletableRepository<TEntity, TId>
    where TEntity : class, IEntity<TId>
    where TId : IEquatable<TId>
{
    protected RepositoryBase(IdentityDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<TEntity> GetByIdAsync(TId id, CancellationToken cancellationToken)
        => await FindByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"{typeof(TEntity).Name}.NotFound", $"{typeof(TEntity).Name} '{id}' was not found.");

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        await AddEntityAsync(entity, cancellationToken);
    }

    public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        UpdateEntity(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteByIdAsync(TId id, CancellationToken cancellationToken)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        RemoveEntity(entity);
    }
}
