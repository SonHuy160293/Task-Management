using System.Text.Json;
using DevFlow.EntityFrameworkCore.Reliability;
using DevFlow.Identity.Application.Abstractions.Messaging;
using DevFlow.Identity.Infrastructure.Persistence.DbContexts;
using DevFlow.Notification.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DevFlow.Identity.Infrastructure.Messaging;
public sealed class IdentityEmailOutbox(IdentityDbContext db) : IVerificationEmailOutbox
{
    public Task StageAsync(EmailDeliveryRequestedV1 request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        request.Validate();
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Pipeline transaction required.");
        }

        db.Set<OutboxMessage>().Add(OutboxMessage.Create(request.MessageId, EmailDeliveryRequestedV1.EventType, 1, request.MessageId, 1, JsonSerializer.Serialize(request), request.CorrelationId, request.CausationId, request.CreatedAt));
        return Task.CompletedTask;
    }
}

public sealed class EmailDeliveryReceipt
{
    public Guid DeliveryMessageId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public sealed class OutboxRecovery
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid MessageId { get; set; }
    public DateTimeOffset RequeuedAt { get; set; }
}

public sealed class EmailDeliveryReceiptStore(IdentityDbContext db) : IEmailDeliveryReceiptStore
{
    public async Task RecordAsync(EmailDeliveryRecordedV1 receipt, CancellationToken ct)
    {
        if (receipt.MessageId.Version != 7 || receipt.DeliveryMessageId.Version != 7)
        {
            throw new InvalidOperationException("Invalid delivery receipt.");
        }

        // Serialize receipt/reconciliation races with the outbox row.
        var row = await db.Set<OutboxMessage>().FromSqlInterpolated($"""
            SELECT * FROM reliability."OutboxMessages" WHERE "MessageId" = {receipt.DeliveryMessageId} FOR UPDATE
            """).SingleOrDefaultAsync(ct);
        if (row is null || row.MessageType != EmailDeliveryRequestedV1.EventType)
        {
            throw new InvalidOperationException("Unknown delivery receipt.");
        }

        if (!await db.Set<EmailDeliveryReceipt>().AnyAsync(x => x.DeliveryMessageId == receipt.DeliveryMessageId, ct))
        {
            db.Set<EmailDeliveryReceipt>().Add(new() { DeliveryMessageId = receipt.DeliveryMessageId, RecordedAt = DateTimeOffset.UtcNow });
        }
    }
}
