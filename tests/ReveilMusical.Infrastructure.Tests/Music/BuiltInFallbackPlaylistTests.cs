using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Infrastructure.Tests.Music;

public sealed class BuiltInFallbackPlaylistTests : FallbackPlaylistContractTests
{
    [Fact]
    public void The_pick_is_random_among_the_tracks_of_that_weather()
    {
        var random = new FakeRandom(0, 1);
        var sut = new BuiltInFallbackPlaylist(random);

        var first = sut.Pick(WeatherCondition.Rainy);
        var second = sut.Pick(WeatherCondition.Rainy);

        Assert.NotEqual(first, second);
        Assert.All(random.Requests, max => Assert.True(max >= 2, "Au moins deux morceaux par météo."));
    }

    protected override IFallbackPlaylist CreateSut() => new BuiltInFallbackPlaylist(new FakeRandom());
}
