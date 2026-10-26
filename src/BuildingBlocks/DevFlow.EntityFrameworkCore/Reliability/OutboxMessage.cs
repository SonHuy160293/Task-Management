namespace DevFlow.EntityFrameworkCore.Reliability;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public Guid MessageId { get; private set; }
    public string MessageType { get; private set; } = null!;
    public int SchemaVersion { get; private set; }
    public Guid AggregateId { get; private set; }
    public long AggregateRevision { get; private set; }
    public string PayloadJson { get; private set; } = null!;
    public Guid CorrelationId { get; private set; }
    public Guid? CausationId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public OutboxStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string? FailureCode { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public Guid ConcurrencyToken { get; private set; } = Guid.CreateVersion7();

    public static OutboxMessage Create(Guid messageId, string messageType, int schemaVersion,
        Guid aggregateId, long aggregateRevision, string payloadJson, Guid correlationId,
        Guid? causationId, DateTimeOffset occurredAt)
    {
        ReliabilityGuard.Require(schemaVersion > 0 && aggregateRevision > 0, nameof(schemaVersion), "Versions must be positive.");
        if (causationId.HasValue)
        {
            ReliabilityGuard.Id(causationId.Value, nameof(causationId));
        }

        return new OutboxMessage
        {
            MessageId = ReliabilityGuard.Id(messageId, nameof(messageId)),
            MessageType = ReliabilityGuard.Code(messageType, 200, nameof(messageType)),
            SchemaVersion = schemaVersion,
            AggregateId = ReliabilityGuard.Id(aggregateId, nameof(aggregateId)),
            AggregateRevision = aggregateRevision,
            PayloadJson = ReliabilityGuard.Json(payloadJson),
            CorrelationId = ReliabilityGuard.Id(correlationId, nameof(correlationId)),
            CausationId = causationId,
            OccurredAt = occurredAt.ToUniversalTime(),
            NextAttemptAt = occurredAt.ToUniversalTime(),
            Status = OutboxStatus.Pending
        };
    }

    public void Claim(Guid token, DateTimeOffset now, DateTimeOffset until)
    {
        ReliabilityGuard.Lease(token, now, until);
        ReliabilityGuard.Require((Status == OutboxStatus.Pending && NextAttemptAt <= now)
            || (Status == OutboxStatus.Processing && LeaseUntil <= now), nameof(Status), "Message is not claimable.");
        int attempts = checked(Attempts + 1);
        Status = OutboxStatus.Processing;
        LeaseToken = token;
        LeaseUntil = until.ToUniversalTime();
        Attempts = attempts;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void MarkPublished(Guid token, DateTimeOffset now)
    {
        ReliabilityGuard.OwnLease(token, LeaseToken, LeaseUntil, now);
        ReliabilityGuard.Require(Status == OutboxStatus.Processing, nameof(Status), "Message must be processing.");
        Status = OutboxStatus.Published;
        PublishedAt = now.ToUniversalTime();
        LeaseToken = null;
        LeaseUntil = null;
        FailureCode = null;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void RequeuePublished(DateTimeOffset now)
    {
        ReliabilityGuard.Require(Status == OutboxStatus.Published, nameof(Status), "Only published messages can be reconciled.");
        Status = OutboxStatus.Pending;
        PublishedAt = null;
        NextAttemptAt = now.ToUniversalTime();
        FailureCode = "ReceiptMissing";
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void ExpireAndRedact(bool received)
    {
        PayloadJson = "{}";
        if (received)
        {
            Status = OutboxStatus.Published;
            PublishedAt ??= DateTimeOffset.UtcNow;
        }
        else
        {
            Status = OutboxStatus.DeadLetter;
            PublishedAt = null;
            FailureCode = "Expired";
        }
        LeaseToken = null;
        LeaseUntil = null;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void Retry(Guid token, DateTimeOffset now, DateTimeOffset nextAttemptAt, string failureCode, bool deadLetter = false)
    {
        ReliabilityGuard.OwnLease(token, LeaseToken, LeaseUntil, now);
        ReliabilityGuard.Require(nextAttemptAt >= now, nameof(nextAttemptAt), "Retry time cannot precede the current time.");
        string code = ReliabilityGuard.Code(failureCode, 64, nameof(failureCode));
        Status = deadLetter ? OutboxStatus.DeadLetter : OutboxStatus.Pending;
        FailureCode = code;
        NextAttemptAt = nextAttemptAt.ToUniversalTime();
        LeaseToken = null;
        LeaseUntil = null;
        ConcurrencyToken = Guid.CreateVersion7();
    }
}
