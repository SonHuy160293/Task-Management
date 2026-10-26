namespace DevFlow.Notification.Contracts;
public sealed record EmailDeliveryRecordedV1(Guid MessageId, Guid DeliveryMessageId, Guid CorrelationId, DateTimeOffset RecordedAt)
{
    public const string EventType = "notification.email-recorded.v1";
}
