using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReveilMusical.Application.Options;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Application.Notifications;

/// <summary>
/// Envoie le message en cascade : canal préféré, puis <see cref="WakeUpOptions.FallbackChannels"/>,
/// en sautant les canaux sans coordonnée ou sans implémentation. Si aucun n'aboutit, l'opérateur
/// est alerté : un réveil raté n'est jamais silencieux. Le choix du canal (Strategy) se fait par
/// <see cref="ChannelId"/> : cette classe ne connaît aucun canal concret.
/// </summary>
public sealed partial class NotificationDispatcher
{
    private readonly INotificationChannelResolver _resolver;
    private readonly IOperatorAlerter _alerter;
    private readonly IReadOnlyList<ChannelId> _fallbackChannels;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationChannelResolver resolver,
        IOperatorAlerter alerter,
        IOptions<WakeUpOptions> options,
        ILogger<NotificationDispatcher> logger)
    {
        _resolver = resolver;
        _alerter = alerter;
        // Les identifiants ont été validés au démarrage (WakeUpOptionsValidator).
        _fallbackChannels = [.. options.Value.FallbackChannels.Select(c => ChannelId.Create(c).Value)];
        _logger = logger;
    }

    public async Task<DispatchOutcome> DispatchAsync(UserProfile profile, WakeUpMessage message, CancellationToken cancellationToken)
    {
        var attempts = new List<DeliveryAttempt>();

        foreach (var channel in Candidates(profile))
        {
            var attempt = await TryDeliverAsync(profile, channel, message, cancellationToken).ConfigureAwait(false);
            attempts.Add(attempt);

            if (attempt.Status == DeliveryStatus.Delivered)
            {
                return new DispatchOutcome(attempts, channel, OperatorAlerted: false);
            }
        }

        var summary = $"Aucun canal n'a pu réveiller l'utilisateur {profile.Id} : " +
                      string.Join(", ", attempts.Select(a => $"{a.Channel} ({a.Status}: {a.Detail})")) + ".";
        // Jamais annulée : une alerte qui ne part pas, c'est le silence que le brief interdit.
        await _alerter.RaiseAsync(new OperatorAlert(profile.Id, summary), CancellationToken.None).ConfigureAwait(false);

        return new DispatchOutcome(attempts, DeliveredOn: null, OperatorAlerted: true);
    }

    private IEnumerable<ChannelId> Candidates(UserProfile profile) =>
        new[] { profile.PreferredChannel }.Concat(_fallbackChannels).Distinct();

    private async Task<DeliveryAttempt> TryDeliverAsync(
        UserProfile profile, ChannelId channel, WakeUpMessage message, CancellationToken cancellationToken)
    {
        var contact = profile.ContactFor(channel);
        if (contact is null)
        {
            return new DeliveryAttempt(channel, DeliveryStatus.NoContact, null);
        }

        var implementation = _resolver.Resolve(channel);
        if (implementation is null)
        {
            LogNotRegistered(channel.Value);
            return new DeliveryAttempt(channel, DeliveryStatus.NotRegistered, null);
        }

        var sent = await SendAsync(implementation, channel, contact, message, cancellationToken).ConfigureAwait(false);

        if (sent.IsSuccess)
        {
            return new DeliveryAttempt(channel, DeliveryStatus.Delivered, sent.Value.Reference);
        }

        LogDeliveryFailed(channel.Value, sent.Error.Kind.ToString(), sent.Error.Message);
        return new DeliveryAttempt(channel, DeliveryStatus.Failed, sent.Error.Message);
    }

    /// <summary>
    /// Filet du métier, indépendant de l'Infrastructure : un canal qui lève malgré son contrat (bug,
    /// panne imprévue) compte comme un échec, et la cascade continue. Seule l'annulation demandée par
    /// l'appelant se propage.
    /// </summary>
    private async Task<Result<DeliveryReceipt>> SendAsync(
        INotificationChannel implementation, ChannelId channel, ContactAddress contact, WakeUpMessage message, CancellationToken cancellationToken)
    {
        try
        {
            return await implementation.SendAsync(contact, message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogChannelThrew(ex, channel.Value);
            return Result.Failure<DeliveryReceipt>(ErrorKind.ChannelUnavailable, "Erreur inattendue du canal.");
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Channel '{Channel}' threw instead of returning a result.")]
    private partial void LogChannelThrew(Exception exception, string channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No notification channel is registered under '{Channel}'.")]
    private partial void LogNotRegistered(string channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Delivery on '{Channel}' failed ({Kind}): {Reason}")]
    private partial void LogDeliveryFailed(string channel, string kind, string reason);
}
