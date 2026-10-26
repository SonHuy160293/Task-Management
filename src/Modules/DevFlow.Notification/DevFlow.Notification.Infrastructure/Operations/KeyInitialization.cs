using System.Security.Cryptography;

namespace DevFlow.Notification.Infrastructure.Operations;
public static class KeyInitialization
{
    // Only explicit local operator invocation creates keys. Never rotate automatically at startup.
    public static void Create(string directory, string id)
    {
        if (id.Length is < 1 or > 64 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
        {
            throw new ArgumentException("Invalid key id.");
        }

        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        string privateFile = Path.Combine(directory, id + ".private.pem");
        string publicFile = Path.Combine(directory, id + ".public.pem");
        if (File.Exists(privateFile) || File.Exists(publicFile))
        {
            throw new InvalidOperationException("Key already exists.");
        }

        using var rsa = RSA.Create(3072);
        using (var stream = new FileStream(privateFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(rsa.ExportPkcs8PrivateKeyPem());
            }
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(privateFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.WriteAllText(publicFile, rsa.ExportSubjectPublicKeyInfoPem());
        string hashFile = Path.Combine(directory, "recipient-hmac.secret");
        if (!File.Exists(hashFile))
        {
            using var stream = new FileStream(hashFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream);
            writer.Write(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(hashFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        Console.WriteLine("Key files created. Back up keys separately and restrict filesystem access.");
    }
}
