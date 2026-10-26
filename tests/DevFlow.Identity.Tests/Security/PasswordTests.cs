using DevFlow.Identity.Application.Security;
using DevFlow.Identity.Infrastructure.Security.Options;
using DevFlow.Identity.Infrastructure.Security.Passwords;
using Microsoft.Extensions.Options;
using Xunit;
namespace DevFlow.Identity.Tests.Security;

public sealed class PasswordTests
{
    private const string Password = "a long unique passphrase 2026";
    [Theory]
    [InlineData(14, false)]
    [InlineData(15, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void PolicyCountsScalars(int length, bool valid)
        => Assert.Equal(valid, PasswordPolicy.IsValid(string.Concat(Enumerable.Repeat("😀", length))));
    [Fact]
    public void PolicyRejectsMalformedEncoding() => Assert.False(PasswordPolicy.IsValid(new string('a', 20) + "\ud800"));
    [Fact]
    public async Task HashRoundTripSaltAndWhitespace()
    {
        using var hasher = new Argon2PasswordHasher(Options.Create(new Argon2Options { MemoryKiB = 19456, Iterations = 2 }));
        string first = await hasher.HashAsync(Password, CancellationToken.None);
        string second = await hasher.HashAsync(Password, CancellationToken.None);
        Assert.NotEqual(first, second);
        Assert.StartsWith("$argon2id$v=19$", first);
        Assert.Equal(PasswordVerificationResult.Success, await hasher.VerifyAsync(Password, first, CancellationToken.None));
        Assert.Equal(PasswordVerificationResult.Failed, await hasher.VerifyAsync(Password + " ", first, CancellationToken.None));
        Assert.Equal(PasswordVerificationResult.Failed, await hasher.VerifyAsync(Password, first.Replace("m=19456", "m=999999"), CancellationToken.None));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hasher.HashAsync(Password, cancelled.Token));
    }

    [Fact]
    public async Task WeakerHashNeedsRehash()
    {
        using var weak = new Argon2PasswordHasher(Options.Create(new Argon2Options { MemoryKiB = 19456, Iterations = 2 }));
        using var strong = new Argon2PasswordHasher(Options.Create(new Argon2Options { MemoryKiB = 19456, Iterations = 3 }));
        string hash = await weak.HashAsync(Password, CancellationToken.None);
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, await strong.VerifyAsync(Password, hash, CancellationToken.None));
    }

    [Theory]
    [InlineData("passwordpassword")]
    [InlineData("passwordpasswordpassword")]
    [InlineData("123456789012345")]
    [InlineData("1234567890123456")]
    [InlineData("qwertyuiopasdfgh")]
    [InlineData("correct horse battery staple")]
    [InlineData("letmeinletmeinletmein")]
    public void LocalBlocklistRejectsEveryEntryCaseInsensitively(string password)
    {
        Assert.True(CommonPasswordBlocklist.Contains(password));
        Assert.True(CommonPasswordBlocklist.Contains(password.ToUpperInvariant()));
    }

    [Theory]
    [InlineData("passwordpassword ")]
    [InlineData(" passwordpassword")]
    [InlineData("a long unique passphrase 2026")]
    public void LocalBlocklistMatchesTheWholePasswordWithoutTrimming(string password)
        => Assert.False(CommonPasswordBlocklist.Contains(password));
}
