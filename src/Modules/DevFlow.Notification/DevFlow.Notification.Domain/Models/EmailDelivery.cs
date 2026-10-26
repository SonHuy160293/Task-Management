namespace DevFlow.Notification.Domain.Models;
public sealed class EmailDelivery
{
    private EmailDelivery()
    {
    }

    public Guid Id { get; private set; }
    public string RequestHash { get; private set; } = "";
    public string RequestJson { get; private set; } = "";
    public string? FrozenRequestJson { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string Status { get; private set; } = "Pending";
    public string Outcome { get; private set; } = "Unknown";
    public int Attempts { get; private set; }
    public bool BudgetReserved { get; private set; }
    public DateTimeOffset? FirstAttemptAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public string? ProviderId { get; private set; }
    public string? FailureCode { get; private set; }
    public Guid Version { get; private set; } = Guid.CreateVersion7();

    public static EmailDelivery Create(Guid id, string hash, string encryptedRequest, DateTimeOffset created, DateTimeOffset expires) => new()
    {
        Id = id,
        RequestHash = hash,
        RequestJson = encryptedRequest,
        CreatedAt = created,
        ExpiresAt = expires,
        NextAttemptAt = created
    };
    public bool CanSend(DateTimeOffset now) => now < ExpiresAt.AddMinutes(-5) && (FirstAttemptAt is null || now < FirstAttemptAt.Value.AddHours(23)) && Attempts < 24;
    public void Claim(DateTimeOffset now)
    {
        if (!(Status == "Pending" && NextAttemptAt <= now || Status == "Processing" && LeaseUntil <= now))
        {
            throw new InvalidOperationException("Email job is not claimable.");
        }

        Status = "Processing";
        LeaseToken = Guid.CreateVersion7();
        LeaseUntil = now.AddSeconds(60);
        Touch();
    }

    public void ReserveBudget()
    {
        BudgetReserved = true;
        Touch();
    }

    public void Freeze(string ciphertext)
    {
        FrozenRequestJson ??= ciphertext;
        Touch();
    }

    public void BeginAttempt(DateTimeOffset now)
    {
        FirstAttemptAt ??= now;
        Attempts++;
        Touch();
    }

    public void Accept(string providerId)
    {
        Status = "ProviderAccepted";
        ProviderId = providerId;
        FailureCode = null;
        RequestJson = "{}";
        FrozenRequestJson = null;
        Release();
    }

    public void Retry(DateTimeOffset at, string code)
    {
        Status = "Pending";
        NextAttemptAt = at;
        FailureCode = code;
        Release();
    }

    public void DeadLetter(string code)
    {
        Status = "DeadLetter";
        FailureCode = code;
        Release();
    }

    public void Expire()
    {
        Status = FirstAttemptAt is null ? "Expired" : "DeadLetter";
        FailureCode = FirstAttemptAt is null ? "Expired" : "AcceptanceUnknown";
        RequestJson = "{}";
        FrozenRequestJson = null;
        Release();
    }

    public void Suppress()
    {
        Status = "Suppressed";
        RequestJson = "{}";
        FrozenRequestJson = null;
        Release();
    }

    public void RecordOutcome(string outcome)
    {
        if (Outcome == "Complained" || Outcome == "Bounced" && outcome != "Complained" || Outcome == "Delivered" && outcome == "Failed")
        {
            return;
        }

        Outcome = outcome;
        Touch();
    }

    public void Redrive(DateTimeOffset now)
    {
        if (Status != "DeadLetter" || FailureCode == "AcceptanceUnknown" || !CanSend(now))
        {
            throw new InvalidOperationException("Delivery is not eligible for redrive.");
        }

        Status = "Pending";
        NextAttemptAt = now;
        FailureCode = null;
        Release();
    }

    public void ReconcileAccepted(DateTimeOffset now, string providerId)
    {
        if (providerId.Length is < 1 or > 200 || providerId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '/' and not '_' and not '.'))
        {
            throw new ArgumentException("Invalid provider reference.");
        }
        if (Status == "ProviderAccepted" && ProviderId == providerId)
        {
            return;
        }
        if (FirstAttemptAt is null || Status is not ("Pending" or "DeadLetter" or "Processing")
            || Status == "Processing" && LeaseUntil > now)
        {
            throw new InvalidOperationException("Delivery is not eligible for acceptance reconciliation.");
        }
        // An authenticated operator supplies provider-side evidence; this never resends.
        Accept(providerId);
    }

    private void Release()
    {
        LeaseToken = null;
        LeaseUntil = null;
        Touch();
    }

    private void Touch() => Version = Guid.CreateVersion7();
    public override string ToString() => $"EmailDelivery {Id} {Status} [REDACTED]";
}
