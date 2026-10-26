namespace DevFlow.SharedKernel.Abstractions.Domain;

public abstract class SoftDeletableEntity<TId> : Entity<TId>, ISoftDeletableEntity
{
    protected SoftDeletableEntity() { }

    protected SoftDeletableEntity(TId id)
        : base(id)
    {
    }

    public bool IsDeleted { get; protected set; }
    public DateTimeOffset? DeletedAt { get; protected set; }
    public string? DeletedBy { get; protected set; }

    public void SetDeletedAudit(DateTimeOffset deletedAt, string? deletedBy)
    {
        IsDeleted = true;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
    }

    protected void MarkDeleted()
    {
        IsDeleted = true;
    }
}

public abstract class SoftDeletableEntity : SoftDeletableEntity<Guid>, IEntity
{
    protected SoftDeletableEntity() { }

    protected SoftDeletableEntity(Guid id)
        : base(id)
    {
    }
}
