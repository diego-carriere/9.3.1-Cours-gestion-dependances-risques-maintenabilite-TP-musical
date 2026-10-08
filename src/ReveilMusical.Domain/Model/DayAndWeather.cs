namespace ReveilMusical.Domain.Model;

/// <summary>Clé d'une surcharge de préférences : un jour précis, par une météo précise.</summary>
public readonly record struct DayAndWeather(DayOfWeek Day, WeatherCondition Weather);
