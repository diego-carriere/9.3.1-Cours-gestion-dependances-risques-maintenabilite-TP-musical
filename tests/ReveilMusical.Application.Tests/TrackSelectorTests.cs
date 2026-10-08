using Microsoft.Extensions.Logging.Abstractions;
using ReveilMusical.Application.Music;
using ReveilMusical.Application.Options;
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
    public async Task Picks_a_random_track_among_the_results_of_a_random_keyword()
    {
        _catalog.Returns("rain", A, B, C);
        var random = new FakeRandom(1, 2); // mot-clé n°1 ("rain"), puis morceau n°2 (C)
        var profile = new ProfileBuilder().ForWeather(WeatherCondition.Rainy, "pluie", "rain").Build();

        var choice = await CreateSut(random).SelectAsync(profile, DayOfWeek.Tuesday, WeatherCondition.Rainy, CancellationToken.None);

        Assert.Equal(new TrackChoice(C, TrackSource.Catalog, PreferenceLevel.Weather, Keyword.Create("rain").Value), choice);
        Assert.Equal([2, 3], random.Requests);
    }

    [Fact]
    public async Task The_day_override_provides_the_keywords()
    {
        _catalog.Returns("monday", A);
        var profile = new ProfileBuilder()
            .ForWeather(WeatherCondition.Rainy, "pluie")
            .ForDay(DayOfWeek.Monday, WeatherCondition.Rainy, "monday")
            .Build();

        var choice = await CreateSut().SelectAsync(profile, DayOfWeek.Monday, WeatherCondition.Rainy, CancellationToken.None);

        Assert.Equal(A, choice.Track);
        Assert.Equal(PreferenceLevel.DayAndWeather, choice.Level);
    }

    [Fact]
    public async Task An_empty_search_draws_another_keyword_of_the_same_set_without_repeating_it()
    {
        _catalog.Returns("rain", B);
        var profile = new ProfileBuilder().ForWeather(WeatherCondition.Rainy, "pluie", "rain").Build();

        var choice = await CreateSut(new FakeRandom(0, 0, 0)).SelectAsync(profile, DayOfWeek.Tuesday, WeatherCondition.Rainy, CancellationToken.None);

        Assert.Equal(B, choice.Track);
        Assert.Equal(["pluie", "rain"], _catalog.Searches.Select(k => k.Value));
    }

    [Fact]
    public async Task An_exhausted_set_moves_on_to_the_fallback_keywords()
    {
        _catalog.Returns("wake up", C);
        var profile = new ProfileBuilder().ForWeather(WeatherCondition.Rainy, "pluie").WithFallback("wake up").Build();

        var choice = await CreateSut().SelectAsync(profile, DayOfWeek.Tuesday, WeatherCondition.Rainy, CancellationToken.None);

        Assert.Equal(new TrackChoice(C, TrackSource.Catalog, PreferenceLevel.Fallback, Keyword.Create("wake up").Value), choice);
    }

    [Fact]
    public async Task An_uncovered_weather_searches_the_fallback_keywords_only_once()
    {
        var profile = new ProfileBuilder().WithFallback("wake up").Build();

        var choice = await CreateSut().SelectAsync(profile, DayOfWeek.Tuesday, WeatherCondition.Snowy, CancellationToken.None);

        Assert.Single(_catalog.Searches);
        Assert.Equal(TrackSource.LocalPlaylist, choice.Source);
    }

    [Fact]
    public async Task A_provider_outage_goes_straight_to_the_local_playlist()
    {
        _catalog.IsDown();
        var profile = new ProfileBuilder().ForWeather(WeatherCondition.Rainy, "pluie", "rain").WithFallback("wake up").Build();

        var choice = await CreateSut().SelectAsync(profile, DayOfWeek.Tuesday, WeatherCondition.Rainy, CancellationToken.None);

        Assert.Single(_catalog.Searches); // pas d'acharnement sur un fournisseur en panne
        Assert.Equal(new TrackChoice(FakeFallbackPlaylist.DefaultTrack, TrackSource.LocalPlaylist, PreferenceLevel.Weather, null), choice);
        Assert.Equal([WeatherCondition.Rainy], _playlist.Picks);
    }

    [Fact]
    public async Task The_number_of_searches_is_bounded_to_protect_provider_quotas()
    {
        var profile = new ProfileBuilder().ForWeather(WeatherCondition.Rainy, "a", "b", "c").WithFallback("d").Build();

        var choice = await CreateSut(maxSearchAttempts: 2).SelectAsync(profile, DayOfWeek.Tuesday, WeatherCondition.Rainy, CancellationToken.None);

        Assert.Equal(2, _catalog.Searches.Count);
        Assert.Equal(TrackSource.LocalPlaylist, choice.Source);
    }

    [Fact]
    public async Task Nothing_found_anywhere_still_wakes_the_user_with_the_local_playlist()
    {
        var profile = new ProfileBuilder().ForWeather(WeatherCondition.Sunny, "soleil").WithFallback("wake up").Build();

        var choice = await CreateSut().SelectAsync(profile, DayOfWeek.Sunday, WeatherCondition.Sunny, CancellationToken.None);

        Assert.Equal(["soleil", "wake up"], _catalog.Searches.Select(k => k.Value));
        Assert.Equal(FakeFallbackPlaylist.DefaultTrack, choice.Track);
    }

    private TrackSelector CreateSut(FakeRandom? random = null, int maxSearchAttempts = 5) => new(
        _catalog,
        _playlist,
        random ?? new FakeRandom(),
        Microsoft.Extensions.Options.Options.Create(new WakeUpOptions { MaxSearchAttempts = maxSearchAttempts }),
        NullLogger<TrackSelector>.Instance);
}
