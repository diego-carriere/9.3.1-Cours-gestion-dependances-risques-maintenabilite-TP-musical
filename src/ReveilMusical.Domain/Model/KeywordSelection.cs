namespace ReveilMusical.Domain.Model;

/// <summary>Niveau de préférence qui a fourni les mots-clés, du plus précis au plus général.</summary>
public enum PreferenceLevel
{
    DayAndWeather,
    Weather,
    Fallback,
}

public sealed record KeywordSelection(KeywordSet Keywords, PreferenceLevel Level);
