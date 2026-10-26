namespace DevFlow.Messaging.Configuration;
public sealed class MessagingOptions
{
    public string Host { get; set; } = "localhost";
    public ushort Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/devflow-dev";
    public string Username { get; set; } = "";
    public string PasswordFile { get; set; } = "";
    public bool UseTls { get; set; }
    public bool Development { get; set; }
    public int QuorumSize { get; set; } = 1;

    public bool Valid() => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Username) && File.Exists(PasswordFile) && (Development || (UseTls && QuorumSize >= 3)) && QuorumSize is >= 1 and <= 7;
    public override string ToString() => "MessagingOptions [REDACTED]";
}
