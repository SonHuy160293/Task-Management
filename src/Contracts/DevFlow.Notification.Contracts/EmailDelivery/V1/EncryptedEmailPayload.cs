namespace DevFlow.Notification.Contracts;

public sealed record EncryptedEmailPayload(int Version, string KeyId, string WrappedKey, string Nonce, string Tag, string Ciphertext)
{
    public override string ToString() => "EncryptedEmailPayload [REDACTED]";
}
