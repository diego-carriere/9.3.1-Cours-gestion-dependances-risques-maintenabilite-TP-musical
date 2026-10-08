using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Infrastructure.Notifications;

/// <summary>
/// Decorator : ajoute timeout, réessai et disjoncteur à n'importe quel canal, sans toucher à son
/// adaptateur. Les exceptions de Polly, comme celles qu'un adaptateur laisse passer malgré son
/// contrat, redeviennent des <see cref="Result{T}"/> : le contrat de
/// <see cref="INotificationChannel"/> reste intact pour le dispatcher. Seule l'annulation demandée
/// par l'appelant se propage.
/// </summary>
internal sealed partial class ResilientNotificationChannel : INotificationChannel
{
    private readonly ResiliencePipeline<Result<DeliveryReceipt>> _pipeline;
    private readonly ChannelId _channel;
    private readonly ILogger<ResilientNotificationChannel> _logger;

    public ResilientNotificationChannel(
        INotificationChannel inner,
        ResiliencePipeline<Result<DeliveryReceipt>> pipeline,
        ChannelId channel,
        ILogger<ResilientNotificationChannel> logger)
    {
        Inner = inner;
        _pipeline = pipeline;
        _channel = channel;
        _logger = logger;
    }

    /// <summary>Le canal décoré (exposé pour les tests de composition).</summary>
    public INotificationChannel Inner { get; }

    public async Task<Result<DeliveryReceipt>> SendAsync(ContactAddress recipient, WakeUpMessage message, CancellationToken cancellationToken)
    {
        try
        {
            // WaitAsync : le délai par tentative tient même face à un adaptateur qui ignore le jeton,
            // car le timeout de Polly est coopératif. L'envoi abandonné peut encore aboutir plus
            // tard : livraison « au moins une fois » (README, « Simplifications assumées »).
            return await _pipeline
                .ExecuteAsync(
                    async token => await Inner.SendAsync(recipient, message, token).WaitAsync(token).ConfigureAwait(false),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (BrokenCircuitException)
        {
            LogCircuitOpen(_channel.Value);
            return Result.Failure<DeliveryReceipt>(ErrorKind.ChannelUnavailable, $"Canal '{_channel}' : circuit ouvert, envoi sauté.");
        }
        catch (Exception ex) when (ex is TimeoutRejectedException
                                   || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            LogTimeout(_channel.Value);
            return Result.Failure<DeliveryReceipt>(ErrorKind.Timeout, $"Canal '{_channel}' : délai dépassé.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Panne imprévue (disque plein, bug d'adaptateur) : un échec de ce canal, jamais la fin
            // du réveil. Le message de l'exception reste dans le journal, hors de la réponse : il
            // peut contenir une coordonnée.
            LogUnexpectedFailure(ex, _channel.Value);
            return Result.Failure<DeliveryReceipt>(
                ErrorKind.ChannelUnavailable, $"Canal '{_channel}' : erreur inattendue ({ex.GetType().Name}).");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Channel '{Channel}' circuit is open; skipping.")]
    private partial void LogCircuitOpen(string channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Channel '{Channel}' timed out.")]
    private partial void LogTimeout(string channel);

    [LoggerMessage(Level = LogLevel.Error, Message = "Channel '{Channel}' failed unexpectedly.")]
    private partial void LogUnexpectedFailure(Exception exception, string channel);
}
