using DevFlow.EntityFrameworkCore.Reliability;
using DevFlow.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevFlow.Notification.Infrastructure.Email.Workers;
public sealed class DeliveryMaintenanceWorker(IServiceScopeFactory scopes, ILogger<DeliveryMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                // Clear expired secrets even when delivery is globally paused/disabled.
                var now = DateTimeOffset.UtcNow;
                var expired = await db.EmailDeliveries.Where(x => x.ExpiresAt <= now && x.RequestJson != "{}").OrderBy(x => x.ExpiresAt).ThenBy(x => x.Id).Take(100).ToListAsync(ct);
                foreach (var job in expired)
                {
                    job.Expire();
                }

                await db.SaveChangesAsync(ct);
                var cutoff = now.AddDays(-30);
                await db.EmailWebhookReceipts.Where(x => x.ProcessedAt != null && x.ReceivedAt < cutoff).ExecuteDeleteAsync(ct);
                await db.Set<OutboxMessage>().Where(x => x.Status == OutboxStatus.Published && x.PublishedAt < cutoff).ExecuteDeleteAsync(ct);
                var terminal = db.EmailDeliveries.Where(x => x.CreatedAt < cutoff && x.Status != "Pending" && x.Status != "Processing").Select(x => x.Id);
                await db.EmailDeliveryAttempts.Where(x => terminal.Contains(x.DeliveryId)).ExecuteDeleteAsync(ct);
                await db.EmailDeliveries.Where(x => terminal.Contains(x.Id)).ExecuteDeleteAsync(ct);
                logger.LogInformation("Notification maintenance complete");
            }
            catch (OperationCanceledException)when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("Notification maintenance failed ({ExceptionType})", ex.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}
