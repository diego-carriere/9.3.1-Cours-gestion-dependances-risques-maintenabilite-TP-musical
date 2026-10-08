namespace ReveilMusical.Domain.Model;

/// <summary>
/// Le message de réveil, indépendant du canal. Chaque adaptateur le met ensuite en forme pour son
/// canal (objet d'email, SMS tronqué, titre de push).
/// </summary>
public sealed record WakeUpMessage(string Title, string Body, Track Track)
{
    public const string DefaultTitle = "Réveil musical";

    /// <summary>
    /// Les noms français sont une table du domaine plutôt qu'un <c>CultureInfo("fr-FR")</c> :
    /// l'hôte tourne en InvariantGlobalization, une culture serait une dépendance cachée.
    /// </summary>
    public static WakeUpMessage Compose(string displayName, Track track, DayOfWeek day, WeatherCondition weather)
    {
        var greeting = string.IsNullOrWhiteSpace(displayName) ? "Bonjour !" : $"Bonjour {displayName.Trim()} !";
        var body = $"{greeting} Ce {DayName(day)} s'annonce {WeatherWording(weather)} : " +
                   $"réveil en musique avec « {track.Title} » de {track.Artist}.";

        return new WakeUpMessage(DefaultTitle, body, track);
    }

    private static string DayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "lundi",
        DayOfWeek.Tuesday => "mardi",
        DayOfWeek.Wednesday => "mercredi",
        DayOfWeek.Thursday => "jeudi",
        DayOfWeek.Friday => "vendredi",
        DayOfWeek.Saturday => "samedi",
        DayOfWeek.Sunday => "dimanche",
        _ => throw new ArgumentOutOfRangeException(nameof(day), day, null),
    };

    private static string WeatherWording(WeatherCondition weather) => weather switch
    {
        WeatherCondition.Sunny => "ensoleillé",
        WeatherCondition.Rainy => "pluvieux",
        WeatherCondition.Snowy => "neigeux",
        WeatherCondition.Cloudy => "nuageux",
        _ => throw new ArgumentOutOfRangeException(nameof(weather), weather, null),
    };
}
