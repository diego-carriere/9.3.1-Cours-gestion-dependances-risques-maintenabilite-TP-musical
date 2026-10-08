using Microsoft.Extensions.Logging.Abstractions;
using ReveilMusical.Application.Notifications;
using ReveilMusical.Application.Options;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Application.Tests;

public sealed class NotificationDispatcherTests
{
    private static readonly WakeUpMessage Message =
        WakeUpMessage.Compose("Alice", new Track("A", "Artiste"), DayOfWeek.Monday, WeatherCondition.Sunny);

    private readonly FakeNotificationChannel _push = new();
    private readonly FakeNotificationChannel _sms = new();
    private readonly FakeNotificationChannel _email = new();
    private readonly RecordingOperatorAlerter _alerter = new();

    [Fact]
    public async Task The_preferred_channel_is_used_when_it_works()
    {
        var profile = new ProfileBuilder().Prefers("sms").WithContact("sms", "+33612345678").Build();

        var outcome = await CreateSut().DispatchAsync(profile, Message, CancellationToken.None);

        Assert.Equal(Channel("sms"), outcome.DeliveredOn);
        Assert.Equal([new DeliveryAttempt(Channel("sms"), DeliveryStatus.Delivered, "fake-1")], outcome.Attempts);
        Assert.Equal(("+33612345678", Message), (_sms.Sent.Single().Recipient.Value, _sms.Sent.Single().Message));
        Assert.Empty(_alerter.Alerts);
    }

    [Fact]
    public async Task A_failing_preferred_channel_cascades_to_the_next_channel_with_a_contact()
    {
        _push.FailsWith(ErrorKind.ChannelUnavailable);
        var profile = new ProfileBuilder()
            .Prefers("push")
            .WithContact("push", "device-token")
            .WithContact("email", "alice@example.com")
            .Build();

        var outcome = await CreateSut().DispatchAsync(profile, Message, CancellationToken.None);

        Assert.Equal(Channel("email"), outcome.DeliveredOn);
        Assert.Equal(
            [DeliveryStatus.Failed, DeliveryStatus.NoContact, DeliveryStatus.Delivered],
            outcome.Attempts.Select(a => a.Status));
        Assert.Equal(["push", "sms", "email"], outcome.Attempts.Select(a => a.Channel.Value));
        Assert.Equal(0, _sms.Attempts);
    }

    [Fact]
    public async Task The_preferred_channel_is_never_tried_twice_even_if_it_is_also_a_fallback()
    {
        _sms.FailsWith(ErrorKind.ChannelUnavailable);
        var profile = new ProfileBuilder().Prefers("sms").WithContact("sms", "+33612345678").Build();

        await CreateSut().DispatchAsync(profile, Message, CancellationToken.None);

        Assert.Equal(1, _sms.Attempts);
    }

    [Fact]
    public async Task A_preferred_channel_without_contact_is_skipped()
    {
        var profile = new ProfileBuilder().Prefers("sms").WithContact("email", "alice@example.com").Build();

        var outcome = await CreateSut().DispatchAsync(profile, Message, CancellationToken.None);

        Assert.Equal(DeliveryStatus.NoContact, outcome.Attempts[0].Status);
        Assert.Equal(Channel("email"), outcome.DeliveredOn);
    }

    [Fact]
    public async Task A_channel_without_registered_implementation_is_skipped()
    {
        var profile = new ProfileBuilder()
            .Prefers("whatsapp")
            .WithContact("whatsapp", "+33612345678")
            .WithContact("sms", "+33612345678")
            .Build();

        var outcome = await CreateSut().DispatchAsync(profile, Message, CancellationToken.None);

        Assert.Equal(DeliveryStatus.NotRegistered, outcome.Attempts[0].Status);
        Assert.Equal(Channel("sms"), outcome.DeliveredOn);
    }

    [Fact]
    public async Task When_every_channel_fails_the_operator_is_alerted()
    {
        _push.FailsWith(ErrorKind.ChannelUnavailable);
        _sms.FailsWith(ErrorKind.Timeout);
        _email.FailsWith(ErrorKind.InvalidContact);
        var profile = new ProfileBuilder("7")
            .Prefers("push")
            .WithContact("push", "t")
            .WithContact("sms", "+33612345678")
            .WithContact("email", "x")
            .Build();

        var outcome = await CreateSut().DispatchAsync(profile, Message, CancellationToken.None);

        Assert.Null(outcome.DeliveredOn);
        Assert.True(outcome.OperatorAlerted);
        var alert = Assert.Single(_alerter.Alerts);
        Assert.Equal("7", alert.UserId.Value);
        Assert.Contains("push", alert.Summary, StringComparison.Ordinal);
        Assert.Equal("Échec scripté (Timeout).", outcome.Attempts[1].Detail);
    }

    [Fact]
    public async Task A_channel_that_throws_counts_as_a_failure_and_the_cascade_goes_on()
    {
        _push.Throws(new IOException("disk full"));
        var profile = new ProfileBuilder().Prefers("push").WithContact("push", "t").WithContact("sms", "+33612345678").Build();

        var outcome = await CreateSut().DispatchAsync(profile, Message, CancellationToken.None);

        Assert.Equal(Channel("sms"), outcome.DeliveredOn);
        Assert.Equal(new DeliveryAttempt(Channel("push"), DeliveryStatus.Failed, "Erreur inattendue du canal."), outcome.Attempts[0]);
        Assert.Empty(_alerter.Alerts);
    }

    [Fact]
    public async Task When_every_channel_throws_the_operator_is_still_alerted()
    {
        _push.Throws(new InvalidOperationException("bug"));
        _sms.Throws(new IOException("disk full"));
        var profile = new ProfileBuilder().Prefers("push").WithContact("push", "t").WithContact("sms", "+33612345678").Build();

        var outcome = await CreateSut().DispatchAsync(profile, Message, CancellationToken.None);

        Assert.Null(outcome.DeliveredOn);
        Assert.True(outcome.OperatorAlerted);
        Assert.Single(_alerter.Alerts);
    }

    [Fact]
    public async Task A_cancellation_requested_by_the_caller_is_not_swallowed()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _sms.Throws(new OperationCanceledException(cancellation.Token));
        var profile = new ProfileBuilder().Prefers("sms").WithContact("sms", "+33612345678").Build();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSut().DispatchAsync(profile, Message, cancellation.Token));
    }

    [Fact]
    public async Task Without_fallback_channels_only_the_preferred_one_is_tried()
    {
        _sms.FailsWith(ErrorKind.ChannelUnavailable);
        var profile = new ProfileBuilder().Prefers("sms").WithContact("sms", "+33612345678").WithContact("email", "a@b.fr").Build();

        var outcome = await CreateSut(fallbackChannels: []).DispatchAsync(profile, Message, CancellationToken.None);

        Assert.Single(outcome.Attempts);
        Assert.True(outcome.OperatorAlerted);
    }

    private NotificationDispatcher CreateSut(string[]? fallbackChannels = null) => new(
        new FakeNotificationChannelResolver().With("push", _push).With("sms", _sms).With("email", _email),
        _alerter,
        Microsoft.Extensions.Options.Options.Create(new WakeUpOptions { FallbackChannels = fallbackChannels ?? ["push", "sms", "email"] }),
        NullLogger<NotificationDispatcher>.Instance);

    private static ChannelId Channel(string id) => ChannelId.Create(id).Value;
}
