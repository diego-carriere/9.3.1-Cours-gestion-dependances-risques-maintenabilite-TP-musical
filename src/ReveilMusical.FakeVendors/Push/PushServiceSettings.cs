namespace ReveilMusical.FakeVendors.Push;

public sealed class PushServiceSettings
{
    public string OutboxDirectory { get; set; } = "outbox";

    public bool WriteToConsole { get; set; } = true;

    /// <summary>Simule un service de push indisponible : chaque rapport annonce ServiceDown.</summary>
    public bool SimulateOutage { get; set; }
}
