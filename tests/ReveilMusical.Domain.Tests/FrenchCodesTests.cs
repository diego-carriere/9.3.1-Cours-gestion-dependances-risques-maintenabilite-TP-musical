using ReveilMusical.Domain.Model;

namespace ReveilMusical.Domain.Tests;

public sealed class FrenchCodesTests
{
    [Theory]
    [InlineData("SOLEIL", WeatherCondition.Sunny)]
    [InlineData("pluie", WeatherCondition.Rainy)]
    [InlineData(" Neige ", WeatherCondition.Snowy)]
    [InlineData("NUAGEUX", WeatherCondition.Cloudy)]
    public void Weather_codes_of_the_brief_are_recognised_whatever_the_case(string code, WeatherCondition expected)
    {
        Assert.True(FrenchCodes.TryParseWeather(code, out var weather));
        Assert.Equal(expected, weather);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SUNNY")]
    [InlineData("BROUILLARD")]
    public void Unknown_weather_codes_are_refused(string? code)
    {
        Assert.False(FrenchCodes.TryParseWeather(code, out _));
    }

    [Theory]
    [InlineData("LUNDI", DayOfWeek.Monday)]
    [InlineData("mardi", DayOfWeek.Tuesday)]
    [InlineData("Mercredi", DayOfWeek.Wednesday)]
    [InlineData("JEUDI", DayOfWeek.Thursday)]
    [InlineData("VENDREDI", DayOfWeek.Friday)]
    [InlineData("SAMEDI", DayOfWeek.Saturday)]
    [InlineData("DIMANCHE", DayOfWeek.Sunday)]
    public void Day_codes_are_recognised_whatever_the_case(string code, DayOfWeek expected)
    {
        Assert.True(FrenchCodes.TryParseDay(code, out var day));
        Assert.Equal(expected, day);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("MONDAY")]
    [InlineData("1")]
    public void Unknown_day_codes_are_refused(string? code)
    {
        Assert.False(FrenchCodes.TryParseDay(code, out _));
    }

    [Fact]
    public void Every_weather_and_every_day_has_a_code_that_parses_back()
    {
        foreach (var weather in Enum.GetValues<WeatherCondition>())
        {
            Assert.True(FrenchCodes.TryParseWeather(FrenchCodes.Of(weather), out var parsed));
            Assert.Equal(weather, parsed);
        }

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            Assert.True(FrenchCodes.TryParseDay(FrenchCodes.Of(day), out var parsed));
            Assert.Equal(day, parsed);
        }
    }
}
