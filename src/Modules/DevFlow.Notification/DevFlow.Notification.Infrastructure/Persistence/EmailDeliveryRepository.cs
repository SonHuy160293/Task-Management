using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevFlow.EntityFrameworkCore.Reliability;
using DevFlow.Notification.Application.Abstractions;
using DevFlow.Notification.Contracts;
using DevFlow.Notification.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace DevFlow.Notification.Infrastructure.Persistence;
public sealed class EmailDeliveryRepository(NotificationDbContext db) : IEmailDeliveryRepository
{
    public async Task AcceptAsync(EmailDeliveryRequestedV1 message, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Pipeline transaction required.");
        }

        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({message.MessageId.ToString()}, 271001))", ct);
        string json = JsonSerializer.Serialize(message);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        var inbox = await db.Set<InboxMessage>().SingleOrDefaultAsync(x => x.ConsumerName == "notification.email.v1" && x.MessageId == message.MessageId, ct);
        var now = DateTimeOffset.UtcNow;
        if (inbox is null)
        {
            var job = EmailDelivery.Create(message.MessageId, hash, json, message.CreatedAt, message.ExpiresAt);
            if (!job.CanSend(now))
            {
                job.Expire();
            }

            db.EmailDeliveries.Add(job);
            var lease = Guid.CreateVersion7();
            inbox = InboxMessage.Create("notification.email.v1", message.MessageId, hash, lease, now, now.AddMinutes(1));
            inbox.Complete(lease, now);
            db.Set<InboxMessage>().Add(inbox);
        }
        else
        {
            inbox.EnsurePayloadMatches(hash);
        }

        // Reissue a receipt on duplicate consumption; the original receipt may have been lost.
        var receipt = new EmailDeliveryRecordedV1(Guid.CreateVersion7(), message.MessageId, message.CorrelationId, now);
        db.Set<OutboxMessage>().Add(OutboxMessage.Create(receipt.MessageId, EmailDeliveryRecordedV1.EventType, 1, message.MessageId, 1, JsonSerializer.Serialize(receipt), message.CorrelationId, message.MessageId, now));
    }

    public async Task RecordWebhookAsync(ProviderEmailEvent value, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({value.EventId}, 271002))", ct);
        var existing = await db.EmailWebhookReceipts.SingleOrDefaultAsync(x => x.EventId == value.EventId, ct);
        if (existing is not null)
        {
            if (existing.PayloadHash != value.PayloadHash)
            {
                throw new InvalidOperationException("Webhook payload mismatch.");
            }

            return;
        }

        db.EmailWebhookReceipts.Add(new EmailWebhookReceipt { EventId = value.EventId, PayloadHash = value.PayloadHash, ProviderId = value.ProviderId, EventType = value.Type, RecipientHash = value.RecipientHash, ReceivedAt = DateTimeOffset.UtcNow, });
    }
}
