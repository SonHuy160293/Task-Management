using DevFlow.EntityFrameworkCore.Reliability;
using Microsoft.EntityFrameworkCore;

namespace DevFlow.Messaging.Outbox;
public class EfOutboxDispatchStore<TContext>(TContext db) : IOutboxDispatchStore where TContext : DbContext
{
    public virtual async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(CancellationToken ct)
    {
        if (await db.Set<OutboxDispatchControl>().AsNoTracking().AnyAsync(x => x.Paused, ct))
        {
            return[];
        }

        var pending = db.Set<OutboxMessage>().Where(x => x.Status == OutboxStatus.Pending || x.Status == OutboxStatus.Processing);
        var oldest = await pending.MinAsync(x => (DateTimeOffset? )x.OccurredAt, ct);
        MessagingMetrics.Scan(await pending.LongCountAsync(ct), await db.Set<OutboxMessage>().LongCountAsync(x => x.Status == OutboxStatus.DeadLetter, ct), oldest is null ? 0 : Math.Max(0, (DateTimeOffset.UtcNow - oldest.Value).TotalSeconds));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var rows = await db.Set<OutboxMessage>().FromSqlInterpolated($"""
            SELECT * FROM reliability."OutboxMessages"
            WHERE ("Status" = 0 AND "NextAttemptAt" <= {now})
               OR ("Status" = 1 AND "LeaseUntil" <= {now})
            ORDER BY "OccurredAt" LIMIT 10 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        foreach (var row in rows)
        {
            row.Claim(Guid.CreateVersion7(), now, now.AddSeconds(60));
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return rows;
    }

    public async Task PublishedAsync(Guid id, Guid lease, CancellationToken ct)
    {
        await UpdateAsync(id, lease, row => row.MarkPublished(lease, DateTimeOffset.UtcNow), ct);
    }

    public async Task FailedAsync(Guid id, Guid lease, string code, bool permanent, CancellationToken ct)
    {
        await UpdateAsync(id, lease, row => row.Retry(lease, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.Add(RetrySchedule.Delay(row.Attempts)), code, permanent), ct);
    }

    private async Task UpdateAsync(Guid id, Guid lease, Action<OutboxMessage> update, CancellationToken ct)
    {
        var row = await db.Set<OutboxMessage>().SingleAsync(x => x.MessageId == id, ct);
        if (row.LeaseToken != lease || row.LeaseUntil <= DateTimeOffset.UtcNow)
        {
            db.ChangeTracker.Clear();
            return;
        }

        update(row);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        { /* A recovered worker owns this record now. */
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }
}
