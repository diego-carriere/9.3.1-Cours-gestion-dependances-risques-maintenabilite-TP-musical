using ReveilMusical.Domain.Model;

namespace ReveilMusical.Domain.Tests;

public sealed class UserProfileTests
{
    private static readonly TrackRequest Sunny = Request("Here Comes the Sun", "The Beatles");
    private static readonly TrackRequest Rainy = Request("Set Fire to the Rain", "Adele");
    private static readonly TrackRequest MondayRain = Request("Manic Monday", "The Bangles");
    private static readonly TrackRequest Fallback = Request("Wake Me Up", "Avicii");

    private static readonly ChannelId Sms = ChannelId.Create("sms").Value;
    private static readonly ChannelId Email = ChannelId.Create("email").Value;

    private static readonly UserProfile Profile = Build(
        new Dictionary<WeatherCondition, TrackRequest>
        {
            [WeatherCondition.Sunny] = Sunny,
            [WeatherCondition.Rainy] = Rainy,
        },
        new Dictionary<DayAndWeather, TrackRequest>
        {
            [new DayAndWeather(DayOfWeek.Monday, WeatherCondition.Rainy)] = MondayRain,
        },
        Fallback);

    [Fact]
    public void A_day_and_weather_override_comes_first_then_the_weather_choice_then_the_fallback()
    {
        Assert.Equal(
            [
                new PreferredTrack(MondayRain, PreferenceLevel.DayAndWeather),
                new PreferredTrack(Rainy, PreferenceLevel.Weather),
                new PreferredTrack(Fallback, PreferenceLevel.Fallback),
            ],
            Profile.CandidatesFor(DayOfWeek.Monday, WeatherCondition.Rainy));
    }

    [Fact]
    public void Without_an_override_for_that_day_the_weather_choice_comes_first()
    {
        Assert.Equal(
            [new PreferredTrack(Rainy, PreferenceLevel.Weather), new PreferredTrack(Fallback, PreferenceLevel.Fallback)],
            Profile.CandidatesFor(DayOfWeek.Tuesday, WeatherCondition.Rainy));
    }

    [Fact]
    public void An_override_for_another_weather_on_the_same_day_does_not_apply()
    {
        Assert.Equal(
            new PreferredTrack(Sunny, PreferenceLevel.Weather),
            Profile.CandidatesFor(DayOfWeek.Monday, WeatherCondition.Sunny)[0]);
    }

    [Fact]
    public void A_weather_the_user_did_not_cover_falls_back_to_the_fallback_track()
    {
        Assert.Equal(
            [new PreferredTrack(Fallback, PreferenceLevel.Fallback)],
            Profile.CandidatesFor(DayOfWeek.Tuesday, WeatherCondition.Snowy));
    }

    [Fact]
    public void The_same_track_at_two_levels_is_proposed_once_at_the_most_precise_level()
    {
        var profile = Build(
            new Dictionary<WeatherCondition, TrackRequest> { [WeatherCondition.Rainy] = Request("wake me up", "AVICII") },
            new Dictionary<DayAndWeather, TrackRequest>(),
            Fallback);

        Assert.Equal(
            [new PreferredTrack(Request("wake me up", "AVICII"), PreferenceLevel.Weather)],
            profile.CandidatesFor(DayOfWeek.Tuesday, WeatherCondition.Rainy));
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
        Assert.Equal(Fallback, Profile.FallbackTrack);
    }

    private static TrackRequest Request(string title, string artist) => TrackRequest.Create(title, artist).Value;

    private static UserProfile Build(
        IReadOnlyDictionary<WeatherCondition, TrackRequest> byWeather,
        IReadOnlyDictionary<DayAndWeather, TrackRequest> byDayAndWeather,
        TrackRequest fallback) => new(
        UserId.Create("42").Value,
        "Alice",
        Sms,
        new Dictionary<ChannelId, ContactAddress> { [Sms] = ContactAddress.Create("+33612345678").Value },
        byWeather,
        byDayAndWeather,
        fallback);
}
