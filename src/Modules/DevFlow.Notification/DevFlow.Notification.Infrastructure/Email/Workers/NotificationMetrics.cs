using System.Diagnostics.Metrics;
using DevFlow.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevFlow.Notification.Infrastructure.Email.Workers;
internal static class NotificationMetrics
{
    private static readonly Meter Meter = new("DevFlow.Notification");
    private static long _pending, _dead, _lastScan, _accepted, _delivered, _bounced, _suppressed, _expired;
    private static double _oldestAge;
    static NotificationMetrics()
    {
        Meter.CreateObservableGauge("devflow.notification.pending", () => Interlocked.Read(ref _pending));
        Meter.CreateObservableGauge("devflow.notification.dead_letters", () => Interlocked.Read(ref _dead));
        Meter.CreateObservableGauge("devflow.notification.provider_accepted", () => Interlocked.Read(ref _accepted));
        Meter.CreateObservableGauge("devflow.notification.delivered", () => Interlocked.Read(ref _delivered));
        Meter.CreateObservableGauge("devflow.notification.bounced", () => Interlocked.Read(ref _bounced));
        Meter.CreateObservableGauge("devflow.notification.suppressed", () => Interlocked.Read(ref _suppressed));
        Meter.CreateObservableGauge("devflow.notification.expired", () => Interlocked.Read(ref _expired));
        Meter.CreateObservableGauge("devflow.notification.oldest_pending_age.seconds", () => Volatile.Read(ref _oldestAge));
        Meter.CreateObservableGauge("devflow.notification.worker_heartbeat_age.seconds", () => Interlocked.Read(ref _lastScan) == 0 ? -1 : DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Interlocked.Read(ref _lastScan));
    }

    public static async Task ScanAsync(NotificationDbContext db, CancellationToken ct)
    {
        var groups = await db.EmailDeliveries.GroupBy(x => new { x.Status, x.Outcome }).Select(x => new { x.Key.Status, x.Key.Outcome, Count = x.LongCount() }).ToListAsync(ct);
        Interlocked.Exchange(ref _pending, groups.Where(x => x.Status is "Pending" or "Processing").Sum(x => x.Count));
        Interlocked.Exchange(ref _dead, groups.Where(x => x.Status == "DeadLetter").Sum(x => x.Count));
        Interlocked.Exchange(ref _accepted, groups.Where(x => x.Status == "ProviderAccepted").Sum(x => x.Count));
        Interlocked.Exchange(ref _delivered, groups.Where(x => x.Outcome == "Delivered").Sum(x => x.Count));
        Interlocked.Exchange(ref _bounced, groups.Where(x => x.Outcome is "Bounced" or "Complained").Sum(x => x.Count));
        Interlocked.Exchange(ref _suppressed, groups.Where(x => x.Status == "Suppressed").Sum(x => x.Count));
        Interlocked.Exchange(ref _expired, groups.Where(x => x.Status == "Expired").Sum(x => x.Count));
        var oldest = await db.EmailDeliveries.Where(x => x.Status == "Pending" || x.Status == "Processing").MinAsync(x => (DateTimeOffset? )x.CreatedAt, ct);
        Volatile.Write(ref _oldestAge, oldest is null ? 0 : Math.Max(0, (DateTimeOffset.UtcNow - oldest.Value).TotalSeconds));
        Interlocked.Exchange(ref _lastScan, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }
}
