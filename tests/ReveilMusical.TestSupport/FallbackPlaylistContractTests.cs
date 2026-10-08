using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using Xunit;

namespace ReveilMusical.TestSupport;

/// <summary>Le dernier recours ne peut pas échouer : un morceau existe pour chaque météo.</summary>
public abstract class FallbackPlaylistContractTests
{
    protected abstract IFallbackPlaylist CreateSut();

    [Fact]
    public void Every_weather_gets_a_track()
    {
        var sut = CreateSut();

        foreach (var weather in Enum.GetValues<WeatherCondition>())
        {
            var track = sut.Pick(weather);

            Assert.False(string.IsNullOrWhiteSpace(track.Title));
            Assert.False(string.IsNullOrWhiteSpace(track.Artist));
        }
    }
}
