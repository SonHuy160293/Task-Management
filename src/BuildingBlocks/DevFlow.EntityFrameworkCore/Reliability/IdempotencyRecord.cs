using DevFlow.SharedKernel.Exceptions;

namespace DevFlow.EntityFrameworkCore.Reliability;

public sealed class IdempotencyRecord
{
    private IdempotencyRecord() { }

    public Guid Id { get; private set; }
    public string Operation { get; private set; } = null!;
    public string OwnerScopeHash { get; private set; } = null!;
    public string KeyHash { get; private set; } = null!;
    public string RequestFingerprint { get; private set; } = null!;
    public IdempotencyStatus Status { get; private set; }
    public Guid? ResultResourceId { get; private set; }
    public string? ResultCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public Guid ConcurrencyToken { get; private set; } = Guid.CreateVersion7();

    public static IdempotencyRecord Create(string operation, string ownerScopeHash, string keyHash,
        string fingerprint, Guid leaseToken, DateTimeOffset now, DateTimeOffset leaseUntil)
    {
        ReliabilityGuard.Lease(leaseToken, now, leaseUntil);
        return new IdempotencyRecord
        {
            Id = Guid.CreateVersion7(),
            Operation = ReliabilityGuard.Code(operation, 100, nameof(operation)),
            OwnerScopeHash = ReliabilityGuard.Hash(ownerScopeHash, nameof(ownerScopeHash)),
            KeyHash = ReliabilityGuard.Hash(keyHash, nameof(keyHash)),
            RequestFingerprint = ReliabilityGuard.Hash(fingerprint, nameof(fingerprint)),
            Status = IdempotencyStatus.InProgress,
            CreatedAt = now.ToUniversalTime(),
            LeaseToken = leaseToken,
            LeaseUntil = leaseUntil.ToUniversalTime()
        };
    }

    public void EnsureRequestMatches(string fingerprint)
    {
        if (RequestFingerprint != ReliabilityGuard.Hash(fingerprint, nameof(fingerprint)))
        {
            throw new ConflictException("Idempotency.KeyReused", "The key was reused for a different request.");
        }
    }

    public void Reclaim(string fingerprint, Guid token, DateTimeOffset now, DateTimeOffset until)
    {
        EnsureRequestMatches(fingerprint);
        ReliabilityGuard.Lease(token, now, until);
        ReliabilityGuard.Require(Status == IdempotencyStatus.InProgress && LeaseUntil <= now, nameof(Status), "Request lease has not expired.");
        LeaseToken = token;
        LeaseUntil = until.ToUniversalTime();
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void Complete(Guid token, Guid resourceId, string resultCode, DateTimeOffset now)
    {
        ReliabilityGuard.OwnLease(token, LeaseToken, LeaseUntil, now);
        ReliabilityGuard.Require(Status == IdempotencyStatus.InProgress, nameof(Status), "Request must be in progress.");
        Guid result = ReliabilityGuard.Id(resourceId, nameof(resourceId));
        string code = ReliabilityGuard.Code(resultCode, 64, nameof(resultCode));
        ResultResourceId = result;
        ResultCode = code;
        Status = IdempotencyStatus.Completed;
        CompletedAt = now.ToUniversalTime();
        LeaseToken = null;
        LeaseUntil = null;
        ConcurrencyToken = Guid.CreateVersion7();
    }
}
