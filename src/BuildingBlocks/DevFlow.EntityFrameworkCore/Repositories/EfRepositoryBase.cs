using System.Linq.Expressions;
using DevFlow.SharedKernel.Abstractions.Domain;
using Microsoft.EntityFrameworkCore;

namespace DevFlow.EntityFrameworkCore.Repositories;

public abstract class EfRepositoryBase<TEntity, TId, TDbContext>
    where TEntity : class, IEntity<TId>
    where TDbContext : DbContext
{
    protected EfRepositoryBase(TDbContext dbContext)
    {
        DbContext = dbContext;
    }

    protected TDbContext DbContext { get; }

    protected DbSet<TEntity> DbSet => DbContext.Set<TEntity>();

    protected IQueryable<TEntity> Query(Expression<Func<TEntity, bool>>? predicate = null, params Expression<Func<TEntity, object>>[] includeProperties)
        => BuildQuery(DbSet.AsNoTracking(), predicate, includeProperties);

    protected IQueryable<TEntity> QueryWithIdentityResolution(Expression<Func<TEntity, bool>>? predicate = null, params Expression<Func<TEntity, object>>[] includeProperties)
        => BuildQuery(DbSet.AsNoTrackingWithIdentityResolution(), predicate, includeProperties);

    protected IQueryable<TEntity> TrackedQuery(Expression<Func<TEntity, bool>>? predicate = null, params Expression<Func<TEntity, object>>[] includeProperties)
        => BuildQuery(DbSet.AsTracking(), predicate, includeProperties);

    protected Task<TEntity?> FindByIdAsync(TId id, CancellationToken cancellationToken)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var property = Expression.Property(parameter, nameof(IEntity<TId>.Id));
        var constant = Expression.Constant(id, typeof(TId));
        var predicate = Expression.Lambda<Func<TEntity, bool>>(
            Expression.Equal(property, constant),
            parameter);

        return DbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    protected async Task AddEntityAsync(TEntity entity, CancellationToken cancellationToken)
        => await DbSet.AddAsync(entity, cancellationToken);

    protected void AddEntity(TEntity entity)
        => DbSet.Add(entity);

    protected void UpdateEntity(TEntity entity)
        => DbSet.Update(entity);

    protected void RemoveEntity(TEntity entity)
        => DbSet.Remove(entity);

    private static IQueryable<TEntity> BuildQuery(IQueryable<TEntity> query, Expression<Func<TEntity, bool>>? predicate, IReadOnlyCollection<Expression<Func<TEntity, object>>> includeProperties)
    {
        foreach (var includeProperty in includeProperties)
        {
            query = query.Include(includeProperty);
        }

        return predicate is null ? query : query.Where(predicate);
    }
}
