namespace ReveilMusical.Domain.Model;

/// <summary>
/// Les codes du brief (« SOLEIL / PLUIE / NEIGE / NUAGEUX ») et leurs équivalents pour les jours
/// (« LUNDI »…« DIMANCHE ») : vocabulaire métier, partagé par l'API et le service utilisateur.
/// Insensibles à la casse et aux espaces autour.
/// </summary>
public static class FrenchCodes
{
    public static bool TryParseWeather(string? code, out WeatherCondition weather)
    {
        (var known, weather) = Normalize(code) switch
        {
            "SOLEIL" => (true, WeatherCondition.Sunny),
            "PLUIE" => (true, WeatherCondition.Rainy),
            "NEIGE" => (true, WeatherCondition.Snowy),
            "NUAGEUX" => (true, WeatherCondition.Cloudy),
            _ => (false, default),
        };
        return known;
    }

    public static bool TryParseDay(string? code, out DayOfWeek day)
    {
        (var known, day) = Normalize(code) switch
        {
            "LUNDI" => (true, DayOfWeek.Monday),
            "MARDI" => (true, DayOfWeek.Tuesday),
            "MERCREDI" => (true, DayOfWeek.Wednesday),
            "JEUDI" => (true, DayOfWeek.Thursday),
            "VENDREDI" => (true, DayOfWeek.Friday),
            "SAMEDI" => (true, DayOfWeek.Saturday),
            "DIMANCHE" => (true, DayOfWeek.Sunday),
            _ => (false, default),
        };
        return known;
    }

    public static string Of(WeatherCondition weather) => weather switch
    {
        WeatherCondition.Sunny => "SOLEIL",
        WeatherCondition.Rainy => "PLUIE",
        WeatherCondition.Snowy => "NEIGE",
        WeatherCondition.Cloudy => "NUAGEUX",
        _ => throw new ArgumentOutOfRangeException(nameof(weather), weather, null),
    };

    public static string Of(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "LUNDI",
        DayOfWeek.Tuesday => "MARDI",
        DayOfWeek.Wednesday => "MERCREDI",
        DayOfWeek.Thursday => "JEUDI",
        DayOfWeek.Friday => "VENDREDI",
        DayOfWeek.Saturday => "SAMEDI",
        DayOfWeek.Sunday => "DIMANCHE",
        _ => throw new ArgumentOutOfRangeException(nameof(day), day, null),
    };

    private static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();
}
