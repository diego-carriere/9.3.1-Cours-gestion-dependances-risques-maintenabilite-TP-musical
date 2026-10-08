using ReveilMusical.Domain.Model;

namespace ReveilMusical.Domain.Abstractions;

/// <summary>Dernier recours quand aucun fournisseur ne répond : renvoie toujours un morceau.</summary>
public interface IFallbackPlaylist
{
    public Track Pick(WeatherCondition weather);
}
