namespace ReveilMusical.FakeVendors.Mail;

public sealed class SmtpMailSettings
{
    public string OutboxDirectory { get; set; } = "outbox";

    public bool WriteToConsole { get; set; } = true;

    /// <summary>Simule un serveur SMTP injoignable : chaque envoi lève.</summary>
    public bool SimulateOutage { get; set; }

    public string FromAddress { get; set; } = "reveil@example.com";
}
