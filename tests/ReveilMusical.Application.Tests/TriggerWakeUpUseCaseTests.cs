using Microsoft.Extensions.Logging.Abstractions;
using ReveilMusical.Application.Music;
using ReveilMusical.Application.Notifications;
using ReveilMusical.Application.Options;
using ReveilMusical.Application.WakeUp;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Application.Tests;

public sealed class TriggerWakeUpUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 5, 30, 0, TimeSpan.Zero);
    private static readonly Track Sun = new("Here Comes the Sun", "The Beatles");

    private readonly FakeUserProfileProvider _profiles = new();
    private readonly FakeMusicCatalog _catalog = new();
    private readonly FakeFallbackPlaylist _playlist = new();
    private readonly FakeNotificationChannel _sms = new();
    private readonly FakeNotificationChannel _email = new();
    private readonly RecordingOperatorAlerter _alerter = new();

    public TriggerWakeUpUseCaseTests()
    {
        _profiles.With(new ProfileBuilder("42")
            .Named("Alice")
            .Prefers("sms")
            .WithContact("sms", "+33612345678")
            .WithContact("email", "alice@example.com")
            .ForWeather(WeatherCondition.Sunny, "soleil")
            .Build());
        _catalog.Returns("soleil", Sun);
    }

    [Fact]
    public async Task Nominal_wake_up_sends_the_chosen_track_on_the_preferred_channel()
    {
        var report = (await CreateSut().ExecuteAsync(Request("42", WeatherCondition.Sunny), CancellationToken.None)).Value;

        Assert.Equal(Sun, report.Track.Track);
        Assert.Equal(TrackSource.Catalog, report.Track.Source);
        Assert.Equal(ChannelId.Create("sms").Value, report.Dispatch.DeliveredOn);
        Assert.True(report.Delivered);
        Assert.False(report.Degraded);
        Assert.Equal(Now, report.TriggeredAtUtc);
        Assert.Contains("« Here Comes the Sun » de The Beatles", _sms.Sent.Single().Message.Body, StringComparison.Ordinal);
        Assert.StartsWith("Bonjour Alice ! Ce lundi s'annonce ensoleillé", _sms.Sent.Single().Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_user_is_reported_without_searching_or_sending()
    {
        var result = await CreateSut().ExecuteAsync(Request("inconnu", WeatherCondition.Sunny), CancellationToken.None);

        Assert.Equal(ErrorKind.UserNotFound, result.Error.Kind);
        Assert.Empty(_catalog.Searches);
        Assert.Equal(0, _sms.Attempts);
    }

    [Fact]
    public async Task A_user_service_outage_is_reported()
    {
        _profiles.IsDown();

        var result = await CreateSut().ExecuteAsync(Request("42", WeatherCondition.Sunny), CancellationToken.None);

        Assert.Equal(ErrorKind.UserServiceUnavailable, result.Error.Kind);
    }

    [Fact]
    public async Task Using_the_local_playlist_is_a_degraded_but_delivered_wake_up()
    {
        _catalog.IsDown();

        var report = (await CreateSut().ExecuteAsync(Request("42", WeatherCondition.Sunny), CancellationToken.None)).Value;

        Assert.Equal(TrackSource.LocalPlaylist, report.Track.Source);
        Assert.True(report.Delivered);
        Assert.True(report.Degraded);
    }

    [Fact]
    public async Task Delivering_on_a_fallback_channel_is_a_degraded_wake_up()
    {
        _sms.FailsWith(ErrorKind.ChannelUnavailable);

        var report = (await CreateSut().ExecuteAsync(Request("42", WeatherCondition.Sunny), CancellationToken.None)).Value;

        Assert.Equal(ChannelId.Create("email").Value, report.Dispatch.DeliveredOn);
        Assert.True(report.Degraded);
    }

    [Fact]
    public async Task An_undelivered_wake_up_is_still_a_report_and_alerts_the_operator()
    {
        _sms.FailsWith(ErrorKind.ChannelUnavailable);
        _email.FailsWith(ErrorKind.ChannelUnavailable);

        var report = (await CreateSut().ExecuteAsync(Request("42", WeatherCondition.Sunny), CancellationToken.None)).Value;

        Assert.False(report.Delivered);
        Assert.True(report.Degraded);
        Assert.Single(_alerter.Alerts);
        Assert.Equal(ChannelId.Create("sms").Value, report.PreferredChannel);
    }

    private static WakeUpRequest Request(string userId, WeatherCondition weather) =>
        new(UserId.Create(userId).Value, DayOfWeek.Monday, weather);

    private TriggerWakeUpUseCase CreateSut()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new WakeUpOptions { FallbackChannels = ["sms", "email"] });

        return new TriggerWakeUpUseCase(
            _profiles,
            new TrackSelector(_catalog, _playlist, new FakeRandom(), options, NullLogger<TrackSelector>.Instance),
            new NotificationDispatcher(
                new FakeNotificationChannelResolver().With("sms", _sms).With("email", _email),
                _alerter,
                options,
                NullLogger<NotificationDispatcher>.Instance),
            new FakeClock(Now));
    }
}
