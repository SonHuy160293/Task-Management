using DevFlow.Messaging.Configuration;
using DevFlow.Messaging.MassTransit;
using DevFlow.Messaging.Outbox;
using DevFlow.Notification.Application;
using DevFlow.Notification.Contracts;
using DevFlow.Notification.Infrastructure;
using DevFlow.Notification.Infrastructure.Messaging;
using DevFlow.Notification.Infrastructure.Operations;
using DevFlow.Notification.Infrastructure.Persistence;
using DevFlow.Observability;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

try
{
    if (args.Length >= 3 && args[0] == "init-keys")
    {
        KeyInitialization.Create(args[1], args[2]);
        return;
    }

    var builder = Host.CreateApplicationBuilder(args.Length > 0 && (args[0] == "ops" || args[0] == "topology") ? [] : args);
    builder.AddDevFlowObservability();
    builder.Logging.AddFilter("MassTransit", LogLevel.None);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
    if (args.Length > 0 && args[0] == "topology")
    {
        await NotificationTopology.DeployAsync(builder.Configuration, CancellationToken.None);
        return;
    }

    builder.Services.AddNotificationApplication().AddNotificationPersistence(builder.Configuration).AddNotificationEmail(builder.Configuration);
    if (args.Length > 1 && args[0] == "ops" && args[1] == "replay-errors")
    {
        // Host/container administration is the authentication boundary. Preserve event IDs.
        if (args.Length != 5 || args[2].Length is < 1 or > 160 || args[3].Length is < 1 or > 300)
        {
            throw new ArgumentException("Usage: ops replay-errors <operator> <reason> <limit>.");
        }

        builder.Services.AddNotificationMessaging(builder.Configuration, runWorkers: false);
        using var replayHost = builder.Build();
        await replayHost.StartAsync();
        using var scope = replayHost.Services.CreateScope();
        var broker = builder.Configuration.GetSection("Messaging").Get<MessagingOptions>()!;
        int count = await BrokerErrorReplay.ReplayAsync<EmailDeliveryRequestedV1>(broker, replayHost.Services.GetRequiredService<IBus>(), "devflow.notification.email-requested.v1_error", int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);
        var audit = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        audit.Set<OutboxOperation>().Add(new() { Action = "replay-errors", Operator = args[2], Reason = args[3], OccurredAt = DateTimeOffset.UtcNow });
        await audit.SaveChangesAsync();
        await replayHost.StopAsync();
        Console.WriteLine($"Replayed {count} encrypted messages.");
        return;
    }

    if (args.Length > 0 && args[0] == "ops")
    {
        using var controlHost = builder.Build();
        using var scope = controlHost.Services.CreateScope();
        await NotificationOperations.ExecuteAsync(scope.ServiceProvider, args.Skip(1).ToArray(), CancellationToken.None);
        return;
    }

    builder.Services.AddNotificationMessaging(builder.Configuration);
    using var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Notification worker stopped ({ex.GetType().Name}).");
    Environment.ExitCode = 1;
}
