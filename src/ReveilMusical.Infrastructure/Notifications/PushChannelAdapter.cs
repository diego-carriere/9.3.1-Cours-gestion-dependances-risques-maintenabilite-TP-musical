using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.FakeVendors.Push;

namespace ReveilMusical.Infrastructure.Notifications;

/// <summary>
/// Adapter : ramène le SDK push (callback sur un autre thread) à une <see cref="Task"/>, via une
/// <see cref="TaskCompletionSource{TResult}"/>. Une annulation de l'appelant est propagée ; le
/// délai maximal d'attente est l'affaire du décorateur de résilience.
/// </summary>
internal sealed class PushChannelAdapter : INotificationChannel
{
    private readonly IPushService _service;

    public PushChannelAdapter(IPushService service) => _service = service;

    public async Task<Result<DeliveryReceipt>> SendAsync(ContactAddress recipient, WakeUpMessage message, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<PushDeliveryReport>(TaskCreationOptions.RunContinuationsAsynchronously);
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = message.Title,
            ["body"] = message.Body,
            ["track"] = $"{message.Track.Title} — {message.Track.Artist}",
        };

        _service.Deliver(new PushRequest(recipient.Value, payload), report => completion.TrySetResult(report));

        var delivered = await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        return delivered.State switch
        {
            PushDeliveryState.Delivered when delivered.TicketId is not null => Result.Success(new DeliveryReceipt(delivered.TicketId)),
            PushDeliveryState.Rejected => Result.Failure<DeliveryReceipt>(
                ErrorKind.InvalidContact, $"Jeton d'appareil refusé : {delivered.Reason}"),
            _ => Result.Failure<DeliveryReceipt>(ErrorKind.ChannelUnavailable, $"Service push indisponible : {delivered.Reason}"),
        };
    }
}
