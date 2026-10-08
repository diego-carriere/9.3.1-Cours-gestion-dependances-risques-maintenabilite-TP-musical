using ReveilMusical.Domain.Model;

namespace ReveilMusical.Domain.Tests;

public sealed class UserProfileTests
{
    private static readonly KeywordSet Sunny = Set("soleil", "beau temps");
    private static readonly KeywordSet Rainy = Set("pluie", "rain");
    private static readonly KeywordSet MondayRain = Set("monday", "blues");
    private static readonly KeywordSet Fallback = Set("wake up", "morning");

    private static readonly ChannelId Sms = ChannelId.Create("sms").Value;
    private static readonly ChannelId Email = ChannelId.Create("email").Value;

    private static readonly UserProfile Profile = new(
        UserId.Create("42").Value,
        "Alice",
        Sms,
        new Dictionary<ChannelId, ContactAddress> { [Sms] = ContactAddress.Create("+33612345678").Value },
        new Dictionary<WeatherCondition, KeywordSet>
        {
            [WeatherCondition.Sunny] = Sunny,
            [WeatherCondition.Rainy] = Rainy,
        },
        new Dictionary<DayAndWeather, KeywordSet>
        {
            [new DayAndWeather(DayOfWeek.Monday, WeatherCondition.Rainy)] = MondayRain,
        },
        Fallback);

    [Fact]
    public void A_day_and_weather_override_wins_over_the_weather_choice()
    {
        var selection = Profile.KeywordsFor(DayOfWeek.Monday, WeatherCondition.Rainy);

        Assert.Equal(new KeywordSelection(MondayRain, PreferenceLevel.DayAndWeather), selection);
    }

    [Fact]
    public void Without_an_override_for_that_day_the_weather_choice_applies()
    {
        var selection = Profile.KeywordsFor(DayOfWeek.Tuesday, WeatherCondition.Rainy);

        Assert.Equal(new KeywordSelection(Rainy, PreferenceLevel.Weather), selection);
    }

    [Fact]
    public void An_override_for_another_weather_on_the_same_day_does_not_apply()
    {
        var selection = Profile.KeywordsFor(DayOfWeek.Monday, WeatherCondition.Sunny);

        Assert.Equal(new KeywordSelection(Sunny, PreferenceLevel.Weather), selection);
    }

    [Fact]
    public void A_weather_the_user_did_not_cover_falls_back_to_the_fallback_keywords()
    {
        var selection = Profile.KeywordsFor(DayOfWeek.Tuesday, WeatherCondition.Snowy);

        Assert.Equal(new KeywordSelection(Fallback, PreferenceLevel.Fallback), selection);
    }

    [Fact]
    public void ContactFor_returns_the_address_of_a_known_channel()
    {
        Assert.Equal("+33612345678", Profile.ContactFor(Sms)?.Value);
    }

    [Fact]
    public void ContactFor_returns_null_for_a_channel_without_contact()
    {
        Assert.Null(Profile.ContactFor(Email));
    }

    [Fact]
    public void The_profile_exposes_its_identity_and_preferences()
    {
        Assert.Equal("42", Profile.Id.Value);
        Assert.Equal("Alice", Profile.DisplayName);
        Assert.Equal(Sms, Profile.PreferredChannel);
        Assert.Equal(Fallback, Profile.FallbackKeywords);
    }

    private static KeywordSet Set(params string[] keywords) => KeywordSet.Create(keywords).Value;
}
