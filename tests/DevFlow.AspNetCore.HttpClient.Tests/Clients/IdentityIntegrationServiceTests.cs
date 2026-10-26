using System.Net;
using DevFlow.AspNetCore.HttpClient.Abstractions;
using DevFlow.Identity.Contracts;
using DevFlow.SharedKernel.Integrations;
using DevFlow.Tasks.Application.Abstractions.Integrations;
using DevFlow.Tasks.Infrastructure.Integrations;

namespace DevFlow.AspNetCore.HttpClient.Tests.Clients;

public sealed class IdentityIntegrationServiceTests
{
    [Fact]
    public async Task AdapterForwardsEndpointCancellationAndResult()
    {
        Guid userId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var expected = IntegrationResult<UserIntegrationDto>.Success(
            new UserIntegrationDto(userId, "Ada", null, "active"),
            HttpStatusCode.OK);
        var transport = new FakeTransport(expected);
        var service = new IdentityIntegrationService(transport);

        IntegrationResult<UserIntegrationDto> actual = await service.GetUserAsync(userId, cancellation.Token);

        Assert.Same(expected, actual);
        Assert.Equal($"api/v1/integrations/users/{userId:D}", transport.Endpoint);
        Assert.Equal(cancellation.Token, transport.CancellationToken);
    }

    private sealed class FakeTransport(IntegrationResult<UserIntegrationDto> result)
        : IIntegrationHttpClient<IIdentityIntegrationService>
    {
        public string? Endpoint { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<IntegrationResult<TResponse>> GetAsync<TResponse>(
            string endpoint,
            CancellationToken cancellationToken = default)
        {
            Endpoint = endpoint;
            CancellationToken = cancellationToken;
            return Task.FromResult((IntegrationResult<TResponse>)(object)result);
        }

        public Task<IntegrationResult<TResponse>> PostAsync<TRequest, TResponse>(
            string endpoint,
            TRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IntegrationResult<TResponse>> PutAsync<TRequest, TResponse>(
            string endpoint,
            TRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IntegrationResult> DeleteAsync(
            string endpoint,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
