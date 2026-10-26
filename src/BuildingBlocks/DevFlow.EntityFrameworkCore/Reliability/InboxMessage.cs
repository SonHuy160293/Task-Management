using DevFlow.SharedKernel.Exceptions;

namespace DevFlow.EntityFrameworkCore.Reliability;

public sealed class InboxMessage
{
    private InboxMessage() { }

    public string ConsumerName { get; private set; } = null!;
    public Guid MessageId { get; private set; }
    public string PayloadHash { get; private set; } = null!;
    public InboxStatus Status { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? FailureCode { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public Guid ConcurrencyToken { get; private set; } = Guid.CreateVersion7();

    public static InboxMessage Create(string consumerName, Guid messageId, string payloadHash,
        Guid leaseToken, DateTimeOffset receivedAt, DateTimeOffset leaseUntil)
    {
        ReliabilityGuard.Lease(leaseToken, receivedAt, leaseUntil);
        return new InboxMessage
        {
            ConsumerName = ReliabilityGuard.Code(consumerName, 100, nameof(consumerName)),
            MessageId = ReliabilityGuard.Id(messageId, nameof(messageId)),
            PayloadHash = ReliabilityGuard.Hash(payloadHash, nameof(payloadHash)),
            Status = InboxStatus.Processing,
            ReceivedAt = receivedAt.ToUniversalTime(),
            LeaseToken = leaseToken,
            LeaseUntil = leaseUntil.ToUniversalTime()
        };
    }

    public void EnsurePayloadMatches(string hash)
    {
        if (PayloadHash != ReliabilityGuard.Hash(hash, nameof(hash)))
        {
            throw new ConflictException("Inbox.PayloadMismatch", "The message identifier was reused with another payload.");
        }
    }

    public void Reclaim(string payloadHash, Guid token, DateTimeOffset now, DateTimeOffset until)
    {
        EnsurePayloadMatches(payloadHash);
        ReliabilityGuard.Lease(token, now, until);
        ReliabilityGuard.Require(Status == InboxStatus.Processing && LeaseUntil <= now, nameof(Status), "Message lease has not expired.");
        LeaseToken = token;
        LeaseUntil = until.ToUniversalTime();
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void Complete(Guid token, DateTimeOffset now)
    {
        ReliabilityGuard.OwnLease(token, LeaseToken, LeaseUntil, now);
        ReliabilityGuard.Require(Status == InboxStatus.Processing, nameof(Status), "Message must be processing.");
        Status = InboxStatus.Processed;
        ProcessedAt = now.ToUniversalTime();
        LeaseToken = null;
        LeaseUntil = null;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void DeadLetter(Guid token, DateTimeOffset now, string failureCode)
    {
        ReliabilityGuard.OwnLease(token, LeaseToken, LeaseUntil, now);
        string code = ReliabilityGuard.Code(failureCode, 64, nameof(failureCode));
        Status = InboxStatus.DeadLetter;
        FailureCode = code;
        LeaseToken = null;
        LeaseUntil = null;
        ConcurrencyToken = Guid.CreateVersion7();
    }
}
