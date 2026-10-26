using DevFlow.Identity.Application.Abstractions.Security;
using DevFlow.Identity.Infrastructure.Security.Options;
using DevFlow.Identity.Infrastructure.Security.Passwords;
using DevFlow.Identity.Infrastructure.Security.RateLimiting;
using DevFlow.Identity.Infrastructure.Security.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
namespace DevFlow.Identity.Infrastructure.DepedencyInjection;

public static class IdentitySecurityExtensions
{
    public static IServiceCollection AddIdentitySecurity(this IServiceCollection services, IConfiguration configuration)
    {
        const string prefix = "Identity:Security:";
        services.AddOptions<Argon2Options>().Bind(configuration.GetSection(prefix + "Argon2")).Validate(x => x.IsValid(), "Invalid Argon2 costs.").ValidateOnStart();
        services.AddOptions<VerificationTokenOptions>().Bind(configuration.GetSection(prefix + "VerificationToken")).Validate(x => x.IsValid(), "Invalid token lifetime.").ValidateOnStart();
        services.AddOptions<RegistrationRateLimitOptions>().Bind(configuration.GetSection(prefix + "RegistrationRateLimit")).Validate(x => x.IsValid(), "Redis connection and a 32-64 byte Base64 HMAC secret are required; limits must be bounded.").ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<IVerificationTokenGenerator, VerificationTokenGenerator>();
        services.TryAddSingleton<IConnectionMultiplexer>(provider =>
        {
            var options = ConfigurationOptions.Parse(provider.GetRequiredService<IOptions<RegistrationRateLimitOptions>>().Value.RedisConnectionString);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 2500;
            options.AsyncTimeout = 2500;
            options.BacklogPolicy = BacklogPolicy.FailFast;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IRegistrationRateLimiter, RedisRegistrationRateLimiter>();

        return services;
    }
}
