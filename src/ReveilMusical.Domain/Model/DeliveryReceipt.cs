namespace ReveilMusical.Domain.Model;

/// <summary>Accusé d'envoi : une référence opaque rendue par le canal (identifiant de message...).</summary>
public sealed record DeliveryReceipt(string Reference);
