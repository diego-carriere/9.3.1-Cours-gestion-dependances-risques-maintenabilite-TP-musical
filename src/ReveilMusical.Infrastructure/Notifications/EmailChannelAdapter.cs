using System.Net;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.FakeVendors.Mail;

namespace ReveilMusical.Infrastructure.Notifications;

/// <summary>
/// Adapter : ramène le SDK mail (synchrone, exceptions) à <see cref="INotificationChannel"/>
/// (asynchrone, <see cref="Result{T}"/>). L'appel bloquant part sur le pool de threads, et l'attente
/// respecte le jeton : <c>Task.Run</c> seul n'annule qu'avant le démarrage.
/// </summary>
internal sealed class EmailChannelAdapter : INotificationChannel
{
    private readonly SmtpMailClient _client;

    public EmailChannelAdapter(SmtpMailClient client) => _client = client;

    public async Task<Result<DeliveryReceipt>> SendAsync(ContactAddress recipient, WakeUpMessage message, CancellationToken cancellationToken)
    {
        var envelope = new MailEnvelope(
            _client.Settings.FromAddress,
            recipient.Value,
            $"{message.Title} : {message.Track.Title}",
            $"<p>{WebUtility.HtmlEncode(message.Body)}</p>");

        try
        {
            var messageId = await Task.Run(() => _client.Send(envelope), cancellationToken)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return Result.Success(new DeliveryReceipt(messageId));
        }
        // Le type d'échec du SDK, pas son message : celui-ci répète l'adresse, et le texte d'erreur
        // part dans les journaux, l'alerte et la réponse HTTP.
        catch (MailDeliveryException ex) when (ex.Failure == MailDeliveryFailure.InvalidRecipient)
        {
            return Result.Failure<DeliveryReceipt>(ErrorKind.InvalidContact, "Adresse email refusée par le serveur mail.");
        }
        catch (MailDeliveryException ex)
        {
            return Result.Failure<DeliveryReceipt>(ErrorKind.ChannelUnavailable, $"Serveur mail indisponible ({ex.Failure}).");
        }
    }
}
