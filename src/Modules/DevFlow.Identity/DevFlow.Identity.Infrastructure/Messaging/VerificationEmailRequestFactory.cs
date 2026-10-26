using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DevFlow.Identity.Application.Abstractions.Messaging;
using DevFlow.Notification.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.Identity.Infrastructure.Messaging;

public sealed class VerificationEmailRequestFactory : IVerificationEmailRequestFactory
{
    private readonly string _keyId;
    private readonly string _publicKey;

    public VerificationEmailRequestFactory(string keyId, string publicKey)
    {
        _keyId = keyId;
        _publicKey = publicKey;
    }

    public EmailDeliveryRequestedV1 Create(Guid messageId, string recipient, string displayName, string locale, string rawToken, DateTimeOffset createdAt, DateTimeOffset expiresAt)
        => VerificationEmailEnvelopeFactory.Create(messageId, messageId, null, recipient, displayName, locale, rawToken, createdAt, expiresAt, _keyId, _publicKey);

    public static VerificationEmailRequestFactory Load(IConfiguration configuration)
    {
        string? keyId = configuration["Identity:VerificationEmail:KeyId"];
        string? file = configuration["Identity:VerificationEmail:PublicKeyFile"];
        try
        {
            if (keyId is null || !Regex.IsMatch(keyId, "\\A[A-Za-z0-9-]{1,64}\\z") || string.IsNullOrWhiteSpace(file))
                throw new InvalidOperationException();
            string pem = File.ReadAllText(file);
            if (!pem.TrimStart().StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal))
                throw new InvalidOperationException();
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 3072)
                throw new InvalidOperationException();
            return new VerificationEmailRequestFactory(keyId, pem);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or CryptographicException or InvalidOperationException or NotSupportedException)
        {
            throw new InvalidOperationException("Identity verification email public key configuration is missing or invalid.");
        }
    }
}

public static class IdentityVerificationEmailExtensions
{
    public static IServiceCollection AddIdentityVerificationEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IVerificationEmailRequestFactory>(VerificationEmailRequestFactory.Load(configuration));
        services.AddScoped<IVerificationEmailOutbox, IdentityEmailOutbox>();
        return services;
    }
}
