using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Infrastructure.Tests.Music;

public sealed class CachedMusicCatalogTests : IDisposable
{
    private static readonly Track Sun = new("Soleil", "GIMS");
    private static readonly TrackRequest Soleil = TrackRequest.Create("soleil").Value;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 8, 6, 0, 0, TimeSpan.Zero));
    private readonly MusicCacheOptions _options = new() { Freshness = TimeSpan.FromHours(24), StaleRetention = TimeSpan.FromDays(7) };

    [Fact]
    public async Task A_fresh_entry_spares_the_provider_a_second_call()
    {
        var inner = new FakeMusicCatalog().Returns("soleil", Sun);
        var sut = Wrap(inner);

        await sut.SearchAsync(Soleil, TestContext.Current.CancellationToken);
        var second = await sut.SearchAsync(TrackRequest.Create("SOLEIL").Value, TestContext.Current.CancellationToken);

        Assert.Equal([Sun], second.Value);
        Assert.Single(inner.Searches);
    }

    [Fact]
    public async Task An_empty_answer_is_cached_too()
    {
        var inner = new FakeMusicCatalog();
        var sut = Wrap(inner);

        await sut.SearchAsync(Soleil, TestContext.Current.CancellationToken);
        await sut.SearchAsync(Soleil, TestContext.Current.CancellationToken);

        Assert.Single(inner.Searches);
    }

    [Fact]
    public async Task A_stale_entry_is_refreshed()
    {
        var inner = new FakeMusicCatalog().Returns("soleil", Sun);
        var sut = Wrap(inner);

        await sut.SearchAsync(Soleil, TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromHours(25));
        await sut.SearchAsync(Soleil, TestContext.Current.CancellationToken);

        Assert.Equal(2, inner.Searches.Count);
    }

    [Fact]
    public async Task A_stale_entry_is_served_when_the_provider_fails()
    {
        var inner = new FakeMusicCatalog().Returns("soleil", Sun);
        var sut = Wrap(inner);
        await sut.SearchAsync(Soleil, TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromHours(25));
        inner.IsDown();
        var result = await sut.SearchAsync(Soleil, TestContext.Current.CancellationToken);

        Assert.Equal([Sun], result.Value);
    }

    [Fact]
    public async Task Without_any_entry_a_provider_failure_is_passed_through()
    {
        var result = await Wrap(new FakeMusicCatalog().IsDown(ErrorKind.Timeout)).SearchAsync(Soleil, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.Timeout, result.Error.Kind);
    }

    [Fact]
    public async Task Entries_are_scoped_by_provider()
    {
        var itunes = new FakeMusicCatalog().Returns("soleil", Sun);
        var musicBrainz = new FakeMusicCatalog();

        await Wrap(itunes, "itunes").SearchAsync(Soleil, TestContext.Current.CancellationToken);
        var other = await Wrap(musicBrainz, "musicbrainz").SearchAsync(Soleil, TestContext.Current.CancellationToken);

        Assert.Empty(other.Value);
        Assert.Single(musicBrainz.Searches);
    }

    public void Dispose() => _cache.Dispose();

    private CachedMusicCatalog Wrap(IMusicCatalog inner, string provider = "itunes") => new(
        inner, provider, _cache, _clock, Microsoft.Extensions.Options.Options.Create(_options), NullLogger<CachedMusicCatalog>.Instance);
}

public sealed class CachedMusicCatalogContractTests : MusicCatalogContractTests, IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public void Dispose() => _cache.Dispose();

    protected override IMusicCatalog CreateSutWhoseProviderIsDown() => Wrap(new FakeMusicCatalog().IsDown());

    protected override IMusicCatalog CreateSutWithNoMatch() => Wrap(new FakeMusicCatalog());

    protected override IMusicCatalog CreateSutWithAMatch() => Wrap(new FakeMusicCatalog().Returns(SampleRequest, new Track("Soleil", "GIMS")));

    private CachedMusicCatalog Wrap(IMusicCatalog inner) => new(
        inner, "p", _cache, new FakeClock(DateTimeOffset.UnixEpoch),
        Microsoft.Extensions.Options.Options.Create(new MusicCacheOptions()), NullLogger<CachedMusicCatalog>.Instance);
}
