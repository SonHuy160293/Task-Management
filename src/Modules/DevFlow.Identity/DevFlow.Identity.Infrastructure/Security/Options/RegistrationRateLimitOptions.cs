namespace DevFlow.Identity.Infrastructure.Security.Options;

public sealed class RegistrationRateLimitOptions
{
    public string RedisConnectionString { get; set; } = string.Empty;
    public string KeySecretBase64 { get; set; } = string.Empty;
    public int IpPermitLimit { get; set; } = 20;
    public TimeSpan IpWindow { get; set; } = TimeSpan.FromMinutes(10);
    public int EmailPermitLimit { get; set; } = 5;
    public TimeSpan EmailWindow { get; set; } = TimeSpan.FromHours(1);
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(RedisConnectionString) || IpPermitLimit is < 1 or > 10000 || EmailPermitLimit is < 1 or > 10000
            || IpWindow < TimeSpan.FromSeconds(1) || IpWindow > TimeSpan.FromDays(1)
            || EmailWindow < TimeSpan.FromSeconds(1) || EmailWindow > TimeSpan.FromDays(1))
        {
            return false;
        }

        Span<byte> secret = stackalloc byte[64];
        return Convert.TryFromBase64String(KeySecretBase64, secret, out int bytes) && bytes is >= 32 and <= 64;
    }

    public override string ToString() => "RegistrationRateLimitOptions [REDACTED]";
}
