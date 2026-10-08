namespace ReveilMusical.FakeVendors.Push;

/// <summary>SDK push simulé : à callback, comme beaucoup de SDK mobiles.</summary>
public sealed class PushService : IPushService
{
    private const int MinTokenLength = 8;

    private readonly PushServiceSettings _settings;
    private readonly OutboxFile _outbox;

    public PushService(PushServiceSettings settings)
    {
        _settings = settings;
        _outbox = new OutboxFile(settings.OutboxDirectory, "push.log", settings.WriteToConsole);
    }

    public void Deliver(PushRequest request, Action<PushDeliveryReport> onCompleted) =>
        _ = Task.Run(() => onCompleted(Process(request)));

    private PushDeliveryReport Process(PushRequest request)
    {
        if (_settings.SimulateOutage)
        {
            return new PushDeliveryReport(null, PushDeliveryState.ServiceDown, "Push service unavailable (simulated).");
        }

        if (request.DeviceToken.Length < MinTokenLength || !request.DeviceToken.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            return new PushDeliveryReport(null, PushDeliveryState.Rejected, "Unknown or malformed device token.");
        }

        if (!request.Payload.TryGetValue("body", out var body))
        {
            return new PushDeliveryReport(null, PushDeliveryState.Rejected, "Payload has no 'body'.");
        }

        var ticketId = "push-" + Guid.NewGuid().ToString("N");
        var title = request.Payload.GetValueOrDefault("title", string.Empty);
        _outbox.Append($"[PUSH {ticketId}] device={request.DeviceToken} title=\"{title}\" body=\"{body}\"");
        return new PushDeliveryReport(ticketId, PushDeliveryState.Delivered, null);
    }
}
