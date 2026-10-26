using System.Net;
using DevFlow.Identity.Application.Security;
namespace DevFlow.Identity.Application.Abstractions.Security;

public interface IRegistrationRateLimiter
{
    Task<RegistrationLimitResult> CheckIpAsync(IPAddress address, CancellationToken cancellationToken);
    Task<RegistrationLimitResult> CheckEmailAsync(string email, CancellationToken cancellationToken);
}
