using ReveilMusical.Domain.Model;

namespace ReveilMusical.TestSupport;

/// <summary>Construit un <see cref="UserProfile"/> lisible en test, avec des valeurs par défaut sûres.</summary>
public sealed class ProfileBuilder
{
    private readonly string _id;
    private readonly Dictionary<ChannelId, ContactAddress> _contacts = [];
    private readonly Dictionary<WeatherCondition, KeywordSet> _byWeather = [];
    private readonly Dictionary<DayAndWeather, KeywordSet> _byDayAndWeather = [];
    private string _displayName = "Alice";
    private string _preferredChannel = "sms";
    private string[] _fallback = ["wake up"];

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

    public ProfileBuilder ForWeather(WeatherCondition weather, params string[] keywords)
    {
        _byWeather[weather] = KeywordSet.Create(keywords).Value;
        return this;
    }

    public ProfileBuilder ForDay(DayOfWeek day, WeatherCondition weather, params string[] keywords)
    {
        _byDayAndWeather[new DayAndWeather(day, weather)] = KeywordSet.Create(keywords).Value;
        return this;
    }

    public ProfileBuilder WithFallback(params string[] keywords)
    {
        _fallback = keywords;
        return this;
    }

    public UserProfile Build() => new(
        UserId.Create(_id).Value,
        _displayName,
        ChannelId.Create(_preferredChannel).Value,
        _contacts,
        _byWeather,
        _byDayAndWeather,
        KeywordSet.Create(_fallback).Value);
}
