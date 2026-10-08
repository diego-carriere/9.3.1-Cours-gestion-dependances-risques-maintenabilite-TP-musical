using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.FakeVendors.Sms;
using ReveilMusical.Infrastructure.Notifications;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Infrastructure.Tests.Notifications;

public sealed class SmsChannelAdapterTests : NotificationChannelContractTests, IDisposable
{
    private readonly TempDirectory _outbox = new();

    protected override ContactAddress ValidContact => ContactAddress.Create("+33612345678").Value;

    protected override ContactAddress InvalidContact => ContactAddress.Create("06 12 34 56 78").Value;

    [Fact]
    public async Task A_body_longer_than_one_SMS_is_truncated_with_an_ellipsis()
    {
        var gateway = new RecordingGateway(SmsStatusCodes.Accepted);
        var longTrack = new Track(new string('t', 150), "Artiste");
        var message = WakeUpMessage.Compose("Alice", longTrack, DayOfWeek.Monday, WeatherCondition.Sunny);

        var result = await new SmsChannelAdapter(gateway).SendAsync(ValidContact, message, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(SmsGatewayClient.MaxLength, gateway.LastText!.Length);
        Assert.EndsWith("…", gateway.LastText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_short_body_is_sent_unchanged()
    {
        var gateway = new RecordingGateway(SmsStatusCodes.Accepted);

        await new SmsChannelAdapter(gateway).SendAsync(ValidContact, SampleMessage, TestContext.Current.CancellationToken);

        Assert.Equal(SampleMessage.Body, gateway.LastText);
    }

    [Theory]
    [InlineData(SmsStatusCodes.MessageTooLong)]
    [InlineData(SmsStatusCodes.ServiceUnavailable)]
    [InlineData(42)]
    public async Task Any_other_status_code_means_the_channel_is_unavailable(int statusCode)
    {
        var result = await new SmsChannelAdapter(new RecordingGateway(statusCode))
            .SendAsync(ValidContact, SampleMessage, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.ChannelUnavailable, result.Error.Kind);
        Assert.Contains(statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture), result.Error.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _outbox.Dispose();

    protected override INotificationChannel CreateWorkingSut() => Create(simulateOutage: false);

    protected override INotificationChannel CreateSutWhoseServiceIsDown() => Create(simulateOutage: true);

    private SmsChannelAdapter Create(bool simulateOutage) => new(new SmsGatewayClient(new SmsGatewaySettings
    {
        OutboxDirectory = _outbox.Path,
        WriteToConsole = false,
        SimulateOutage = simulateOutage,
    }));

    private sealed class RecordingGateway(int statusCode) : ISmsGatewayClient
    {
        public string? LastText { get; private set; }

        public Task<SmsSubmitResponse> SubmitAsync(string msisdn, string text, CancellationToken cancellationToken = default)
        {
            LastText = text;
            return Task.FromResult(new SmsSubmitResponse(statusCode, statusCode == SmsStatusCodes.Accepted ? "sms-1" : null));
        }
    }
}
