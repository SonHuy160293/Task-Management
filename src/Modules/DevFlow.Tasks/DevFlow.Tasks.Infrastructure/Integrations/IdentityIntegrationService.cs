using DevFlow.AspNetCore.HttpClient.Abstractions;
using DevFlow.Identity.Contracts;
using DevFlow.SharedKernel.Integrations;
using DevFlow.Tasks.Application.Abstractions.Integrations;

namespace DevFlow.Tasks.Infrastructure.Integrations;

public sealed class IdentityIntegrationService(IIntegrationHttpClient<IIdentityIntegrationService> client) : IIdentityIntegrationService
{
    public Task<IntegrationResult<UserIntegrationDto>> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => client.GetAsync<UserIntegrationDto>(
            $"api/v1/integrations/users/{userId:D}",
            cancellationToken);
}
