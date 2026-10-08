namespace ReveilMusical.Domain.Model;

/// <summary>
/// Préférences d'un utilisateur, telles que les renvoie le service utilisateur : un morceau par
/// météo, des surcharges par jour + météo, un morceau de secours, le canal préféré et les coordonnées.
/// </summary>
public sealed class UserProfile
{
    private readonly IReadOnlyDictionary<ChannelId, ContactAddress> _contacts;
    private readonly IReadOnlyDictionary<WeatherCondition, TrackRequest> _tracksByWeather;
    private readonly IReadOnlyDictionary<DayAndWeather, TrackRequest> _tracksByDayAndWeather;

    public UserProfile(
        UserId id,
        string displayName,
        ChannelId preferredChannel,
        IReadOnlyDictionary<ChannelId, ContactAddress> contacts,
        IReadOnlyDictionary<WeatherCondition, TrackRequest> tracksByWeather,
        IReadOnlyDictionary<DayAndWeather, TrackRequest> tracksByDayAndWeather,
        TrackRequest fallbackTrack)
    {
        Id = id;
        DisplayName = displayName;
        PreferredChannel = preferredChannel;
        _contacts = contacts;
        _tracksByWeather = tracksByWeather;
        _tracksByDayAndWeather = tracksByDayAndWeather;
        FallbackTrack = fallbackTrack;
    }

    public UserId Id { get; }

    public string DisplayName { get; }

    public ChannelId PreferredChannel { get; }

    public TrackRequest FallbackTrack { get; }

    /// <summary>
    /// La règle du jour (README, « Règle de choix du morceau ») : les morceaux à chercher, du plus
    /// précis au plus général, sans doublon : jour + météo, puis météo, puis secours. Le premier est
    /// le choix de l'utilisateur pour ce réveil ; les suivants servent si le fournisseur ne trouve
    /// pas le précédent. Trois recherches dans un dictionnaire, pas de pattern.
    /// </summary>
    public IReadOnlyList<PreferredTrack> CandidatesFor(DayOfWeek day, WeatherCondition weather)
    {
        var candidates = new List<PreferredTrack>(capacity: 3);

        if (_tracksByDayAndWeather.TryGetValue(new DayAndWeather(day, weather), out var dayOverride))
        {
            candidates.Add(new PreferredTrack(dayOverride, PreferenceLevel.DayAndWeather));
        }

        if (_tracksByWeather.TryGetValue(weather, out var weatherChoice))
        {
            candidates.Add(new PreferredTrack(weatherChoice, PreferenceLevel.Weather));
        }

        candidates.Add(new PreferredTrack(FallbackTrack, PreferenceLevel.Fallback));

        // Le même morceau à deux niveaux n'est cherché qu'une fois, au niveau le plus précis.
        return [.. candidates.DistinctBy(candidate => candidate.Request.Normalized)];
    }

    public ContactAddress? ContactFor(ChannelId channel) =>
        _contacts.TryGetValue(channel, out var contact) ? contact : null;
}
