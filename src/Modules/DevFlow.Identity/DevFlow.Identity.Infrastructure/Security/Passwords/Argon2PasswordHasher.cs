using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DevFlow.Identity.Application.Abstractions.Security;
using DevFlow.Identity.Application.Security;
using DevFlow.Identity.Infrastructure.Security.Options;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
namespace DevFlow.Identity.Infrastructure.Security.Passwords;

public sealed class Argon2PasswordHasher : IPasswordHasher, IDisposable
{
    private readonly Argon2Options _options;
    private readonly SemaphoreSlim _slots;
    public Argon2PasswordHasher(IOptions<Argon2Options> options)
    {
        _options = options.Value;
        if (!_options.IsValid())
        {
            throw new ArgumentException("Invalid Argon2 configuration.", nameof(options));
        }

        _slots = new SemaphoreSlim(_options.MaxConcurrency);
    }

    public async Task<string> HashAsync(string password, CancellationToken cancellationToken)
    {
        if (!PasswordPolicy.IsValid(password))
        {
            throw new ArgumentException("Invalid password length or encoding.", nameof(password));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = await DeriveAsync(password, salt, _options.MemoryKiB, _options.Iterations, _options.Parallelism, cancellationToken);
        return FormattableString.Invariant($"$argon2id$v=19$m={_options.MemoryKiB},t={_options.Iterations},p={_options.Parallelism}$")
            + Encode(salt) + "$" + Encode(hash);
    }

    public async Task<PasswordVerificationResult> VerifyAsync(string password, string storedHash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (password.Length > PasswordPolicy.MaximumLength * 2 || storedHash.Length > 512)
        {
            return PasswordVerificationResult.Failed;
        }

        string[] parts = storedHash.Split('$');
        if (parts.Length != 6 || parts[0] != string.Empty || parts[1] != "argon2id" || parts[2] != "v=19")
        {
            return PasswordVerificationResult.Failed;
        }

        string[] costs = parts[3].Split(',');
        if (costs.Length != 3 || !ReadCost(costs[0], "m=", 131072, out int memory)
            || !ReadCost(costs[1], "t=", 6, out int iterations) || !ReadCost(costs[2], "p=", 4, out int parallelism)
            || memory < 8 * parallelism)
        {
            return PasswordVerificationResult.Failed;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[4] + new string('=', (4 - (parts[4].Length % 4)) % 4));
            expected = Convert.FromBase64String(parts[5] + new string('=', (4 - (parts[5].Length % 4)) % 4));
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }

        if (salt.Length != 16 || expected.Length != 32 || Encode(salt) != parts[4] || Encode(expected) != parts[5])
        {
            return PasswordVerificationResult.Failed;
        }

        byte[] actual = await DeriveAsync(password, salt, memory, iterations, parallelism, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            return PasswordVerificationResult.Failed;
        }

        return memory < _options.MemoryKiB || iterations < _options.Iterations
            ? PasswordVerificationResult.SuccessRehashNeeded : PasswordVerificationResult.Success;
    }

    public void Dispose() => _slots.Dispose();

    private static bool ReadCost(string value, string prefix, int maximum, out int cost)
    {
        cost = 0;
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(value.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out cost)
            && cost >= 1 && cost <= maximum;
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');
    private async Task<byte[]> DeriveAsync(string password, byte[] salt, int memory, int iterations, int parallelism, CancellationToken cancellationToken)
    {
        await _slots.WaitAsync(cancellationToken);
        byte[] bytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon = new Argon2id(bytes) { Salt = salt, MemorySize = memory, Iterations = iterations, DegreeOfParallelism = parallelism };
            byte[] result = await argon.GetBytesAsync(32);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            _slots.Release();
        }
    }

}
