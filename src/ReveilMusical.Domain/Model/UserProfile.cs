namespace ReveilMusical.Domain.Model;

/// <summary>
/// Préférences d'un utilisateur, telles que les renvoie le service utilisateur : mots-clés par
/// météo, surcharges par jour + météo, mots-clés de secours, canal préféré et coordonnées.
/// </summary>
public sealed class UserProfile
{
    private readonly IReadOnlyDictionary<ChannelId, ContactAddress> _contacts;
    private readonly IReadOnlyDictionary<WeatherCondition, KeywordSet> _keywordsByWeather;
    private readonly IReadOnlyDictionary<DayAndWeather, KeywordSet> _keywordsByDayAndWeather;

    public UserProfile(
        UserId id,
        string displayName,
        ChannelId preferredChannel,
        IReadOnlyDictionary<ChannelId, ContactAddress> contacts,
        IReadOnlyDictionary<WeatherCondition, KeywordSet> keywordsByWeather,
        IReadOnlyDictionary<DayAndWeather, KeywordSet> keywordsByDayAndWeather,
        KeywordSet fallbackKeywords)
    {
        Id = id;
        DisplayName = displayName;
        PreferredChannel = preferredChannel;
        _contacts = contacts;
        _keywordsByWeather = keywordsByWeather;
        _keywordsByDayAndWeather = keywordsByDayAndWeather;
        FallbackKeywords = fallbackKeywords;
    }

    public UserId Id { get; }

    public string DisplayName { get; }

    public ChannelId PreferredChannel { get; }

    public KeywordSet FallbackKeywords { get; }

    /// <summary>
    /// La règle du jour (README, « Règle de choix du morceau ») : jour + météo, sinon météo,
    /// sinon secours. Trois recherches dans un dictionnaire, pas de pattern.
    /// </summary>
    public KeywordSelection KeywordsFor(DayOfWeek day, WeatherCondition weather)
    {
        if (_keywordsByDayAndWeather.TryGetValue(new DayAndWeather(day, weather), out var dayOverride))
        {
            return new KeywordSelection(dayOverride, PreferenceLevel.DayAndWeather);
        }

        if (_keywordsByWeather.TryGetValue(weather, out var weatherChoice))
        {
            return new KeywordSelection(weatherChoice, PreferenceLevel.Weather);
        }

        return new KeywordSelection(FallbackKeywords, PreferenceLevel.Fallback);
    }

    public ContactAddress? ContactFor(ChannelId channel) =>
        _contacts.TryGetValue(channel, out var contact) ? contact : null;
}
