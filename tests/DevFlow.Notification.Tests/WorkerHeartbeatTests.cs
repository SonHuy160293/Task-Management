using DevFlow.EntityFrameworkCore.Reliability;
using DevFlow.Messaging.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevFlow.Notification.Tests;

public sealed class WorkerHeartbeatTests
{
    [Fact]
    public async Task SuccessfulEmptyScanRefreshesHeartbeatButFailedStorageLeavesItUnchanged()
    {
        string path = Path.Combine(Path.GetTempPath(), "devflow-heartbeat-" + Guid.NewGuid().ToString("N"));
        var store = new ScanStore();
        var registrations = new ServiceCollection();
        registrations.AddSingleton<IOutboxDispatchStore>(store);
        registrations.AddSingleton<IIntegrationEventPublisher, UnusedPublisher>();
        using var services = registrations.BuildServiceProvider();
        using var relay = new OutboxRelay(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxRelay>.Instance,
            new WorkerHeartbeat(path));
        try
        {
            await relay.RunOnceAsync(CancellationToken.None);
            string initialValue = await File.ReadAllTextAsync(path);
            Assert.True(long.TryParse(initialValue, out long timestamp));
            Assert.InRange(timestamp, DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Assert.InRange(new FileInfo(path).Length, 1, 32);

            DateTime initialWrite = File.GetLastWriteTimeUtc(path);
            store.Fail = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => relay.RunOnceAsync(CancellationToken.None));
            Assert.Equal(initialValue, await File.ReadAllTextAsync(path));
            Assert.Equal(initialWrite, File.GetLastWriteTimeUtc(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class ScanStore : IOutboxDispatchStore
    {
        public bool Fail { get; set; }

        public Task<IReadOnlyList<OutboxMessage>> ClaimAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Fail
                ? throw new InvalidOperationException("Storage unavailable.")
                : Task.FromResult<IReadOnlyList<OutboxMessage>>([]);
        }

        public Task PublishedAsync(Guid id, Guid lease, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task FailedAsync(Guid id, Guid lease, string code, bool permanent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class UnusedPublisher : IIntegrationEventPublisher
    {
        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
