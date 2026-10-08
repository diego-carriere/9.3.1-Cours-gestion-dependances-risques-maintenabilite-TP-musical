using Microsoft.Extensions.Logging.Abstractions;
using ReveilMusical.Application.Music;
using ReveilMusical.Domain.Model;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Application.Tests;

public sealed class TrackSelectorTests
{
    private static readonly Track A = new("A", "Artiste A");
    private static readonly Track B = new("B", "Artiste B");
    private static readonly Track C = new("C", "Artiste C");

    private readonly FakeMusicCatalog _catalog = new();
    private readonly FakeFallbackPlaylist _playlist = new();

    [Fact]
    public async Task The_users_track_for_the_weather_is_searched_and_the_first_result_is_kept()
    {
        var rain = TrackRequest.Create("Set Fire to the Rain", "Adele").Value;
        _catalog.Returns(rain, A, B, C);
        var profile = new ProfileBuilder().ForWeather(WeatherCondition.Rainy, "Set Fire to the Rain", "Adele").Build();

        var choice = await CreateSut().SelectAsync(profile, DayOfWeek.Tuesday, WeatherCondition.Rainy, CancellationToken.None);

        Assert.Equal(new TrackChoice(A, TrackSource.Catalog, PreferenceLevel.Weather, rain), choice);
        Assert.Equal([rain], _catalog.Searches);
    }

    [Fact]
    public async Task The_day_override_provides_the_track()
    {
        _catalog.Returns("Manic Monday", A).Returns("Set Fire to the Rain", B);
        var profile = new ProfileBuilder()
            .ForWeather(WeatherCondition.Rainy, "Set Fire to the Rain")
            .ForDay(DayOfWeek.Monday, WeatherCondition.Rainy, "Manic Monday")
            .Build();

        var choice = await CreateSut().SelectAsync(profile, DayOfWeek.Monday, WeatherCondition.Rainy, CancellationToken.None);

        Assert.Equal(A, choice.Track);
        Assert.Equal(PreferenceLevel.DayAndWeather, choice.Level);
    }

    [Fact]
    public async Task A_track_the_provider_does_not_find_moves_on_to_the_next_preference_level()
    {
        _catalog.Returns("Set Fire to the Rain", B);
        var profile = new ProfileBuilder()
            .ForWeather(WeatherCondition.Rainy, "Set Fire to the Rain")
            .ForDay(DayOfWeek.Monday, WeatherCondition.Rainy, "Introuvable")
            .Build();

        var choice = await CreateSut().SelectAsync(profile, DayOfWeek.Monday, WeatherCondition.Rainy, CancellationToken.None);

        Assert.Equal(B, choice.Track);
        Assert.Equal(PreferenceLevel.Weather, choice.Level);
        Assert.Equal(["Introuvable", "Set Fire to the Rain"], _catalog.Searches.Select(r => r.Title));
    }

    [Fact]
    public async Task A_weather_track_that_is_not_found_moves_on_to_the_fallback_track()
    {
        _catalog.Returns("Wake Me Up", C);
        var profile = new ProfileBuilder().ForWeather(WeatherCondition.Rainy, "Introuvable").WithFallback("Wake Me Up").Build();

        var choice = await CreateSut().SelectAsync(profile, DayOfWeek.Tuesday, WeatherCondition.Rainy, CancellationToken.None);

        Assert.Equal(
            new TrackChoice(C, TrackSource.Catalog, PreferenceLevel.Fallback, TrackRequest.Create("Wake Me Up").Value),
            choice);
    }

    [Fact]
    public async Task An_uncovered_weather_searches_the_fallback_track_only_once()
    {
        var profile = new ProfileBuilder().WithFallback("Wake Me Up").Build();

        var choice = await CreateSut().SelectAsync(profile, DayOfWeek.Tuesday, WeatherCondition.Snowy, CancellationToken.None);

        Assert.Single(_catalog.Searches);
        Assert.Equal(TrackSource.LocalPlaylist, choice.Source);
        Assert.Equal(PreferenceLevel.Fallback, choice.Level);
    }

    [Fact]
    public async Task A_provider_outage_goes_straight_to_the_local_playlist()
    {
        _catalog.IsDown();
        var profile = new ProfileBuilder()
            .ForWeather(WeatherCondition.Rainy, "Set Fire to the Rain")
            .WithFallback("Wake Me Up")
            .Build();

        var choice = await CreateSut().SelectAsync(profile, DayOfWeek.Tuesday, WeatherCondition.Rainy, CancellationToken.None);

        Assert.Single(_catalog.Searches); // pas d'acharnement sur un fournisseur en panne
        Assert.Equal(new TrackChoice(FakeFallbackPlaylist.DefaultTrack, TrackSource.LocalPlaylist, PreferenceLevel.Weather, null), choice);
        Assert.Equal([WeatherCondition.Rainy], _playlist.Picks);
    }

    [Fact]
    public async Task Nothing_found_anywhere_still_wakes_the_user_with_the_local_playlist_after_at_most_three_searches()
    {
        var profile = new ProfileBuilder()
            .ForWeather(WeatherCondition.Sunny, "Introuvable 1")
            .ForDay(DayOfWeek.Sunday, WeatherCondition.Sunny, "Introuvable 2")
            .WithFallback("Introuvable 3")
            .Build();

        var choice = await CreateSut().SelectAsync(profile, DayOfWeek.Sunday, WeatherCondition.Sunny, CancellationToken.None);

        Assert.Equal(["Introuvable 2", "Introuvable 1", "Introuvable 3"], _catalog.Searches.Select(r => r.Title));
        Assert.Equal(new TrackChoice(FakeFallbackPlaylist.DefaultTrack, TrackSource.LocalPlaylist, PreferenceLevel.DayAndWeather, null), choice);
    }

    [Fact]
    public async Task A_catalog_that_throws_still_wakes_the_user_with_the_local_playlist()
    {
        _catalog.Throws(new InvalidOperationException("bug d'adaptateur"));
        var profile = new ProfileBuilder().ForWeather(WeatherCondition.Rainy, "Set Fire to the Rain").Build();

        var choice = await CreateSut().SelectAsync(profile, DayOfWeek.Tuesday, WeatherCondition.Rainy, CancellationToken.None);

        Assert.Equal(new TrackChoice(FakeFallbackPlaylist.DefaultTrack, TrackSource.LocalPlaylist, PreferenceLevel.Weather, null), choice);
    }

    [Fact]
    public async Task A_cancellation_requested_by_the_caller_is_not_swallowed()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _catalog.Throws(new OperationCanceledException(cancellation.Token));
        var profile = new ProfileBuilder().Build();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateSut().SelectAsync(profile, DayOfWeek.Tuesday, WeatherCondition.Rainy, cancellation.Token));
    }

    private TrackSelector CreateSut() => new(_catalog, _playlist, NullLogger<TrackSelector>.Instance);
}
