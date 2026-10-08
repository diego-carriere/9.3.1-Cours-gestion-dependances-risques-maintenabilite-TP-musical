using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.FakeVendors.Push;
using ReveilMusical.Infrastructure.Notifications;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Infrastructure.Tests.Notifications;

public sealed class PushChannelAdapterTests : NotificationChannelContractTests, IDisposable
{
    private readonly TempDirectory _outbox = new();

    protected override ContactAddress ValidContact => ContactAddress.Create("device-token-1234").Value;

    protected override ContactAddress InvalidContact => ContactAddress.Create("bad").Value;

    [Fact]
    public async Task The_payload_carries_title_body_and_track()
    {
        var service = new CapturingPushService();

        var result = await new PushChannelAdapter(service).SendAsync(ValidContact, SampleMessage, TestContext.Current.CancellationToken);

        Assert.Equal("ticket-1", result.Value.Reference);
        Assert.Equal("device-token-1234", service.Last!.DeviceToken);
        Assert.Equal(SampleMessage.Title, service.Last.Payload["title"]);
        Assert.Equal(SampleMessage.Body, service.Last.Payload["body"]);
        Assert.Equal("Here Comes the Sun — The Beatles", service.Last.Payload["track"]);
    }

    [Fact]
    public async Task A_caller_cancellation_while_waiting_for_the_callback_propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var pending = new PushChannelAdapter(new SilentPushService()).SendAsync(ValidContact, SampleMessage, cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Theory]
    [InlineData(PushDeliveryState.Rejected)]
    [InlineData(PushDeliveryState.ServiceDown)]
    public async Task A_vendor_reason_that_repeats_the_token_never_reaches_the_error_message(PushDeliveryState state)
    {
        // Un vrai SDK peut citer le jeton dans son texte libre : l'adaptateur ne le recopie pas.
        var service = new ReportingPushService(new PushDeliveryReport(null, state, $"token {ValidContact.Value} is gone"));

        var result = await new PushChannelAdapter(service).SendAsync(ValidContact, SampleMessage, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(ValidContact.Value, result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _outbox.Dispose();

    protected override INotificationChannel CreateWorkingSut() => Create(simulateOutage: false);

    protected override INotificationChannel CreateSutWhoseServiceIsDown() => Create(simulateOutage: true);

    private PushChannelAdapter Create(bool simulateOutage) => new(new PushService(new PushServiceSettings
    {
        OutboxDirectory = _outbox.Path,
        WriteToConsole = false,
        SimulateOutage = simulateOutage,
    }));

    private sealed class CapturingPushService : IPushService
    {
        public PushRequest? Last { get; private set; }

        public void Deliver(PushRequest request, Action<PushDeliveryReport> onCompleted)
        {
            Last = request;
            onCompleted(new PushDeliveryReport("ticket-1", PushDeliveryState.Delivered, null));
        }
    }

    private sealed class ReportingPushService(PushDeliveryReport report) : IPushService
    {
        public void Deliver(PushRequest request, Action<PushDeliveryReport> onCompleted) => onCompleted(report);
    }

    /// <summary>Un service qui ne rappelle jamais : seul un délai ou une annulation y met fin.</summary>
    private sealed class SilentPushService : IPushService
    {
        public void Deliver(PushRequest request, Action<PushDeliveryReport> onCompleted)
        {
        }
    }
}
