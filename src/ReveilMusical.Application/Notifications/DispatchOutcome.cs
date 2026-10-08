using ReveilMusical.Domain.Model;

namespace ReveilMusical.Application.Notifications;

public enum DeliveryStatus
{
    Delivered,
    Failed,

    /// <summary>L'utilisateur n'a pas de coordonnée pour ce canal.</summary>
    NoContact,

    /// <summary>Aucune implémentation n'est enregistrée sous cet identifiant.</summary>
    NotRegistered,
}

/// <summary>Une tentative d'envoi ; <c>Detail</c> porte la référence d'envoi en cas de succès, la cause sinon.</summary>
public sealed record DeliveryAttempt(ChannelId Channel, DeliveryStatus Status, string? Detail);

public sealed record DispatchOutcome(IReadOnlyList<DeliveryAttempt> Attempts, ChannelId? DeliveredOn, bool OperatorAlerted);
