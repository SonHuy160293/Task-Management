using System.Net;
using System.Text.Json;

namespace DevFlow.Identity.Domain.Models;

public sealed class SecurityAuditEvent
{
    private SecurityAuditEvent() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid TenantId { get; private set; }
    // Historical actor snapshots deliberately have no foreign keys or navigations.
    public Guid? ActorUserId { get; private set; }
    public Guid? ActorMemberId { get; private set; }
    public string EventCode { get; private set; } = null!;
    public string TargetType { get; private set; } = null!;
    public Guid? TargetId { get; private set; }
    public bool Success { get; private set; } = true;
    public IPAddress? ClientIp { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public JsonElement Details { get; private set; } = JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
    public DateTimeOffset OccurredAt { get; private set; } = DateTimeOffset.UtcNow;

    public Organization Tenant { get; private set; } = null!;

    public static SecurityAuditEvent Create(
        Guid tenantId,
        string eventCode,
        string targetType,
        Guid? actorUserId = null,
        Guid? actorMemberId = null,
        Guid? targetId = null,
        bool success = true,
        IPAddress? clientIp = null,
        Guid? correlationId = null,
        JsonElement? details = null)
        => new SecurityAuditEvent
        {
            TenantId = tenantId,
            ActorUserId = actorUserId,
            ActorMemberId = actorMemberId,
            EventCode = eventCode,
            TargetType = targetType,
            TargetId = targetId,
            Success = success,
            ClientIp = clientIp,
            CorrelationId = correlationId,
            Details = details?.Clone() ?? JsonSerializer.SerializeToElement(new Dictionary<string, object?>()),
        };
}
