namespace ReveilMusical.Api.Contracts;

/// <summary>
/// Le rapport de réveil renvoyé à l'ordonnanceur. Même forme en 200 (remis) et en 503 (non remis,
/// opérateur alerté). Aucun champ propre à un fournisseur ou à un SDK.
/// </summary>
internal sealed record WakeUpHttpResponse(
    string UserId,
    bool Delivered,
    bool Degraded,
    DateTimeOffset TriggeredAtUtc,
    TrackHttpResponse Track,
    NotificationHttpResponse Notification);

internal sealed record TrackHttpResponse(string Title, string Artist, string Source, string Preference, string? Requested);

internal sealed record NotificationHttpResponse(
    string PreferredChannel,
    string? DeliveredOn,
    bool OperatorAlerted,
    IReadOnlyList<DeliveryAttemptHttpResponse> Attempts);

internal sealed record DeliveryAttemptHttpResponse(string Channel, string Status, string? Detail);
