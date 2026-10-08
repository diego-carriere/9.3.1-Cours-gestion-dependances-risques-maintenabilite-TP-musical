namespace ReveilMusical.FakeVendors.Push;

/// <summary>Charge utile en clé/valeur : « title » et « body » sont attendus.</summary>
public sealed record PushRequest(string DeviceToken, IReadOnlyDictionary<string, string> Payload);

public enum PushDeliveryState
{
    Delivered,
    Rejected,
    ServiceDown,
}

public sealed record PushDeliveryReport(string? TicketId, PushDeliveryState State, string? Reason);
