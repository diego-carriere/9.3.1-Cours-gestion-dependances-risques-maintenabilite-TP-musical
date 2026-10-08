using ReveilMusical.Domain.Model;

namespace ReveilMusical.TestSupport;

/// <summary>Construit un <see cref="UserProfile"/> lisible en test, avec des valeurs par défaut sûres.</summary>
public sealed class ProfileBuilder
{
    private readonly string _id;
    private readonly Dictionary<ChannelId, ContactAddress> _contacts = [];
    private readonly Dictionary<WeatherCondition, TrackRequest> _byWeather = [];
    private readonly Dictionary<DayAndWeather, TrackRequest> _byDayAndWeather = [];
    private string _displayName = "Alice";
    private string _preferredChannel = "sms";
    private TrackRequest _fallback = TrackRequest.Create("Wake Me Up").Value;

    public ProfileBuilder(string id = "42") => _id = id;

    public ProfileBuilder Named(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    public ProfileBuilder Prefers(string channel)
    {
        _preferredChannel = channel;
        return this;
    }

    public ProfileBuilder WithContact(string channel, string address)
    {
        _contacts[ChannelId.Create(channel).Value] = ContactAddress.Create(address).Value;
        return this;
    }

    public ProfileBuilder ForWeather(WeatherCondition weather, string title, string? artist = null)
    {
        _byWeather[weather] = TrackRequest.Create(title, artist).Value;
        return this;
    }

    public ProfileBuilder ForDay(DayOfWeek day, WeatherCondition weather, string title, string? artist = null)
    {
        _byDayAndWeather[new DayAndWeather(day, weather)] = TrackRequest.Create(title, artist).Value;
        return this;
    }

    public ProfileBuilder WithFallback(string title, string? artist = null)
    {
        _fallback = TrackRequest.Create(title, artist).Value;
        return this;
    }

    public UserProfile Build() => new(
        UserId.Create(_id).Value,
        _displayName,
        ChannelId.Create(_preferredChannel).Value,
        _contacts,
        _byWeather,
        _byDayAndWeather,
        _fallback);
}
