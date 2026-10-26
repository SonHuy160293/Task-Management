namespace DevFlow.AspNetCore.HttpClient.Abstractions;

public interface ICorrelationIdAccessor
{
    Guid? GetCorrelationId();
}
