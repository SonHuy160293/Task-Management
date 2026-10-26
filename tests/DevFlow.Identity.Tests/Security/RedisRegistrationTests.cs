using System.Net;
using System.Security.Cryptography;
using DevFlow.Identity.Application.Security;
using DevFlow.Identity.Infrastructure.Security.Options;
using DevFlow.Identity.Infrastructure.Security.RateLimiting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace DevFlow.Identity.Tests.Security;

public sealed class RedisFactAttribute : FactAttribute
{
    public RedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVFLOW_TEST_REDIS")))
        {
            Skip = "Set DEVFLOW_TEST_REDIS to an isolated Redis endpoint.";
        }
    }
}

public sealed class RedisRegistrationTests
{
    [RedisFact]
    public async Task IndependentClientsShareAtomicNormalizedBuckets()
    {
        string endpoint = Environment.GetEnvironmentVariable("DEVFLOW_TEST_REDIS")!;
        using var first = await ConnectionMultiplexer.ConnectAsync(endpoint);
        using var second = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var options = Options.Create(new RegistrationRateLimitOptions
        {
            RedisConnectionString = endpoint,
            KeySecretBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        });
        using var a = new RedisRegistrationRateLimiter(first, options);
        using var b = new RedisRegistrationRateLimiter(second, options);
        RegistrationLimitResult[] results = await Task.WhenAll(Enumerable.Range(0, 40).Select(i =>
            (i % 2 == 0 ? a : b).CheckEmailAsync(i % 2 == 0 ? "Alice@example.com" : " ALICE@example.com ", CancellationToken.None)));
        Assert.Equal(5, results.Count(result => result.Status == RegistrationLimitStatus.Allowed));
        Assert.Equal(35, results.Count(result => result.Status == RegistrationLimitStatus.Limited));
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(RegistrationLimitStatus.Allowed, (await a.CheckIpAsync(IPAddress.Loopback, CancellationToken.None)).Status);
        }

        Assert.Equal(
            RegistrationLimitStatus.Limited,
            (await b.CheckIpAsync(IPAddress.Parse("::ffff:127.0.0.1"), CancellationToken.None)).Status);
        await first.CloseAsync();
        Assert.Equal(
            RegistrationLimitStatus.Unavailable,
            (await a.CheckEmailAsync("other@example.com", CancellationToken.None)).Status);
    }

    [RedisFact]
    public async Task WindowExpiresAndCancellationPropagates()
    {
        string endpoint = Environment.GetEnvironmentVariable("DEVFLOW_TEST_REDIS")!;
        using var connection = await ConnectionMultiplexer.ConnectAsync(endpoint);
        using var limiter = new RedisRegistrationRateLimiter(connection, Options.Create(new RegistrationRateLimitOptions
        {
            RedisConnectionString = endpoint,
            KeySecretBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            EmailPermitLimit = 1,
            EmailWindow = TimeSpan.FromSeconds(1),
        }));
        Assert.Equal(RegistrationLimitStatus.Allowed, (await limiter.CheckEmailAsync("test@example.com", CancellationToken.None)).Status);
        var rejected = await limiter.CheckEmailAsync("test@example.com", CancellationToken.None);
        Assert.Equal(RegistrationLimitStatus.Limited, rejected.Status);
        Assert.True(rejected.RetryAfter > TimeSpan.Zero);
        await Task.Delay(TimeSpan.FromMilliseconds(1100));
        Assert.Equal(RegistrationLimitStatus.Allowed, (await limiter.CheckEmailAsync("test@example.com", CancellationToken.None)).Status);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => limiter.CheckEmailAsync("test@example.com", source.Token));
    }
}
