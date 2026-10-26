using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevFlow.Messaging.Outbox;
public sealed class OutboxRelay(IServiceScopeFactory scopes, ILogger<OutboxRelay> logger, WorkerHeartbeat? heartbeat = null) : BackgroundService
{
    private static readonly Meter Meter = new("DevFlow.Messaging");
    private static readonly Counter<long> Published = Meter.CreateCounter<long>("devflow.outbox.published");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>("devflow.outbox.failed");
    private readonly WorkerHeartbeat _heartbeat = heartbeat ?? new WorkerHeartbeat();
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("Outbox scan failed ({ExceptionType})", ex.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxDispatchStore>();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        foreach (var message in await store.ClaimAsync(ct))
        {
            if (message.LeaseUntil <= DateTimeOffset.UtcNow.AddSeconds(10))
            {
                continue;
            }

            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                await publisher.PublishAsync(message, deadline.Token);
                await store.PublishedAsync(message.MessageId, message.LeaseToken!.Value, ct);
                Published.Add(1);
            }
            catch (OperationCanceledException)when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                bool permanent = ex is InvalidIntegrationEventException;
                await store.FailedAsync(message.MessageId, message.LeaseToken!.Value, permanent ? "InvalidEvent" : "PublicationUnavailable", permanent, ct);
                Failed.Add(1);
                logger.LogWarning("Outbox publication deferred: {MessageId} {FailureCode}", message.MessageId, permanent ? "InvalidEvent" : "PublicationUnavailable");
            }
        }

        await _heartbeat.CompleteScanAsync(ct);
    }
}
