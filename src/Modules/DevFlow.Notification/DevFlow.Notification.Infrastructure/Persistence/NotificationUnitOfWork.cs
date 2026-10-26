using DevFlow.SharedKernel.Abstractions.Persistence;
using DevFlow.SharedKernel.Abstractions.Shared;

namespace DevFlow.Notification.Infrastructure.Persistence;
public sealed class NotificationUnitOfWork(NotificationDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action(ct);
            if (result is Result { IsFailure: true })
            {
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return result;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            try
            {
                await tx.RollbackAsync(CancellationToken.None);
            }
            catch (Exception)
            {
            }

            db.ChangeTracker.Clear();
            throw;
        }
    }
}
