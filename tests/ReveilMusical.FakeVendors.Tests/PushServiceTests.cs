using ReveilMusical.FakeVendors.Push;

namespace ReveilMusical.FakeVendors.Tests;

public sealed class PushServiceTests : IDisposable
{
    private static readonly Dictionary<string, string> Payload = new() { ["title"] = "Réveil", ["body"] = "Bonjour" };

    private readonly TempOutbox _outbox = new();

    [Fact]
    public async Task A_delivered_push_reports_through_the_callback_with_a_ticket()
    {
        var report = await DeliverAsync(Service(), new PushRequest("device-token-1234", Payload));

        Assert.Equal(PushDeliveryState.Delivered, report.State);
        Assert.StartsWith("push-", report.TicketId, StringComparison.Ordinal);
        Assert.Contains("device-token-1234", _outbox.Read("push.log"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outage_is_reported_through_the_callback()
    {
        var report = await DeliverAsync(Service(simulateOutage: true), new PushRequest("device-token-1234", Payload));

        Assert.Equal(PushDeliveryState.ServiceDown, report.State);
        Assert.Null(report.TicketId);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("token with spaces")]
    public async Task A_malformed_device_token_is_rejected(string token)
    {
        var report = await DeliverAsync(Service(), new PushRequest(token, Payload));

        Assert.Equal(PushDeliveryState.Rejected, report.State);
        Assert.NotNull(report.Reason);
    }

    [Fact]
    public async Task A_payload_without_body_is_rejected()
    {
        var report = await DeliverAsync(Service(), new PushRequest("device-token-1234", new Dictionary<string, string> { ["title"] = "x" }));

        Assert.Equal(PushDeliveryState.Rejected, report.State);
    }

    public void Dispose() => _outbox.Dispose();

    private static async Task<PushDeliveryReport> DeliverAsync(PushService service, PushRequest request)
    {
        var completion = new TaskCompletionSource<PushDeliveryReport>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Deliver(request, completion.SetResult);
        return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    private PushService Service(bool simulateOutage = false) =>
        new(new PushServiceSettings { OutboxDirectory = _outbox.Path, WriteToConsole = false, SimulateOutage = simulateOutage });
}
