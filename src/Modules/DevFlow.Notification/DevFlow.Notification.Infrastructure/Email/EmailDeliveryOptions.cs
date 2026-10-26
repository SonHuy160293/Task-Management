namespace DevFlow.Notification.Infrastructure.Email;
public sealed class EmailDeliveryOptions
{
    public string Mode { get; set; } = "Disabled";
    public bool DomainVerified { get; set; }
    public string Sender { get; set; } = "";
    public string SupportAddress { get; set; } = "";
    public string FrontendOrigin { get; set; } = "http://localhost:3000";
    public string KeyDirectory { get; set; } = "";
    public string ActiveKeyId { get; set; } = "local-v1";
    public string ApiKeyFile { get; set; } = "";
    public string WebhookSecretFile { get; set; } = "";
    public string RecipientHashKeyFile { get; set; } = "";
    public string MailpitHost { get; set; } = "localhost";
    public int MailpitPort { get; set; } = 1025;
    public int DailyLimit { get; set; } = 100;
    public int MonthlyLimit { get; set; } = 3000;
    public bool Development { get; set; }

    public bool Valid() => Mode is "Disabled" or "Mailpit" or "Resend" && DailyLimit is >= 1 and <= 100 && MonthlyLimit is >= 1 and <= 3000 && (Mode != "Mailpit" || Development && MailpitPort is >= 1 and <= 65535) && (Mode != "Resend" || DomainVerified && HasSecret(ApiKeyFile) && HasSecret(WebhookSecretFile) && Uri.TryCreate(FrontendOrigin, UriKind.Absolute, out var uri) && uri.Scheme == "https" && !string.IsNullOrWhiteSpace(Sender)) && (Mode == "Disabled" || Directory.Exists(KeyDirectory) && HasSecret(RecipientHashKeyFile) && File.Exists(Path.Combine(KeyDirectory, ActiveKeyId + ".private.pem")) && File.Exists(Path.Combine(KeyDirectory, ActiveKeyId + ".public.pem")) && System.Net.Mail.MailAddress.TryCreate(Sender, out _));
    public bool WebhookValid() => Mode is "Disabled" or "Mailpit" or "Resend" && (Mode != "Resend" || HasSecret(WebhookSecretFile) && HasSecret(RecipientHashKeyFile));
    private static bool HasSecret(string path) => File.Exists(path) && new FileInfo(path).Length is> 0 and <= 8192 && !string.IsNullOrWhiteSpace(File.ReadAllText(path));
    public override string ToString() => "EmailDeliveryOptions [REDACTED]";
}
