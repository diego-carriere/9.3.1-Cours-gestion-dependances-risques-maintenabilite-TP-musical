using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Infrastructure.Tests.Music;

public sealed class FailoverMusicCatalogTests
{
    private static readonly Track FromITunes = new("Soleil", "GIMS");
    private static readonly Track FromMusicBrainz = new("Soleil Soleil", "Ilya");
    private static readonly Keyword Soleil = Keyword.Create("soleil").Value;

    private readonly FakeMusicCatalog _itunes = new FakeMusicCatalog().Returns("soleil", FromITunes);
    private readonly FakeMusicCatalog _musicBrainz = new FakeMusicCatalog().Returns("soleil", FromMusicBrainz);

    [Fact]
    public async Task The_first_provider_answers_and_the_next_is_not_called()
    {
        var result = await CreateSut("itunes", "musicbrainz").SearchAsync(Soleil, TestContext.Current.CancellationToken);

        Assert.Equal([FromITunes], result.Value);
        Assert.Empty(_musicBrainz.Searches);
    }

    [Fact]
    public async Task A_failing_provider_hands_over_to_the_next()
    {
        _itunes.IsDown();

        var result = await CreateSut("itunes", "musicbrainz").SearchAsync(Soleil, TestContext.Current.CancellationToken);

        Assert.Equal([FromMusicBrainz], result.Value);
    }

    [Fact]
    public async Task An_empty_answer_is_an_answer_not_a_failure()
    {
        var result = await CreateSut("itunes", "musicbrainz").SearchAsync(Keyword.Create("rien").Value, TestContext.Current.CancellationToken);

        Assert.Empty(result.Value);
        Assert.Empty(_musicBrainz.Searches);
    }

    [Fact]
    public async Task When_every_provider_fails_the_failure_is_reported()
    {
        _itunes.IsDown();
        _musicBrainz.IsDown(ErrorKind.Timeout);

        var result = await CreateSut("itunes", "musicbrainz").SearchAsync(Soleil, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.ProviderUnavailable, result.Error.Kind);
        Assert.Contains("musicbrainz", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_provider_key_is_skipped()
    {
        var result = await CreateSut("spotify", "musicbrainz").SearchAsync(Soleil, TestContext.Current.CancellationToken);

        Assert.Equal([FromMusicBrainz], result.Value);
    }

    [Fact]
    public async Task No_configured_provider_is_an_unavailable_catalog()
    {
        var result = await CreateSut().SearchAsync(Soleil, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.ProviderUnavailable, result.Error.Kind);
    }

    [Fact]
    public async Task The_order_is_read_at_each_call_so_a_switch_needs_no_restart()
    {
        var configuration = Configuration("itunes", "musicbrainz");
        var sut = CreateSut(configuration);

        var before = await sut.SearchAsync(Soleil, TestContext.Current.CancellationToken);
        configuration["Music:Providers:0"] = "musicbrainz";
        configuration["Music:Providers:1"] = "itunes";
        var after = await sut.SearchAsync(Soleil, TestContext.Current.CancellationToken);

        Assert.Equal([FromITunes], before.Value);
        Assert.Equal([FromMusicBrainz], after.Value);
    }

    private FailoverMusicCatalog CreateSut(params string[] providers) => CreateSut(Configuration(providers));

    private FailoverMusicCatalog CreateSut(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IMusicCatalog>("itunes", _itunes);
        services.AddKeyedSingleton<IMusicCatalog>("musicbrainz", _musicBrainz);

        return new FailoverMusicCatalog(services.BuildServiceProvider(), configuration, NullLogger<FailoverMusicCatalog>.Instance);
    }

    private static IConfigurationRoot Configuration(params string[] providers) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(providers.Select((p, i) => new KeyValuePair<string, string?>($"Music:Providers:{i}", p)))
            .Build();
}

public sealed class FailoverMusicCatalogContractTests : MusicCatalogContractTests
{
    protected override IMusicCatalog CreateSutWhoseProviderIsDown() => Create(new FakeMusicCatalog().IsDown());

    protected override IMusicCatalog CreateSutWithNoMatch() => Create(new FakeMusicCatalog());

    private static FailoverMusicCatalog Create(IMusicCatalog only)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton("itunes", only);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Music:Providers:0", "itunes")])
            .Build();

        return new FailoverMusicCatalog(services.BuildServiceProvider(), configuration, NullLogger<FailoverMusicCatalog>.Instance);
    }
}
