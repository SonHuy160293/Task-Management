using System.Security.Cryptography;
using System.Text;
using DevFlow.Identity.Application.Abstractions.Security;
using DevFlow.Identity.Infrastructure.DepedencyInjection;
using DevFlow.Identity.Infrastructure.Security.Options;
using DevFlow.Identity.Infrastructure.Security.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
namespace DevFlow.Identity.Tests.Security;

public sealed class TokenAndOptionsTests
{
    [Fact]
    public void TokenUsesClockDigestOfBearerAndRedactedDiagnostics()
    {
        var clock = new FixedClock();
        var generator = new VerificationTokenGenerator(clock, Options.Create(new VerificationTokenOptions()));
        var token = generator.Generate(CancellationToken.None);
        Assert.Equal(43, token.RawToken.Length);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(token.RawToken)), token.TokenHash);
        Assert.Equal(clock.GetUtcNow(), token.CreatedAt);
        Assert.Equal(token.CreatedAt.AddHours(24), token.ExpiresAt);
        Assert.Equal("verify_email", token.Purpose);
        Assert.DoesNotContain(token.RawToken, token.ToString());
        Assert.NotEqual(token.RawToken, generator.Generate(CancellationToken.None).RawToken);
    }

    [Fact]
    public void InvalidConfigurationFailsBeforeSecurityServicesResolve()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentitySecurity(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<RegistrationRateLimitOptions>>().Value);
        Assert.IsAssignableFrom<IPasswordHasher>(provider.GetRequiredService<IPasswordHasher>());
        Assert.False(new Argon2Options { MemoryKiB = int.MaxValue }.IsValid());
        Assert.False(new VerificationTokenOptions { Lifetime = TimeSpan.FromDays(2) }.IsValid());
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
    }
}
