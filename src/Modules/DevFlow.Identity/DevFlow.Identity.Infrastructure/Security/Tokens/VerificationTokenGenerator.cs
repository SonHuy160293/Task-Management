using System.Security.Cryptography;
using System.Text;
using DevFlow.Identity.Application.Abstractions.Security;
using DevFlow.Identity.Application.Security;
using DevFlow.Identity.Infrastructure.Security.Options;
using Microsoft.Extensions.Options;
namespace DevFlow.Identity.Infrastructure.Security.Tokens;

public sealed class VerificationTokenGenerator(TimeProvider clock, IOptions<VerificationTokenOptions> options) : IVerificationTokenGenerator
{
    public VerificationToken Generate(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        string raw = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        CryptographicOperations.ZeroMemory(bytes);
        DateTimeOffset createdAt = clock.GetUtcNow();
        return new VerificationToken(raw, SHA256.HashData(Encoding.UTF8.GetBytes(raw)), createdAt, createdAt.Add(options.Value.Lifetime));
    }
}
