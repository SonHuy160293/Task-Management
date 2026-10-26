using System.Text.Json;
using DevFlow.EntityFrameworkCore.Reliability;
using DevFlow.Identity.Infrastructure.Persistence.DbContexts;
using DevFlow.Notification.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevFlow.Identity.Infrastructure.Messaging;
public sealed class IdentityOutboxMaintenanceWorker(IServiceScopeFactory scopes, ILogger<IdentityOutboxMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                var now = DateTimeOffset.UtcNow;
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var rows = await db.Set<OutboxMessage>().FromSqlInterpolated($"""
                    SELECT * FROM reliability."OutboxMessages" WHERE "MessageType" = {EmailDeliveryRequestedV1.EventType}
                    AND "OccurredAt" <= {now.AddDays(-1)} AND "PayloadJson" <> {"{}"}
                    AND ("LeaseUntil" IS NULL OR "LeaseUntil" <= {now})
                    ORDER BY "OccurredAt" LIMIT 100 FOR UPDATE SKIP LOCKED
                    """).ToListAsync(ct);
                foreach (var row in rows)
                {
                    var request = JsonSerializer.Deserialize<EmailDeliveryRequestedV1>(row.PayloadJson);
                    if (request?.ExpiresAt <= now)
                    {
                        row.ExpireAndRedact(await db.Set<EmailDeliveryReceipt>().AnyAsync(x => x.DeliveryMessageId == row.MessageId, ct));
                    }
                }

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                var cutoff = now.AddDays(-30);
                await db.Set<OutboxMessage>().Where(x => x.Status == OutboxStatus.Published && x.OccurredAt < cutoff && db.Set<EmailDeliveryReceipt>().Any(r => r.DeliveryMessageId == x.MessageId)).ExecuteDeleteAsync(ct);
            }
            catch (OperationCanceledException)when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("Identity outbox maintenance failed ({ExceptionType})", ex.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}
