namespace DevFlow.Identity.Application.Abstractions.Persistence;

public interface IRepositoryBase<TEntity>
    where TEntity : class
{
    Task<TEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken);

    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken);
}
