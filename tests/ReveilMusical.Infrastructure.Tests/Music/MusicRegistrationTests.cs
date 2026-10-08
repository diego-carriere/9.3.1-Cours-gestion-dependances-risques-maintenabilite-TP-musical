using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Infrastructure.Tests.Music;

/// <summary>
/// La composition réelle (client typé, pipeline Polly, cache, failover), seul le transport HTTP
/// est simulé : c'est elle qui protège les quotas des fournisseurs.
/// </summary>
public sealed class MusicRegistrationTests : IDisposable
{
    private const string ValidUserAgent = "ReveilMusical/1.0 ( https://example.test/contact )";

    private readonly StubHttpMessageHandler _itunes = new();
    private readonly StubHttpMessageHandler _musicBrainz = new();

    [Fact]
    public async Task The_port_is_the_failover_over_cached_providers()
    {
        _itunes.Enqueue(Fixture.Json(Fixture.Read("itunes-search-soleil.json"), "text/javascript"));
        using var provider = BuildProvider();

        var catalog = provider.GetRequiredService<IMusicCatalog>();
        var first = await catalog.SearchAsync(TrackRequest.Create("soleil").Value, TestContext.Current.CancellationToken);
        var second = await catalog.SearchAsync(TrackRequest.Create("Soleil").Value, TestContext.Current.CancellationToken);

        Assert.IsType<FailoverMusicCatalog>(catalog);
        Assert.Equal(3, first.Value.Count);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(1, _itunes.CallCount); // le cache a épargné le quota iTunes
    }

    [Fact]
    public async Task An_itunes_outage_fails_over_to_musicbrainz()
    {
        _itunes.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        _itunes.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        _musicBrainz.Enqueue(Fixture.Json(Fixture.Read("musicbrainz-recording-soleil.json")));
        using var provider = BuildProvider();

        var result = await provider.GetRequiredService<IMusicCatalog>().SearchAsync(TrackRequest.Create("soleil").Value, TestContext.Current.CancellationToken);

        Assert.Equal("Ilya", result.Value[1].Artist);
        Assert.Equal(2, _itunes.CallCount); // une tentative + un réessai sur 5xx
    }

    [Fact]
    public async Task A_4xx_is_not_retried()
    {
        _itunes.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest));
        using var provider = BuildProvider(("Music:Providers:1", null));

        var result = await provider.GetRequiredService<IMusicCatalog>().SearchAsync(TrackRequest.Create("soleil").Value, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(1, _itunes.CallCount);
    }

    [Fact]
    public async Task The_itunes_rate_limit_rejects_without_calling_the_provider()
    {
        _itunes.Enqueue(Fixture.Json("""{"resultCount":0,"results":[]}"""));
        using var provider = BuildProvider(("Music:ITunes:RequestsPerMinute", "1"), ("Music:Providers:1", null));
        var itunes = provider.GetRequiredKeyedService<IMusicCatalog>("itunes");

        await itunes.SearchAsync(TrackRequest.Create("a").Value, TestContext.Current.CancellationToken);
        var rejected = await itunes.SearchAsync(TrackRequest.Create("b").Value, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.ProviderUnavailable, rejected.Error.Kind);
        Assert.Contains("quota", rejected.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, _itunes.CallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("curl/8.0")]
    public void The_app_refuses_to_start_without_an_identifiable_musicbrainz_user_agent(string? userAgent)
    {
        using var provider = BuildProvider(("Music:MusicBrainz:UserAgent", userAgent));

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MusicBrainzOptions>>().Value);
    }

    [Theory]
    [InlineData("spotify")]
    [InlineData("")]
    public void The_app_refuses_to_start_with_an_unknown_provider(string key)
    {
        using var provider = BuildProvider(("Music:Providers:0", key));

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MusicProvidersOptions>>().Value);
    }

    [Fact]
    public void The_fallback_playlist_is_registered()
    {
        using var provider = BuildProvider();

        Assert.IsType<BuiltInFallbackPlaylist>(provider.GetRequiredService<IFallbackPlaylist>());
    }

    public void Dispose()
    {
        _itunes.Dispose();
        _musicBrainz.Dispose();
    }

    private ServiceProvider BuildProvider(params (string Key, string? Value)[] overrides)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Music:Providers:0"] = "itunes",
            ["Music:Providers:1"] = "musicbrainz",
            ["Music:MusicBrainz:UserAgent"] = ValidUserAgent,
            ["Resilience:Music:ITunes:RetryBaseDelay"] = "00:00:00",
            ["Resilience:Music:MusicBrainz:RetryBaseDelay"] = "00:00:00",
        };
        foreach (var (key, value) in overrides)
        {
            if (value is null)
            {
                settings.Remove(key);
            }
            else
            {
                settings[key] = value;
            }
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton<IClock>(new FakeClock(DateTimeOffset.UnixEpoch));
        services.AddSingleton<IRandom>(new FakeRandom());
        services.AddMusic(configuration);
        services.AddHttpClient<ITunesCatalog>().ConfigurePrimaryHttpMessageHandler(() => _itunes);
        services.AddHttpClient<MusicBrainzCatalog>().ConfigurePrimaryHttpMessageHandler(() => _musicBrainz);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
