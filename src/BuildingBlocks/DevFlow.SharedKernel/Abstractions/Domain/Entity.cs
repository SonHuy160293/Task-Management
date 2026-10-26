namespace DevFlow.SharedKernel.Abstractions.Domain;

public abstract class Entity<TId> : IEntity<TId>, IAuditableEntity
{
    protected Entity()
    {
        CreatedAt = DateTimeOffset.UtcNow;
    }

    protected Entity(TId id)
    {
        Id = id;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public TId Id { get; protected set; } = default!;
    public DateTimeOffset CreatedAt { get; protected set; }
    public string? CreatedBy { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public string? UpdatedBy { get; protected set; }

    public void SetCreatedAudit(DateTimeOffset createdAt, string? createdBy)
    {
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public void SetUpdatedAudit(DateTimeOffset updatedAt, string? updatedBy)
    {
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }
}

public abstract class Entity : Entity<Guid>, IEntity
{
    protected Entity() { }

    protected Entity(Guid id)
        : base(id)
    {
    }
}
