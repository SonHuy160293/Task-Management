using DevFlow.EntityFrameworkCore.Reliability;
using DevFlow.Identity.Infrastructure.Messaging;
using DevFlow.Identity.Infrastructure.Persistence.DbContexts;
using DevFlow.Messaging.Outbox;
using DevFlow.Notification.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DevFlow.Identity.Infrastructure.Persistence.Outbox;
public sealed class IdentityOutboxDispatchStore(IdentityDbContext db) : EfOutboxDispatchStore<IdentityDbContext>(db)
{
    public override async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(CancellationToken ct)
    {
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var due = DateTimeOffset.UtcNow.AddMinutes(-5);
            var rows = await db.Set<OutboxMessage>().FromSqlInterpolated($"""
                SELECT o.* FROM reliability."OutboxMessages" o
                WHERE o."Status" = 2 AND o."PublishedAt" < {due} AND o."MessageType" = {EmailDeliveryRequestedV1.EventType}
                AND NOT EXISTS (SELECT 1 FROM reliability."EmailDeliveryReceipts" r WHERE r."DeliveryMessageId" = o."MessageId")
                ORDER BY o."PublishedAt" LIMIT 10 FOR UPDATE SKIP LOCKED
                """).ToListAsync(ct);
            foreach (var row in rows)
            {
                row.RequeuePublished(DateTimeOffset.UtcNow);
                db.Set<OutboxRecovery>().Add(new() { MessageId = row.MessageId, RequeuedAt = DateTimeOffset.UtcNow });
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        db.ChangeTracker.Clear();
        MessagingMetrics.MissingReceipts(await db.Set<OutboxMessage>().LongCountAsync(x => x.Status == OutboxStatus.Published && x.MessageType == EmailDeliveryRequestedV1.EventType && !db.Set<EmailDeliveryReceipt>().Any(r => r.DeliveryMessageId == x.MessageId), ct));
        return await base.ClaimAsync(ct);
    }
}
