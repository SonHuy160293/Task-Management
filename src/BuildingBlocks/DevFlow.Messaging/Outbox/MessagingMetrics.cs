using System.Diagnostics.Metrics;

namespace DevFlow.Messaging.Outbox;
public static class MessagingMetrics
{
    private static readonly Meter Meter = new("DevFlow.Messaging");
    private static long _pending, _dead, _lastScan, _missingReceipts;
    private static double _oldestAge;
    static MessagingMetrics()
    {
        Meter.CreateObservableGauge("devflow.outbox.pending", () => Interlocked.Read(ref _pending));
        Meter.CreateObservableGauge("devflow.outbox.dead_letters", () => Interlocked.Read(ref _dead));
        Meter.CreateObservableGauge("devflow.outbox.oldest_pending_age.seconds", () => Volatile.Read(ref _oldestAge));
        Meter.CreateObservableGauge("devflow.outbox.receipts_missing", () => Interlocked.Read(ref _missingReceipts));
        Meter.CreateObservableGauge("devflow.outbox.worker_heartbeat_age.seconds", () => Interlocked.Read(ref _lastScan) == 0 ? -1 : DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Interlocked.Read(ref _lastScan));
    }

    public static void Scan(long pending, long dead, double age)
    {
        Interlocked.Exchange(ref _pending, pending);
        Interlocked.Exchange(ref _dead, dead);
        Volatile.Write(ref _oldestAge, age);
        Interlocked.Exchange(ref _lastScan, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public static void MissingReceipts(long count) => Interlocked.Exchange(ref _missingReceipts, count);
}
