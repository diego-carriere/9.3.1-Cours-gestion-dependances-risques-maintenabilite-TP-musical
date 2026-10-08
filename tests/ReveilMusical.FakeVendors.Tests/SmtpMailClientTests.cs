using ReveilMusical.FakeVendors.Mail;

namespace ReveilMusical.FakeVendors.Tests;

public sealed class SmtpMailClientTests : IDisposable
{
    private readonly TempOutbox _outbox = new();

    [Fact]
    public void Send_writes_the_envelope_to_the_outbox_and_returns_a_message_id()
    {
        var client = new SmtpMailClient(Settings());

        var messageId = client.Send(new MailEnvelope("reveil@example.com", "alice@example.com", "Réveil", "<p>Bonjour</p>"));

        Assert.StartsWith("mail-", messageId, StringComparison.Ordinal);
        var log = _outbox.Read("mail.log");
        Assert.Contains(messageId, log, StringComparison.Ordinal);
        Assert.Contains("alice@example.com", log, StringComparison.Ordinal);
        Assert.Contains("<p>Bonjour</p>", log, StringComparison.Ordinal);
    }

    [Fact]
    public void An_outage_throws_a_delivery_exception()
    {
        var client = new SmtpMailClient(Settings(simulateOutage: true));

        var exception = Assert.Throws<MailDeliveryException>(() =>
            client.Send(new MailEnvelope("reveil@example.com", "alice@example.com", "Réveil", "x")));

        Assert.Equal(MailDeliveryFailure.ServerUnavailable, exception.Failure);
        Assert.Empty(_outbox.Read("mail.log"));
    }

    [Theory]
    [InlineData("alice")]
    [InlineData("alice@")]
    [InlineData("@example.com")]
    public void A_malformed_recipient_throws_a_delivery_exception(string recipient)
    {
        var client = new SmtpMailClient(Settings());

        var exception = Assert.Throws<MailDeliveryException>(() =>
            client.Send(new MailEnvelope("reveil@example.com", recipient, "Réveil", "x")));

        Assert.Equal(MailDeliveryFailure.InvalidRecipient, exception.Failure);
    }

    [Fact]
    public void Concurrent_sends_each_land_on_their_own_line()
    {
        var client = new SmtpMailClient(Settings());

        Parallel.For(0, 50, i => client.Send(new MailEnvelope("reveil@example.com", $"user{i}@example.com", "Réveil", "x")));

        Assert.Equal(50, _outbox.Read("mail.log").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void The_exception_carries_its_message()
    {
        var exception = new MailDeliveryException(MailDeliveryFailure.ServerUnavailable, "down");

        Assert.Equal("down", exception.Message);
    }

    public void Dispose() => _outbox.Dispose();

    private SmtpMailSettings Settings(bool simulateOutage = false) =>
        new() { OutboxDirectory = _outbox.Path, WriteToConsole = false, SimulateOutage = simulateOutage };
}
