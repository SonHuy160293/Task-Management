namespace DevFlow.SharedKernel.Abstractions.Domain;

public interface ISoftDeletableEntity
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedAt { get; }
    string? DeletedBy { get; }

    void SetDeletedAudit(DateTimeOffset deletedAt, string? deletedBy);
}
