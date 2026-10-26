using DevFlow.SharedKernel.Integrations;

namespace DevFlow.AspNetCore.HttpClient.Abstractions;

public interface IIntegrationHttpClient<TDestination>
{
    Task<IntegrationResult<TResponse>> PostExternalJsonAsync<TResponse>(
        string endpoint, string json, string idempotencyKey, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("External JSON transport is not implemented by this adapter.");

    Task<IntegrationResult<TResponse>> GetAsync<TResponse>(
        string endpoint,
        CancellationToken cancellationToken = default);

    Task<IntegrationResult<TResponse>> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default);

    Task<IntegrationResult<TResponse>> PutAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default);

    Task<IntegrationResult> DeleteAsync(
        string endpoint,
        CancellationToken cancellationToken = default);
}
