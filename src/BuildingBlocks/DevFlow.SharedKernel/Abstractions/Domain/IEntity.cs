namespace DevFlow.SharedKernel.Abstractions.Domain;

public interface IEntity<out TId>
{
    TId Id { get; }
}

public interface IEntity : IEntity<Guid> { }
