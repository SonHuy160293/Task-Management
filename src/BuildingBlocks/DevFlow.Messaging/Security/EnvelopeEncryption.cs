using System.Security.Cryptography;

namespace DevFlow.Messaging.Security;
public sealed record CipherEnvelope(string WrappedKey, string Nonce, string Tag, string Ciphertext)
{
    public override string ToString() => "CipherEnvelope [REDACTED]";
}

public static class EnvelopeEncryption
{
    public static CipherEnvelope Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData, string publicKeyPem)
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        try
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] tag = new byte[16];
            byte[] cipher = new byte[plaintext.Length];
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plaintext, cipher, tag, associatedData);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            if (rsa.KeySize < 3072)
            {
                throw new CryptographicException("RSA key must be at least 3072 bits.");
            }

            return new(Convert.ToBase64String(rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA256)), Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(cipher));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static byte[] Decrypt(CipherEnvelope payload, ReadOnlySpan<byte> associatedData, string privateKeyPem)
    {
        if (payload.Ciphertext.Length > 65536)
        {
            throw new CryptographicException("Payload too large.");
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        if (rsa.KeySize < 3072)
        {
            throw new CryptographicException("RSA key must be at least 3072 bits.");
        }

        byte[] key = rsa.Decrypt(Convert.FromBase64String(payload.WrappedKey), RSAEncryptionPadding.OaepSHA256);
        try
        {
            byte[] cipher = Convert.FromBase64String(payload.Ciphertext);
            byte[] plain = new byte[cipher.Length];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(Convert.FromBase64String(payload.Nonce), cipher, Convert.FromBase64String(payload.Tag), plain, associatedData);
            return plain;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
