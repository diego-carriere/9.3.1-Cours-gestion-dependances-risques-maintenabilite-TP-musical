using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Abstractions;

/// <summary>
/// L'interface commune vers laquelle chaque adaptateur ramène son SDK. Échecs attendus :
/// <see cref="ErrorKind.InvalidContact"/>, <see cref="ErrorKind.ChannelUnavailable"/>,
/// <see cref="ErrorKind.Timeout"/>. Ne lève jamais pour un échec attendu.
/// </summary>
public interface INotificationChannel
{
    public Task<Result<DeliveryReceipt>> SendAsync(ContactAddress recipient, WakeUpMessage message, CancellationToken cancellationToken);
}
