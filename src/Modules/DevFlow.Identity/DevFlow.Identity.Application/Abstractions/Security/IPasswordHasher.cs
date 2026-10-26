using DevFlow.Identity.Application.Security;
namespace DevFlow.Identity.Application.Abstractions.Security;

public interface IPasswordHasher
{
    Task<string> HashAsync(string password, CancellationToken cancellationToken);
    Task<PasswordVerificationResult> VerifyAsync(string password, string storedHash, CancellationToken cancellationToken);
}
