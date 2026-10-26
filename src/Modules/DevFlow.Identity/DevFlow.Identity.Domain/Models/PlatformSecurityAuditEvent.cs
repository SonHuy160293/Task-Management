using System.Net;
using System.Text.Json;

namespace DevFlow.Identity.Domain.Models;

public sealed class PlatformSecurityAuditEvent
{
    private PlatformSecurityAuditEvent() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    // Historical actor snapshot deliberately has no foreign key or navigation.
    public Guid? ActorUserId { get; private set; }
    public Guid? TargetTenantId { get; private set; }
    public string EventCode { get; private set; } = null!;
    public string TargetType { get; private set; } = null!;
    public Guid? TargetId { get; private set; }
    public bool Success { get; private set; } = true;
    public IPAddress? ClientIp { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public JsonElement Details { get; private set; } = JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
    public DateTimeOffset OccurredAt { get; private set; } = DateTimeOffset.UtcNow;

    public Organization? TargetTenant { get; private set; }

    public static PlatformSecurityAuditEvent Create(
        string eventCode,
        string targetType,
        Guid? actorUserId = null,
        Guid? targetTenantId = null,
        Guid? targetId = null,
        bool success = true,
        IPAddress? clientIp = null,
        Guid? correlationId = null,
        JsonElement? details = null)
        => new PlatformSecurityAuditEvent
        {
            ActorUserId = actorUserId,
            TargetTenantId = targetTenantId,
            EventCode = eventCode,
            TargetType = targetType,
            TargetId = targetId,
            Success = success,
            ClientIp = clientIp,
            CorrelationId = correlationId,
            Details = details?.Clone() ?? JsonSerializer.SerializeToElement(new Dictionary<string, object?>()),
        };
}
