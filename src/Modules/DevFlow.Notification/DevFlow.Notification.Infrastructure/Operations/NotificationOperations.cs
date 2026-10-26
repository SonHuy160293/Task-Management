using DevFlow.Notification.Domain.Models;
using DevFlow.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.Notification.Infrastructure.Operations;
// Invoked only through authenticated host/container administration, never a public endpoint.
public static class NotificationOperations
{
    public static async Task ExecuteAsync(IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("Expected inspect, pause, resume, or redrive.");
        }

        var db = services.GetRequiredService<NotificationDbContext>();
        if (args[0] is "pause-publication" or "resume-publication")
        {
            await DevFlow.Messaging.Outbox.OutboxAdministration.ExecuteAsync(db, args[0], args[1], args[2], ct);
            return;
        }

        if (args[0] == "inspect")
        {
            var rows = await db.EmailDeliveries.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(50).Select(x => new { x.Id, x.Status, x.Outcome, x.Attempts, x.FailureCode, x.NextAttemptAt }).ToListAsync(ct);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(rows));
            return;
        }

        if (args[0] is not ("pause" or "resume" or "redrive" or "reconcile-accepted") || args.Length < 3)
        {
            throw new ArgumentException("Usage: pause|resume <operator> <reason>; redrive <operator> <reason> <message-id>; reconcile-accepted <operator> <reason> <message-id> <provider-id>.");
        }

        if (args[1].Length is < 1 or > 160 || args[2].Length is < 1 or > 300)
        {
            throw new ArgumentException("Bounded operator and reason required.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(271003)", ct);
        var control = await db.EmailDeliveryControls.SingleAsync(ct);
        Guid? id = null;
        if (args[0] == "pause")
        {
            control.PausedUntil = DateTimeOffset.MaxValue;
            control.PauseReason = "OperatorPaused";
        }

        if (args[0] == "resume")
        {
            control.PausedUntil = null;
            control.PauseReason = null;
            control.ConsecutiveFailures = 0;
        }

        if (args[0] == "redrive")
        {
            if (args.Length != 4)
            {
                throw new ArgumentException("Message ID required.");
            }
            id = Guid.Parse(args[3]);
            var job = await db.EmailDeliveries.SingleAsync(x => x.Id == id, ct);
            job.Redrive(DateTimeOffset.UtcNow);
        }

        if (args[0] == "reconcile-accepted")
        {
            if (args.Length != 5)
            {
                throw new ArgumentException("Message ID and provider reference required.");
            }
            id = Guid.Parse(args[3]);
            var job = await db.EmailDeliveries.SingleAsync(x => x.Id == id, ct);
            job.ReconcileAccepted(DateTimeOffset.UtcNow, args[4]);
        }

        db.EmailOperations.Add(new EmailOperation { DeliveryId = id, Operator = args[1], Reason = args[2], Action = args[0], OccurredAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        Console.WriteLine("Operation recorded.");
    }
}
