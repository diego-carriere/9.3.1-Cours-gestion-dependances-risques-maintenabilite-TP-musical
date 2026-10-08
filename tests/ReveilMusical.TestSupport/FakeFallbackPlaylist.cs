using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.TestSupport;

public sealed class FakeFallbackPlaylist : IFallbackPlaylist
{
    public static readonly Track DefaultTrack = new("Morceau de secours", "Playlist locale");

    public List<WeatherCondition> Picks { get; } = [];

    public Track Pick(WeatherCondition weather)
    {
        Picks.Add(weather);
        return DefaultTrack;
    }
}
