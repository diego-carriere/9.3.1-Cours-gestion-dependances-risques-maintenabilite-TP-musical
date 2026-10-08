using ReveilMusical.FakeVendors.Sms;

namespace ReveilMusical.FakeVendors.Tests;

public sealed class SmsGatewayClientTests : IDisposable
{
    private readonly TempOutbox _outbox = new();

    [Fact]
    public async Task An_accepted_message_is_written_to_the_outbox()
    {
        var response = await Client().SubmitAsync("+33612345678", "Bonjour", TestContext.Current.CancellationToken);

        Assert.Equal(SmsStatusCodes.Accepted, response.StatusCode);
        Assert.NotNull(response.MessageId);
        Assert.Contains("+33612345678", _outbox.Read("sms.log"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_outage_is_a_status_code_never_an_exception()
    {
        var response = await Client(simulateOutage: true).SubmitAsync("+33612345678", "Bonjour", TestContext.Current.CancellationToken);

        Assert.Equal(SmsStatusCodes.ServiceUnavailable, response.StatusCode);
        Assert.Null(response.MessageId);
    }

    [Theory]
    [InlineData("0612345678")]
    [InlineData("+33 6 12 34 56 78")]
    [InlineData("+123")]
    public async Task A_number_not_in_E164_format_is_rejected(string msisdn)
    {
        var response = await Client().SubmitAsync(msisdn, "Bonjour", TestContext.Current.CancellationToken);

        Assert.Equal(SmsStatusCodes.InvalidNumber, response.StatusCode);
    }

    [Fact]
    public async Task A_text_longer_than_one_SMS_is_rejected()
    {
        var response = await Client().SubmitAsync("+33612345678", new string('a', SmsGatewayClient.MaxLength + 1), TestContext.Current.CancellationToken);

        Assert.Equal(SmsStatusCodes.MessageTooLong, response.StatusCode);
    }

    public void Dispose() => _outbox.Dispose();

    private SmsGatewayClient Client(bool simulateOutage = false) =>
        new(new SmsGatewaySettings { OutboxDirectory = _outbox.Path, WriteToConsole = false, SimulateOutage = simulateOutage });
}
