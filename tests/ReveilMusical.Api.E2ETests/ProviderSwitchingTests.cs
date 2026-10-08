using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ReveilMusical.Api.E2ETests;

/// <summary>Exigence « en changer rapidement » : une ligne de configuration, sans recompiler ni redémarrer.</summary>
public sealed class ProviderSwitchingTests
{
    [Fact]
    public async Task Editing_the_provider_order_switches_source_on_the_next_wake_up()
    {
        await using var factory = new ReveilApiFactory();
        factory.Upstream
            .FixtureFor(FakeUpstream.ITunesHost, "itunes-search-soleil.json")
            .FixtureFor(FakeUpstream.MusicBrainzHost, "musicbrainz-recording-soleil.json");
        var client = factory.CreateClient();

        var before = await (await client.WakeUpAsync("42", "MARDI", "SOLEIL")).JsonAsync();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        configuration["Music:Providers:0"] = "musicbrainz";
        configuration["Music:Providers:1"] = "itunes";
        var after = await (await client.WakeUpAsync("42", "MARDI", "SOLEIL")).JsonAsync();

        Assert.Equal("GIMS", before.Str("track.artist"));
        Assert.Equal("memo montañez", after.Str("track.artist"));
        Assert.Equal(1, factory.Upstream.CallCountFor(FakeUpstream.ITunesHost));
        Assert.Equal(1, factory.Upstream.CallCountFor(FakeUpstream.MusicBrainzHost));
    }

    [Fact]
    public async Task A_repeated_wake_up_is_served_from_the_cache_to_respect_the_itunes_quota()
    {
        await using var factory = new ReveilApiFactory();
        factory.Upstream.FixtureFor(FakeUpstream.ITunesHost, "itunes-search-soleil.json");
        var client = factory.CreateClient();

        await client.WakeUpAsync("42", "MARDI", "SOLEIL");
        var second = await (await client.WakeUpAsync("42", "MERCREDI", "SOLEIL")).JsonAsync();

        Assert.Equal("Soleil", second.Str("track.title"));
        Assert.Equal(1, factory.Upstream.CallCountFor(FakeUpstream.ITunesHost));
    }
}
