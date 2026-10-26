using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using DevFlow.Identity.Application.Abstractions.Security;
using DevFlow.Identity.Application.Security;
using DevFlow.Identity.Infrastructure.Security.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
namespace DevFlow.Identity.Infrastructure.Security.RateLimiting;

public sealed class RedisRegistrationRateLimiter : IRegistrationRateLimiter, IDisposable
{
    private const string Script = """
        local count = tonumber(redis.call('GET', KEYS[1]) or '0')
        if count == 0 then
            redis.call('SET', KEYS[1], 1, 'PX', ARGV[2])
            return {1, 0}
        end
        local ttl = redis.call('PTTL', KEYS[1])
        if ttl < 0 then return {-1, 0} end
        if count >= tonumber(ARGV[1]) then return {0, ttl} end
        redis.call('INCR', KEYS[1])
        return {1, 0}
        """;
    private readonly IConnectionMultiplexer _connection;
    private readonly RegistrationRateLimitOptions _options;
    private readonly byte[] _secret;
    public RedisRegistrationRateLimiter(IConnectionMultiplexer connection, IOptions<RegistrationRateLimitOptions> options)
    {
        _connection = connection;
        _options = options.Value;
        _secret = Convert.FromBase64String(_options.KeySecretBase64);
    }

    public Task<RegistrationLimitResult> CheckIpAsync(IPAddress address, CancellationToken cancellationToken)
        => CheckAsync("ip", (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString(), _options.IpPermitLimit, _options.IpWindow, cancellationToken);
    public Task<RegistrationLimitResult> CheckEmailAsync(string email, CancellationToken cancellationToken)
        => CheckAsync("email", RegistrationEmail.Normalize(email) ?? throw new ArgumentException("Invalid email.", nameof(email)), _options.EmailPermitLimit, _options.EmailWindow, cancellationToken);
    public void Dispose() => CryptographicOperations.ZeroMemory(_secret);

    private async Task<RegistrationLimitResult> CheckAsync(string dimension, string identifier, int limit, TimeSpan window, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes = Encoding.UTF8.GetBytes(identifier);
        string key;
        try
        {
            key = "devflow:identity:registration:v1:" + dimension + ":" + Convert.ToHexString(HMACSHA256.HashData(_secret, bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }

        try
        {
            if (!_connection.IsConnected)
            {
                return new(RegistrationLimitStatus.Unavailable, TimeSpan.Zero);
            }

            RedisResult result = await _connection.GetDatabase().ScriptEvaluateAsync(
                Script, [(RedisKey)key], [(RedisValue)limit, (RedisValue)(long)window.TotalMilliseconds]).WaitAsync(cancellationToken);
            RedisResult[] values = (RedisResult[])result!;
            long status = (long)values[0];
            return status == 1 ? new(RegistrationLimitStatus.Allowed, TimeSpan.Zero)
                : status == 0 ? new(RegistrationLimitStatus.Limited, TimeSpan.FromMilliseconds((long)values[1]))
                : new(RegistrationLimitStatus.Unavailable, TimeSpan.Zero);
        }
        catch (RedisException)
        {
            return new(RegistrationLimitStatus.Unavailable, TimeSpan.Zero);
        }
        catch (ObjectDisposedException)
        {
            return new(RegistrationLimitStatus.Unavailable, TimeSpan.Zero);
        }
    }

}
