namespace ReveilMusical.Domain.Model;

/// <summary>Niveau de préférence qui a fourni le morceau, du plus précis au plus général.</summary>
public enum PreferenceLevel
{
    DayAndWeather,
    Weather,
    Fallback,
}

/// <summary>Un morceau choisi par l'utilisateur, et le niveau de préférence qui l'a fourni.</summary>
public sealed record PreferredTrack(TrackRequest Request, PreferenceLevel Level);
