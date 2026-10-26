using DevFlow.SharedKernel.Integrations;
using DevFlow.Identity.Contracts;

namespace DevFlow.Tasks.Application.Abstractions.Integrations;

public interface IIdentityIntegrationService
{
    Task<IntegrationResult<UserIntegrationDto>> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
