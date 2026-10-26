namespace DevFlow.SharedKernel.Abstractions.Domain;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; }
    string? CreatedBy { get; }
    DateTimeOffset? UpdatedAt { get; }
    string? UpdatedBy { get; }

    void SetCreatedAudit(DateTimeOffset createdAt, string? createdBy);
    void SetUpdatedAudit(DateTimeOffset updatedAt, string? updatedBy);
}
