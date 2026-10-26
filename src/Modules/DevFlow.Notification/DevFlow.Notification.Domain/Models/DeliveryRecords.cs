namespace DevFlow.Notification.Domain.Models;
public sealed class EmailDeliveryAttempt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DeliveryId { get; set; }
    public int Attempt { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public string Outcome { get; set; } = "Unknown";
    public string? FailureCode { get; set; }
}

public sealed class EmailSuppression
{
    public string RecipientHash { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class EmailWebhookReceipt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string EventId { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public string EventType { get; set; } = "";
    public string? RecipientHash { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}

public sealed class EmailDeliveryControl
{
    public int Id { get; set; } = 1;
    public DateTimeOffset? PausedUntil { get; set; }
    public string? PauseReason { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTimeOffset NextRequestAt { get; set; }
    public string Day { get; set; } = "";
    public int DailyReservations { get; set; }
    public string Month { get; set; } = "";
    public int MonthlyReservations { get; set; }
}

public sealed class EmailOperation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? DeliveryId { get; set; }
    public string Operator { get; set; } = "";
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
