using System.Security.Cryptography;
using DevFlow.Identity.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DevFlow.Identity.Tests.Security;

public sealed class VerificationEmailKeyTests
{
    [Fact]
    public void ValidPublicKeyLoadsAndInvalidConfigurationIsSanitized()
    {
        string path = Path.GetTempFileName();
        try
        {
            using RSA valid = RSA.Create(3072);
            File.WriteAllText(path, valid.ExportSubjectPublicKeyInfoPem());
            Assert.NotNull(VerificationEmailRequestFactory.Load(Config("key-1", path)));
            AssertInvalid(Config("../bad", path), path);
            AssertInvalid(Config("key-1", path + ".missing"), path);
            using RSA small = RSA.Create(2048);
            File.WriteAllText(path, small.ExportSubjectPublicKeyInfoPem());
            AssertInvalid(Config("key-1", path), path);
            File.WriteAllText(path, valid.ExportRSAPrivateKeyPem());
            AssertInvalid(Config("key-1", path), path);
            File.WriteAllText(path, "invalid");
            AssertInvalid(Config("key-1", path), path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static IConfiguration Config(string id, string path) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Identity:VerificationEmail:KeyId"] = id,
        ["Identity:VerificationEmail:PublicKeyFile"] = path,
    }).Build();

    private static void AssertInvalid(IConfiguration config, string sensitive)
    {
        var error = Assert.Throws<InvalidOperationException>(() => VerificationEmailRequestFactory.Load(config));
        Assert.DoesNotContain(sensitive, error.ToString());
    }
}
