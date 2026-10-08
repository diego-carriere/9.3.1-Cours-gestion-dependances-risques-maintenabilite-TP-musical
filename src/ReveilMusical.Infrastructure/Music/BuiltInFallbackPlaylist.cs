using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>
/// La « petite liste de morceaux codée en dur » du brief : dernier recours quand aucun fournisseur
/// ne répond. Ne fait aucune E/S, donc ne peut pas tomber en panne.
/// </summary>
internal sealed class BuiltInFallbackPlaylist : IFallbackPlaylist
{
    private readonly IRandom _random;

    public BuiltInFallbackPlaylist(IRandom random) => _random = random;

    public Track Pick(WeatherCondition weather)
    {
        var tracks = TracksFor(weather);
        return tracks[_random.NextIndex(tracks.Length)];
    }

    private static Track[] TracksFor(WeatherCondition weather) => weather switch
    {
        WeatherCondition.Sunny => [new("Here Comes the Sun", "The Beatles"), new("Walking on Sunshine", "Katrina and the Waves")],
        WeatherCondition.Rainy => [new("Singin' in the Rain", "Gene Kelly"), new("Riders on the Storm", "The Doors")],
        WeatherCondition.Snowy => [new("Let It Snow! Let It Snow! Let It Snow!", "Dean Martin"), new("Snow (Hey Oh)", "Red Hot Chili Peppers")],
        _ => [new("Both Sides Now", "Joni Mitchell"), new("Cloudbusting", "Kate Bush")],
    };
}
