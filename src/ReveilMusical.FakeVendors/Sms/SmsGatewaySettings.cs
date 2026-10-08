namespace ReveilMusical.FakeVendors.Sms;

public sealed class SmsGatewaySettings
{
    public string OutboxDirectory { get; set; } = "outbox";

    public bool WriteToConsole { get; set; } = true;

    /// <summary>Simule une passerelle indisponible : chaque envoi répond 503.</summary>
    public bool SimulateOutage { get; set; }

    public string SenderName { get; set; } = "REVEIL";
}
