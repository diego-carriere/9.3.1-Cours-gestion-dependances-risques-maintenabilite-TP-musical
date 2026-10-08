using ReveilMusical.Domain.Model;

namespace ReveilMusical.Domain.Tests;

public sealed class WakeUpMessageTests
{
    private static readonly Track Track = new("Monday Monday", "The Mamas & the Papas");

    [Fact]
    public void Body_greets_the_user_and_names_the_day_the_weather_and_the_track()
    {
        var message = WakeUpMessage.Compose("Alice", Track, DayOfWeek.Monday, WeatherCondition.Rainy);

        Assert.Equal(
            "Bonjour Alice ! Ce lundi s'annonce pluvieux : réveil en musique avec « Monday Monday » de The Mamas & the Papas.",
            message.Body);
        Assert.Equal("Réveil musical", message.Title);
        Assert.Equal(Track, message.Track);
    }

    [Fact]
    public void Every_day_and_every_weather_has_a_wording()
    {
        // Pas de CultureInfo("fr-FR") : l'hôte tourne en InvariantGlobalization, les noms
        // français sont donc une table du domaine, vérifiée ici de façon exhaustive.
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            foreach (var weather in Enum.GetValues<WeatherCondition>())
            {
                var body = WakeUpMessage.Compose("Alice", new Track("Titre", "Artiste"), day, weather).Body;

                Assert.DoesNotContain(day.ToString(), body, StringComparison.Ordinal);
                Assert.DoesNotContain(weather.ToString(), body, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void A_blank_display_name_gives_a_neutral_greeting()
    {
        var message = WakeUpMessage.Compose(" ", Track, DayOfWeek.Sunday, WeatherCondition.Sunny);

        Assert.StartsWith("Bonjour ! Ce dimanche s'annonce ensoleillé", message.Body, StringComparison.Ordinal);
    }
}
